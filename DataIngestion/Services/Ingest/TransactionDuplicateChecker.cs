using DataIngestion.Data;
using DataIngestion.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DataIngestion.Services.Ingest;

public sealed class TransactionDuplicateChecker(AppDbContext db)
{
    public async Task<bool> IsDuplicateAsync(
        long customerId,
        DateTime transactionDateUtc,
        decimal amount,
        string currency,
        string sourceChannel,
        CancellationToken ct)
    {
        var windowStart = transactionDateUtc.AddSeconds(-IngestionConstants.TransactionDuplicateWindowSeconds);
        var windowEnd = transactionDateUtc.AddSeconds(IngestionConstants.TransactionDuplicateWindowSeconds);

        return await db.IngestionEvents.AsNoTracking().AnyAsync(x =>
            x.CustomerId == customerId &&
            x.Amount == amount &&
            x.Currency == currency &&
            x.SourceChannel == sourceChannel &&
            x.TransactionDate >= windowStart &&
            x.TransactionDate <= windowEnd, ct);
    }
}