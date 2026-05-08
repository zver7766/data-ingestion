namespace DataIngestion.Contracts.Customers;

public sealed class CustomerTransactionsQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; }

    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }

    public string? Currency { get; init; }
    public string? SourceChannel { get; init; }

    public decimal? MinAmount { get; init; }
    public decimal? MaxAmount { get; init; }
}