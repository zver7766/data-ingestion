namespace DataIngestion.Services.Ingest;

public abstract record TransactionValidationResult
{
    public sealed record Valid(
        long CustomerId,
        DateTime TransactionDateUtc,
        decimal Amount,
        string Currency,
        string SourceChannel) : TransactionValidationResult;

    public sealed record Invalid(string Message) : TransactionValidationResult;
}