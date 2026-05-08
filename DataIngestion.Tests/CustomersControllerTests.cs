using DataIngestion.Controllers;
using DataIngestion.Contracts.Customers;
using DataIngestion.Data;
using DataIngestion.Data.Entities;
using DataIngestion.Services.Customers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataIngestion.Tests;

public sealed class CustomersControllerTests
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
    public async Task Create_WhenNameMissing_Returns400ProblemDetails()
    {
        var dbName = Guid.NewGuid().ToString("n");
        await using var db = CreateDb(dbName);

        var controller = new CustomersController(db, new CustomerTransactionsService(db));

        var result = await controller.Create(new CreateCustomerRequest("   "), CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(400, obj.StatusCode);

        var pd = Assert.IsType<ProblemDetails>(obj.Value);
        Assert.Equal(400, pd.Status);
        Assert.Equal("Name is required.", pd.Detail);
        Assert.True(pd.Extensions.TryGetValue("code", out var code));
        Assert.Equal("validation_error", code);
    }

    [Fact]
    public async Task Create_WhenValid_Returns201AndTrimsName()
    {
        var dbName = Guid.NewGuid().ToString("n");
        await using var db = CreateDb(dbName);

        var controller = new CustomersController(db, new CustomerTransactionsService(db));

        var result = await controller.Create(new CreateCustomerRequest("  John  "), CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal(201, created.StatusCode);
        Assert.NotNull(created.Location);

        var customer = Assert.IsType<Customer>(created.Value);
        Assert.Equal("John", customer.Name);
        Assert.Contains($"/api/customers/{customer.Id}", created.Location!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTransactions_WhenCustomerMissing_Returns404ProblemDetails()
    {
        var dbName = Guid.NewGuid().ToString("n");
        await using var db = CreateDb(dbName);

        var controller = new CustomersController(db, new CustomerTransactionsService(db));

        var result = await controller.GetTransactions(
            id: 999,
            query: new CustomerTransactionsQuery(),
            ct: CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(404, obj.StatusCode);

        var pd = Assert.IsType<ProblemDetails>(obj.Value);
        Assert.Equal(404, pd.Status);
        Assert.True(pd.Extensions.TryGetValue("code", out var code));
        Assert.Equal("customer_not_found", code);
    }

    [Fact]
    public async Task GetTransactions_WhenQueryInvalid_Returns400ProblemDetails()
    {
        var dbName = Guid.NewGuid().ToString("n");
        await using var db = CreateDb(dbName);

        db.Customers.Add(new Customer { Name = "C1" });
        await db.SaveChangesAsync();

        var customerId = await db.Customers.Select(x => x.Id).SingleAsync();

        var controller = new CustomersController(db, new CustomerTransactionsService(db));

        var result = await controller.GetTransactions(
            id: customerId,
            query: new CustomerTransactionsQuery { PageSize = 999999 },
            ct: CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(400, obj.StatusCode);

        var pd = Assert.IsType<ProblemDetails>(obj.Value);
        Assert.Equal(400, pd.Status);
        Assert.True(pd.Detail?.Contains("pageSize must be between", StringComparison.OrdinalIgnoreCase));
        Assert.True(pd.Extensions.TryGetValue("code", out var code));
        Assert.Equal("validation_error", code);
    }

    [Fact]
    public async Task GetTransactions_WhenValid_Returns200WithMostRecentFirst()
    {
        var dbName = Guid.NewGuid().ToString("n");
        await using var db = CreateDb(dbName);

        var customer = new Customer { Name = "C1" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var t1 = new IngestionEvent
        {
            CustomerId = customer.Id,
            TransactionDate = new DateTime(2026, 05, 08, 12, 00, 01, DateTimeKind.Utc),
            Amount = 10.5m,
            Currency = "USD",
            SourceChannel = "web"
        };
        var t2 = new IngestionEvent
        {
            CustomerId = customer.Id,
            TransactionDate = new DateTime(2026, 05, 08, 12, 00, 02, DateTimeKind.Utc),
            Amount = 20m,
            Currency = "USD",
            SourceChannel = "web"
        };
        db.IngestionEvents.AddRange(t1, t2);
        await db.SaveChangesAsync();

        var controller = new CustomersController(db, new CustomerTransactionsService(db));

        var result = await controller.GetTransactions(
            id: customer.Id,
            query: new CustomerTransactionsQuery { Page = 1, PageSize = 50 },
            ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<CustomerTransactionsResponse>(ok.Value);
        Assert.Equal(customer.Id, response.CustomerId);
        Assert.Equal(2, response.Total);
        Assert.Equal(2, response.Items.Count);
        Assert.Equal(t2.TransactionDate, response.Items[0].TransactionDateUtc);
        Assert.Equal(t1.TransactionDate, response.Items[1].TransactionDateUtc);
    }
}