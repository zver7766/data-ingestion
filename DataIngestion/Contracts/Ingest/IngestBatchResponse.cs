namespace DataIngestion.Contracts.Ingest;

public sealed record IngestBatchResponse(
    int TotalRows,
    int AcceptedRows,
    int RejectedRows,
    IReadOnlyList<IngestBatchRowResult> Rows);

public sealed record IngestBatchRowResult(
    int RowNumber,
    string Status,
    long? IngestionEventId,
    string? Error);