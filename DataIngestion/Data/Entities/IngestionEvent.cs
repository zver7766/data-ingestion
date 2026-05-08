namespace DataIngestion.Data.Entities;

public sealed class IngestionEvent
{
    public long Id { get; set; }

    public long CustomerId { get; set; }

    public DateTime TransactionDate { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "";

    public string SourceChannel { get; set; } = "";
}