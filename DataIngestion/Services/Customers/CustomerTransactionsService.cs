using DataIngestion.Contracts.Customers;
using DataIngestion.Data;
using DataIngestion.Data.Entities;
using DataIngestion.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DataIngestion.Services.Customers;

public sealed class CustomerTransactionsService(AppDbContext db)
{
    public async Task<(CustomerTransactionsResponse? response, string? error, int? httpStatus)> GetAsync(
        long customerId,
        CustomerTransactionsQuery request,
        CancellationToken ct)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize == 0 ? IngestionConstants.TransactionsDefaultPageSize : request.PageSize;

        if (pageSize < 1 || pageSize > IngestionConstants.TransactionsMaxPageSize)
        {
            return (null, $"pageSize must be between 1 and {IngestionConstants.TransactionsMaxPageSize}.", StatusCodes.Status400BadRequest);
        }

        var customerExists = await db.Customers.AsNoTracking().AnyAsync(c => c.Id == customerId, ct);
        if (!customerExists)
        {
            return (null, $"Customer {customerId} not found.", StatusCodes.Status404NotFound);
        }

        var transactionsQuery = db.IngestionEvents
            .AsNoTracking()
            .Where(t => t.CustomerId == customerId);

        transactionsQuery = ApplyFilters(transactionsQuery, request);

        var total = await transactionsQuery.LongCountAsync(ct);

        var skip = (page - 1) * pageSize;
        var items = await transactionsQuery
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.Id)
            .Skip(skip)
            .Take(pageSize)
            .Select(t => new CustomerTransactionDto(
                t.Id,
                t.TransactionDate,
                t.Amount,
                t.Currency,
                t.SourceChannel))
            .ToListAsync(ct);

        return (new CustomerTransactionsResponse(customerId, page, pageSize, total, items), null, null);
    }

    private static IQueryable<IngestionEvent> ApplyFilters(IQueryable<IngestionEvent> transactionsQuery, CustomerTransactionsQuery request)
    {
        if (request.FromUtc is not null)
        {
            var fromUtcNormalized = request.FromUtc.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(request.FromUtc.Value, DateTimeKind.Utc)
                : request.FromUtc.Value.ToUniversalTime();
            transactionsQuery = transactionsQuery.Where(t => t.TransactionDate >= fromUtcNormalized);
        }

        if (request.ToUtc is not null)
        {
            var toUtcNormalized = request.ToUtc.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(request.ToUtc.Value, DateTimeKind.Utc)
                : request.ToUtc.Value.ToUniversalTime();
            transactionsQuery = transactionsQuery.Where(t => t.TransactionDate <= toUtcNormalized);
        }

        if (!string.IsNullOrWhiteSpace(request.Currency))
        {
            var currency = request.Currency.Trim().ToUpperInvariant();
            transactionsQuery = transactionsQuery.Where(t => t.Currency == currency);
        }

        if (!string.IsNullOrWhiteSpace(request.SourceChannel))
        {
            var sourceChannel = request.SourceChannel.Trim().ToLowerInvariant();
            transactionsQuery = transactionsQuery.Where(t => t.SourceChannel == sourceChannel);
        }

        if (request.MinAmount is not null)
        {
            transactionsQuery = transactionsQuery.Where(t => t.Amount >= request.MinAmount.Value);
        }

        if (request.MaxAmount is not null)
        {
            transactionsQuery = transactionsQuery.Where(t => t.Amount <= request.MaxAmount.Value);
        }

        return transactionsQuery;
    }
}