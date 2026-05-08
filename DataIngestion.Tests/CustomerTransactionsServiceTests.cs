using DataIngestion.Contracts.Customers;
using DataIngestion.Data;
using DataIngestion.Data.Entities;
using DataIngestion.Infrastructure;
using DataIngestion.Services.Customers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DataIngestion.Tests;

public sealed class CustomerTransactionsServiceTests
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
    public async Task GetAsync_WhenCustomerMissing_Returns404()
    {
        var dbName = Guid.NewGuid().ToString("n");
        await using var db = CreateDb(dbName);

        var svc = new CustomerTransactionsService(db);

        var (resp, err, status) = await svc.GetAsync(123, new CustomerTransactionsQuery(), CancellationToken.None);

        Assert.Null(resp);
        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.Contains("not found", err, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAsync_WhenPageSizeTooLarge_Returns400()
    {
        var dbName = Guid.NewGuid().ToString("n");
        await using var db = CreateDb(dbName);
        db.Customers.Add(new Customer { Name = "C1" });
        await db.SaveChangesAsync();

        var customerId = await db.Customers.Select(x => x.Id).SingleAsync();
        var svc = new CustomerTransactionsService(db);

        var (resp, err, status) = await svc.GetAsync(
            customerId,
            new CustomerTransactionsQuery { PageSize = IngestionConstants.TransactionsMaxPageSize + 1 },
            CancellationToken.None);

        Assert.Null(resp);
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Contains("pageSize", err, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAsync_AppliesCurrencyAndSourceChannelNormalizationAndFilters()
    {
        var dbName = Guid.NewGuid().ToString("n");
        await using var db = CreateDb(dbName);

        var customer = new Customer { Name = "C1" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        db.IngestionEvents.AddRange(
            new IngestionEvent
            {
                CustomerId = customer.Id,
                TransactionDate = new DateTime(2026, 05, 08, 10, 00, 00, DateTimeKind.Utc),
                Amount = 1m,
                Currency = "USD",
                SourceChannel = "web"
            },
            new IngestionEvent
            {
                CustomerId = customer.Id,
                TransactionDate = new DateTime(2026, 05, 08, 11, 00, 00, DateTimeKind.Utc),
                Amount = 2m,
                Currency = "EUR",
                SourceChannel = "web"
            },
            new IngestionEvent
            {
                CustomerId = customer.Id,
                TransactionDate = new DateTime(2026, 05, 08, 12, 00, 00, DateTimeKind.Utc),
                Amount = 3m,
                Currency = "USD",
                SourceChannel = "mobile"
            });
        await db.SaveChangesAsync();

        var svc = new CustomerTransactionsService(db);

        var (resp, err, status) = await svc.GetAsync(
            customer.Id,
            new CustomerTransactionsQuery { Currency = " usd ", SourceChannel = " WEB " },
            CancellationToken.None);

        Assert.Null(err);
        Assert.Null(status);
        Assert.NotNull(resp);
        Assert.Equal(1, resp!.Total);
        Assert.Single(resp.Items);
        Assert.Equal("USD", resp.Items[0].Currency);
        Assert.Equal("web", resp.Items[0].SourceChannel);
    }

    [Fact]
    public async Task GetAsync_OrdersByTransactionDateDescThenIdDesc()
    {
        var dbName = Guid.NewGuid().ToString("n");
        await using var db = CreateDb(dbName);

        var customer = new Customer { Name = "C1" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var sameDate = new DateTime(2026, 05, 08, 12, 00, 00, DateTimeKind.Utc);

        var a = new IngestionEvent
        {
            CustomerId = customer.Id,
            TransactionDate = sameDate,
            Amount = 1m,
            Currency = "USD",
            SourceChannel = "web"
        };
        var b = new IngestionEvent
        {
            CustomerId = customer.Id,
            TransactionDate = sameDate,
            Amount = 2m,
            Currency = "USD",
            SourceChannel = "web"
        };
        db.IngestionEvents.AddRange(a, b);
        await db.SaveChangesAsync();

        var svc = new CustomerTransactionsService(db);
        var (resp, _, _) = await svc.GetAsync(customer.Id, new CustomerTransactionsQuery { PageSize = 10 }, CancellationToken.None);

        Assert.NotNull(resp);
        Assert.Equal(2, resp!.Items.Count);
        Assert.True(resp.Items[0].Id > resp.Items[1].Id);
    }
}