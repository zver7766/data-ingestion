namespace DataIngestion.Contracts.Customers;

public sealed record CustomerTransactionsResponse(
    long CustomerId,
    int Page,
    int PageSize,
    long Total,
    IReadOnlyList<CustomerTransactionDto> Items);

public sealed record CustomerTransactionDto(
    long Id,
    DateTime TransactionDateUtc,
    decimal Amount,
    string Currency,
    string SourceChannel);