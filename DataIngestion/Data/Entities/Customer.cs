namespace DataIngestion.Data.Entities;

public sealed class Customer
{
    public long Id { get; set; }

    public string Name { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; }
}