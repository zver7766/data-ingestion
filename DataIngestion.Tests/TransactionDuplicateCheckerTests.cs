using DataIngestion.Data;
using DataIngestion.Data.Entities;
using DataIngestion.Infrastructure;
using DataIngestion.Services.Ingest;
using Microsoft.EntityFrameworkCore;

namespace DataIngestion.Tests;

public sealed class TransactionDuplicateCheckerTests
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
    public async Task IsDuplicateAsync_WhenMatchWithinWindow_ReturnsTrue()
    {
        var dbName = Guid.NewGuid().ToString("n");
        await using var db = CreateDb(dbName);

        var existing = new IngestionEvent
        {
            CustomerId = 10,
            TransactionDate = new DateTime(2026, 05, 08, 12, 00, 00, DateTimeKind.Utc),
            Amount = 10m,
            Currency = "USD",
            SourceChannel = "web"
        };
        db.IngestionEvents.Add(existing);
        await db.SaveChangesAsync();

        var checker = new TransactionDuplicateChecker(db);

        var probeDate = existing.TransactionDate.AddSeconds(IngestionConstants.TransactionDuplicateWindowSeconds - 1);
        var isDup = await checker.IsDuplicateAsync(10, probeDate, 10m, "USD", "web", CancellationToken.None);

        Assert.True(isDup);
    }

    [Fact]
    public async Task IsDuplicateAsync_WhenOutsideWindow_ReturnsFalse()
    {
        var dbName = Guid.NewGuid().ToString("n");
        await using var db = CreateDb(dbName);

        var existing = new IngestionEvent
        {
            CustomerId = 10,
            TransactionDate = new DateTime(2026, 05, 08, 12, 00, 00, DateTimeKind.Utc),
            Amount = 10m,
            Currency = "USD",
            SourceChannel = "web"
        };
        db.IngestionEvents.Add(existing);
        await db.SaveChangesAsync();

        var checker = new TransactionDuplicateChecker(db);

        var probeDate = existing.TransactionDate.AddSeconds(IngestionConstants.TransactionDuplicateWindowSeconds + 1);
        var isDup = await checker.IsDuplicateAsync(10, probeDate, 10m, "USD", "web", CancellationToken.None);

        Assert.False(isDup);
    }

    [Fact]
    public async Task IsDuplicateAsync_WhenDifferentKeyFields_ReturnsFalse()
    {
        var dbName = Guid.NewGuid().ToString("n");
        await using var db = CreateDb(dbName);

        var existing = new IngestionEvent
        {
            CustomerId = 10,
            TransactionDate = new DateTime(2026, 05, 08, 12, 00, 00, DateTimeKind.Utc),
            Amount = 10m,
            Currency = "USD",
            SourceChannel = "web"
        };
        db.IngestionEvents.Add(existing);
        await db.SaveChangesAsync();

        var checker = new TransactionDuplicateChecker(db);

        var probeDate = existing.TransactionDate;

        Assert.False(await checker.IsDuplicateAsync(11, probeDate, 10m, "USD", "web", CancellationToken.None)); // customer
        Assert.False(await checker.IsDuplicateAsync(10, probeDate, 11m, "USD", "web", CancellationToken.None)); // amount
        Assert.False(await checker.IsDuplicateAsync(10, probeDate, 10m, "EUR", "web", CancellationToken.None)); // currency
        Assert.False(await checker.IsDuplicateAsync(10, probeDate, 10m, "USD", "mobile", CancellationToken.None)); // sourceChannel
    }
}