using DataIngestion.Data.Entities;

namespace DataIngestion.Services.Ingest;

public abstract record TransactionIngestionResult
{
    public sealed record Created(IngestionEvent Entity) : TransactionIngestionResult;
    public sealed record Duplicate : TransactionIngestionResult;
    public sealed record CustomerNotFound(long CustomerId) : TransactionIngestionResult;
    public sealed record Invalid(string Message) : TransactionIngestionResult;
}