using DataIngestion.Data;
using DataIngestion.Data.Entities;
using DataIngestion.Services.Ingest;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace DataIngestion.Tests;

public sealed class BatchIngestionServiceTests
{
    private static AppDbContext CreateDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .EnableSensitiveDataLogging()
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task IngestCsvAsync_WhenMissingRequiredColumn_RejectsRowWithClearError()
    {
        var dbName = Guid.NewGuid().ToString("n");
        await using var db = CreateDb(dbName);
        db.Customers.Add(new Customer { Name = "C1" });
        await db.SaveChangesAsync();
        var customerId = await db.Customers.Select(x => x.Id).SingleAsync();

        var csv = $"""
customerId,transactionDate,currency,sourceChannel
{customerId},2026-05-08T12:00:00Z,USD,web
""";

        var service = new BatchIngestionService(db, new TransactionValidationService());
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var response = await service.IngestCsvAsync(stream, CancellationToken.None);

        Assert.Equal(1, response.TotalRows);
        Assert.Equal(0, response.AcceptedRows);
        Assert.Equal(1, response.RejectedRows);
        Assert.Single(response.Rows);
        Assert.Equal("rejected", response.Rows[0].Status);
        Assert.Contains("Missing required column", response.Rows[0].Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IngestCsvAsync_MixedRows_ReportsPerRowErrorsAndAcceptsValidOnes()
    {
        var dbName = Guid.NewGuid().ToString("n");
        await using var db = CreateDb(dbName);

        var customer = new Customer { Name = "C1" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        // existing event to trigger "duplicate vs DB"
        db.IngestionEvents.Add(new IngestionEvent
        {
            CustomerId = customer.Id,
            TransactionDate = new DateTime(2026, 05, 08, 12, 00, 00, DateTimeKind.Utc),
            Amount = 10m,
            Currency = "USD",
            SourceChannel = "web"
        });
        await db.SaveChangesAsync();

        // Row1: duplicate vs DB (within window)
        // Row2: customer not found
        // Row3: accepted
        // Row4: duplicate within file (same as Row3 within window)
        var csv = $"""
customerId,transactionDate,amount,currency,sourceChannel
{customer.Id},2026-05-08T12:00:01Z,10,USD,web
999999,2026-05-08T12:00:00Z,5,USD,web
{customer.Id},2026-05-08T13:00:00Z,20,USD,web
{customer.Id},2026-05-08T13:00:01Z,20,USD,web
""";

        var service = new BatchIngestionService(db, new TransactionValidationService());
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var response = await service.IngestCsvAsync(stream, CancellationToken.None);

        Assert.Equal(4, response.TotalRows);
        Assert.Equal(1, response.AcceptedRows);
        Assert.Equal(3, response.RejectedRows);

        Assert.Equal(4, response.Rows.Count);

        var accepted = response.Rows.Single(r => r.Status == "accepted");
        Assert.NotNull(accepted.IngestionEventId);

        var notFound = response.Rows.Single(r =>
            r.Status == "rejected" &&
            r.Error is not null &&
            r.Error.Contains("not found", StringComparison.OrdinalIgnoreCase));
        Assert.Null(notFound.IngestionEventId);

        var duplicates = response.Rows.Where(r =>
            r.Status == "rejected" &&
            r.Error is not null &&
            r.Error.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Equal(2, duplicates.Count);
    }
}