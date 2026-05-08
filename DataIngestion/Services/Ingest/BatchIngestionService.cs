using CsvHelper;
using CsvHelper.Configuration;
using DataIngestion.Contracts.Ingest;
using DataIngestion.Data;
using DataIngestion.Data.Entities;
using DataIngestion.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace DataIngestion.Services.Ingest;

public sealed class BatchIngestionService(
    AppDbContext db,
    TransactionValidationService validationService)
{
    private static readonly CsvConfiguration CsvConfig = new(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
        TrimOptions = TrimOptions.Trim,
        IgnoreBlankLines = true,
        BadDataFound = null,
        MissingFieldFound = null,
        HeaderValidated = null
    };

    public async Task<IngestBatchResponse> IngestCsvAsync(Stream csvStream, CancellationToken ct)
    {
        using var reader = new StreamReader(csvStream, leaveOpen: true);
        using var csv = new CsvReader(reader, CsvConfig);

        await csv.ReadAsync();
        csv.ReadHeader();

        var header = csv.HeaderRecord ?? Array.Empty<string>();
        var col = new ColumnMap(header);

        var results = new List<IngestBatchRowResult>(capacity: 1024);
        var chunk = new List<StagedRow>(capacity: IngestionConstants.BatchChunkSize);

        var total = 0;
        var accepted = 0;
        var rejected = 0;

        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();
            total++;

            var rowNumber = csv.Parser.Row;
            var staged = StageRow(csv, col, rowNumber);
            chunk.Add(staged);

            if (chunk.Count >= IngestionConstants.BatchChunkSize)
            {
                var (a, r) = await ProcessChunkAsync(chunk, results, ct);
                accepted += a;
                rejected += r;
                chunk.Clear();
            }
        }

        if (chunk.Count > 0)
        {
            var (a, r) = await ProcessChunkAsync(chunk, results, ct);
            accepted += a;
            rejected += r;
        }

        return new IngestBatchResponse(total, accepted, rejected, results);
    }

    private static StagedRow StageRow(CsvReader csv, ColumnMap col, int rowNumber)
    {
        try
        {
            var customerIdStr = col.Get(csv, "customerId", "customer_id", "customer");
            if (!long.TryParse(customerIdStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var customerId))
            {
                return new StagedRow(rowNumber, null, "customerId must be an integer.");
            }

            var dateStr = col.Get(csv, "transactionDate", "transaction_date", "date", "transaction_date_utc");
            if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var transactionDate))
            {
                return new StagedRow(rowNumber, null, "transactionDate must be a valid datetime.");
            }

            var amountStr = col.Get(csv, "amount", "transactionAmount", "transaction_amount");
            if (!decimal.TryParse(amountStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            {
                return new StagedRow(rowNumber, null, "amount must be a decimal number.");
            }

            var currency = col.Get(csv, "currency", "ccy");
            var sourceChannel = col.Get(csv, "sourceChannel", "source_channel", "channel");

            var request = new IngestTransactionRequest(
                CustomerId: customerId,
                TransactionDate: transactionDate,
                Amount: amount,
                Currency: currency,
                SourceChannel: sourceChannel);

            return new StagedRow(rowNumber, request, null);
        }
        catch (Exception ex)
        {
            return new StagedRow(rowNumber, null, ex.Message);
        }
    }

    private async Task<(int accepted, int rejected)> ProcessChunkAsync(
        List<StagedRow> chunk,
        List<IngestBatchRowResult> results,
        CancellationToken ct)
    {
        var accepted = 0;
        var rejected = 0;

        var validRows = new List<ValidStagedRow>(chunk.Count);
        foreach (var row in chunk)
        {
            if (row.ParseError is not null)
            {
                rejected++;
                results.Add(new IngestBatchRowResult(row.RowNumber, "rejected", null, row.ParseError));
                continue;
            }

            var validation = validationService.ValidateAndNormalize(row.Request!);
            if (validation is TransactionValidationResult.Invalid invalid)
            {
                rejected++;
                results.Add(new IngestBatchRowResult(row.RowNumber, "rejected", null, invalid.Message));
                continue;
            }

            validRows.Add(new ValidStagedRow(row.RowNumber, (TransactionValidationResult.Valid)validation));
        }

        if (validRows.Count == 0)
        {
            return (accepted, rejected);
        }

        var customerIds = validRows.Select(v => v.Valid.CustomerId).Distinct().ToArray();
        var existingCustomers = await db.Customers.AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync(ct);
        var customerSet = existingCustomers.ToHashSet();

        var customerFiltered = new List<ValidStagedRow>(validRows.Count);
        foreach (var row in validRows)
        {
            if (!customerSet.Contains(row.Valid.CustomerId))
            {
                rejected++;
                results.Add(new IngestBatchRowResult(row.RowNumber, "rejected", null, $"Customer {row.Valid.CustomerId} not found."));
                continue;
            }
            customerFiltered.Add(row);
        }

        if (customerFiltered.Count == 0)
        {
            return (accepted, rejected);
        }

        // Prefetch potential duplicates in one range query (fast, chunk-bounded)
        var minDate = customerFiltered.Min(x => x.Valid.TransactionDateUtc).AddSeconds(-IngestionConstants.TransactionDuplicateWindowSeconds);
        var maxDate = customerFiltered.Max(x => x.Valid.TransactionDateUtc).AddSeconds(IngestionConstants.TransactionDuplicateWindowSeconds);

        var candidates = await db.IngestionEvents.AsNoTracking()
            .Where(e => customerIds.Contains(e.CustomerId) && e.TransactionDate >= minDate && e.TransactionDate <= maxDate)
            .Select(e => new ExistingKey(e.CustomerId, e.Amount, e.Currency, e.SourceChannel, e.TransactionDate))
            .ToListAsync(ct);

        var existingByKey = new Dictionary<DedupKey, List<DateTime>>();
        foreach (var c in candidates)
        {
            var key = new DedupKey(c.CustomerId, c.Amount, c.Currency, c.SourceChannel);
            if (!existingByKey.TryGetValue(key, out var list))
            {
                list = new List<DateTime>();
                existingByKey[key] = list;
            }
            list.Add(c.TransactionDateUtc);
        }
        foreach (var list in existingByKey.Values)
        {
            list.Sort();
        }

        // Also prevent duplicates within the same chunk
        var acceptedByKey = new Dictionary<DedupKey, List<DateTime>>();

        var entities = new List<(int rowNumber, IngestionEvent entity)>(customerFiltered.Count);
        foreach (var row in customerFiltered)
        {
            var v = row.Valid;
            var key = new DedupKey(v.CustomerId, v.Amount, v.Currency, v.SourceChannel);
            var windowStart = v.TransactionDateUtc.AddSeconds(-IngestionConstants.TransactionDuplicateWindowSeconds);
            var windowEnd = v.TransactionDateUtc.AddSeconds(IngestionConstants.TransactionDuplicateWindowSeconds);

            if (HasAnyInWindow(existingByKey, key, windowStart, windowEnd) ||
                HasAnyInWindow(acceptedByKey, key, windowStart, windowEnd))
            {
                rejected++;
                results.Add(new IngestBatchRowResult(row.RowNumber, "rejected", null, "Duplicate transaction."));
                continue;
            }

            var entity = new IngestionEvent
            {
                CustomerId = v.CustomerId,
                TransactionDate = v.TransactionDateUtc,
                Amount = v.Amount,
                Currency = v.Currency,
                SourceChannel = v.SourceChannel
            };
            entities.Add((row.RowNumber, entity));

            if (!acceptedByKey.TryGetValue(key, out var list))
            {
                list = new List<DateTime>();
                acceptedByKey[key] = list;
            }
            InsertSorted(list, v.TransactionDateUtc);
        }

        if (entities.Count == 0)
        {
            return (accepted, rejected);
        }

        db.IngestionEvents.AddRange(entities.Select(x => x.entity));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            foreach (var (rowNumber, _) in entities)
            {
                rejected++;
                results.Add(new IngestBatchRowResult(rowNumber, "rejected", null, "Duplicate transaction."));
            }
            return (accepted, rejected);
        }

        foreach (var (rowNumber, entity) in entities)
        {
            accepted++;
            results.Add(new IngestBatchRowResult(rowNumber, "accepted", entity.Id, null));
        }

        return (accepted, rejected);
    }

    private static bool HasAnyInWindow(
        Dictionary<DedupKey, List<DateTime>> dict,
        DedupKey key,
        DateTime windowStart,
        DateTime windowEnd)
    {
        return dict.TryGetValue(key, out var list) && HasAnyInWindowSorted(list, windowStart, windowEnd);
    }

    private static bool HasAnyInWindowSorted(List<DateTime> sorted, DateTime windowStart, DateTime windowEnd)
    {
        var idx = sorted.BinarySearch(windowStart);
        if (idx < 0) idx = ~idx;
        return idx < sorted.Count && sorted[idx] <= windowEnd;
    }

    private static void InsertSorted(List<DateTime> sorted, DateTime value)
    {
        var idx = sorted.BinarySearch(value);
        if (idx < 0) idx = ~idx;
        sorted.Insert(idx, value);
    }

    private readonly record struct StagedRow(int RowNumber, IngestTransactionRequest? Request, string? ParseError);
    private readonly record struct ValidStagedRow(int RowNumber, TransactionValidationResult.Valid Valid);
    private readonly record struct DedupKey(long CustomerId, decimal Amount, string Currency, string SourceChannel);
    private readonly record struct ExistingKey(long CustomerId, decimal Amount, string Currency, string SourceChannel, DateTime TransactionDateUtc);

    private sealed class ColumnMap(string[] header)
    {
        private readonly Dictionary<string, string> _normalizedToActual = header
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .ToDictionary(Normalize, h => h, StringComparer.OrdinalIgnoreCase);

        public string Get(CsvReader csv, params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (_normalizedToActual.TryGetValue(Normalize(candidate), out var actual))
                {
                    return csv.GetField(actual) ?? string.Empty;
                }
            }

            throw new InvalidOperationException($"Missing required column. Expected one of: {string.Join(", ", candidates)}.");
        }

        private static string Normalize(string value)
            => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }
}