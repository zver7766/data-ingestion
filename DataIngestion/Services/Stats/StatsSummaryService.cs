using DataIngestion.Contracts.Stats;
using DataIngestion.Data;
using Microsoft.EntityFrameworkCore;

namespace DataIngestion.Services.Stats;

public sealed class StatsSummaryService(AppDbContext db)
{
    public async Task<StatsSummaryResponse> GetSummaryAsync(CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow;
        var since24h = nowUtc.AddHours(-24);
        var since7d = nowUtc.AddDays(-7);

        var totalCustomers = await db.Customers.AsNoTracking().LongCountAsync(ct);

        var totals = await db.IngestionEvents.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalTransactions = g.LongCount(),
                TotalAmount = g.Sum(x => x.Amount)
            })
            .FirstOrDefaultAsync(ct);

        var range = await db.IngestionEvents.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                First = g.Min(x => x.TransactionDate),
                Last = g.Max(x => x.TransactionDate)
            })
            .FirstOrDefaultAsync(ct);

        var recent = await db.IngestionEvents.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count24h = g.LongCount(x => x.TransactionDate >= since24h),
                Count7d = g.LongCount(x => x.TransactionDate >= since7d),
                Amount24h = g.Where(x => x.TransactionDate >= since24h).Sum(x => (decimal?)x.Amount) ?? 0m,
                Amount7d = g.Where(x => x.TransactionDate >= since7d).Sum(x => (decimal?)x.Amount) ?? 0m
            })
            .FirstOrDefaultAsync(ct);

        var byCurrencyRaw = await db.IngestionEvents.AsNoTracking()
            .GroupBy(x => x.Currency)
            .Select(g => new
            {
                Currency = g.Key,
                Transactions = g.LongCount(),
                TotalAmount = g.Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.TotalAmount)
            .ToListAsync(ct);
        var byCurrency = byCurrencyRaw
            .Select(x => new StatsByCurrencyItem(x.Currency, x.Transactions, x.TotalAmount))
            .ToList();

        var bySourceChannelRaw = await db.IngestionEvents.AsNoTracking()
            .GroupBy(x => x.SourceChannel)
            .Select(g => new
            {
                SourceChannel = g.Key,
                Transactions = g.LongCount(),
                TotalAmount = g.Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.TotalAmount)
            .ToListAsync(ct);
        var bySourceChannel = bySourceChannelRaw
            .Select(x => new StatsBySourceChannelItem(x.SourceChannel, x.Transactions, x.TotalAmount))
            .ToList();

        var topCustomersRaw = await db.IngestionEvents.AsNoTracking()
            .GroupBy(x => EF.Property<long?>(x, "CustomerId") ?? 0)
            .Where(g => g.Key != 0)
            .Select(g => new
            {
                CustomerId = g.Key,
                Transactions = g.LongCount(),
                TotalAmount = g.Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.TotalAmount)
            .Take(10)
            .ToListAsync(ct);
        var topCustomers = topCustomersRaw
            .Select(x => new StatsTopCustomerItem(x.CustomerId, x.Transactions, x.TotalAmount))
            .ToList();

        return new StatsSummaryResponse(
            Totals: new StatsTotals(
                TotalCustomers: totalCustomers,
                TotalTransactions: totals?.TotalTransactions ?? 0,
                TotalAmount: totals?.TotalAmount ?? 0m),
            Recent: new StatsRecentActivity(
                TransactionsLast24h: recent?.Count24h ?? 0,
                TransactionsLast7d: recent?.Count7d ?? 0,
                AmountLast24h: recent?.Amount24h ?? 0m,
                AmountLast7d: recent?.Amount7d ?? 0m),
            Range: new StatsRange(
                FirstTransactionUtc: range is null ? null : range.First,
                LastTransactionUtc: range is null ? null : range.Last),
            ByCurrency: byCurrency,
            BySourceChannel: bySourceChannel,
            TopCustomersByAmount: topCustomers);
    }
}