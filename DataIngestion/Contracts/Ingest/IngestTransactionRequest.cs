namespace DataIngestion.Contracts.Ingest;

public sealed record IngestTransactionRequest(
    long CustomerId,
    DateTime TransactionDate,
    decimal Amount,
    string? Currency,
    string? SourceChannel);