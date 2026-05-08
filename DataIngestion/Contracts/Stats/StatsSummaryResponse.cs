namespace DataIngestion.Contracts.Stats;

public sealed record StatsSummaryResponse(
    StatsTotals Totals,
    StatsRecentActivity Recent,
    StatsRange Range,
    IReadOnlyList<StatsByCurrencyItem> ByCurrency,
    IReadOnlyList<StatsBySourceChannelItem> BySourceChannel,
    IReadOnlyList<StatsTopCustomerItem> TopCustomersByAmount);

public sealed record StatsTotals(
    long TotalCustomers,
    long TotalTransactions,
    decimal TotalAmount);

public sealed record StatsRecentActivity(
    long TransactionsLast24h,
    long TransactionsLast7d,
    decimal AmountLast24h,
    decimal AmountLast7d);

public sealed record StatsRange(
    DateTime? FirstTransactionUtc,
    DateTime? LastTransactionUtc);

public sealed record StatsByCurrencyItem(
    string Currency,
    long Transactions,
    decimal TotalAmount);

public sealed record StatsBySourceChannelItem(
    string SourceChannel,
    long Transactions,
    decimal TotalAmount);

public sealed record StatsTopCustomerItem(
    long CustomerId,
    long Transactions,
    decimal TotalAmount);

