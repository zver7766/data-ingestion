using DataIngestion.Contracts.Ingest;

namespace DataIngestion.Services.Ingest;

public interface ITransactionIngestionService
{
    Task<TransactionIngestionResult> IngestAsync(IngestTransactionRequest request, CancellationToken ct);
}

