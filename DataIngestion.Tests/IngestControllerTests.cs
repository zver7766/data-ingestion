using DataIngestion.Controllers;
using DataIngestion.Contracts.Ingest;
using DataIngestion.Data;
using DataIngestion.Data.Entities;
using DataIngestion.Services.Ingest;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Text;

namespace DataIngestion.Tests;

public sealed class IngestControllerTests
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
    public async Task IngestTransaction_WhenCreated_Returns201WithEntity()
    {
        var entity = new IngestionEvent
        {
            Id = 123,
            CustomerId = 1,
            TransactionDate = DateTime.UtcNow,
            Amount = 10m,
            Currency = "USD",
            SourceChannel = "web"
        };

        var svc = new Mock<ITransactionIngestionService>(MockBehavior.Strict);
        svc.Setup(x => x.IngestAsync(It.IsAny<IngestTransactionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransactionIngestionResult.Created(entity));

        var controller = new IngestController(svc.Object);

        var result = await controller.IngestTransaction(
            new IngestTransactionRequest(1, entity.TransactionDate, 10m, "USD", "web"),
            CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal(201, created.StatusCode);
        Assert.Equal($"/api/ingest/transaction/{entity.Id}", created.Location);
        Assert.Same(entity, created.Value);
    }

    [Fact]
    public async Task IngestTransaction_WhenDuplicate_Returns409ProblemDetailsWithCode()
    {
        var svc = new Mock<ITransactionIngestionService>(MockBehavior.Strict);
        svc.Setup(x => x.IngestAsync(It.IsAny<IngestTransactionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransactionIngestionResult.Duplicate());

        var controller = new IngestController(svc.Object);

        var result = await controller.IngestTransaction(
            new IngestTransactionRequest(1, DateTime.UtcNow, 10m, "USD", "web"),
            CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(409, obj.StatusCode);
        var pd = Assert.IsType<ProblemDetails>(obj.Value);
        Assert.Equal(409, pd.Status);
        Assert.True(pd.Extensions.TryGetValue("code", out var code));
        Assert.Equal("duplicate_transaction", code);
    }

    [Fact]
    public async Task IngestTransaction_WhenCustomerNotFound_Returns404ProblemDetailsWithCode()
    {
        var svc = new Mock<ITransactionIngestionService>(MockBehavior.Strict);
        svc.Setup(x => x.IngestAsync(It.IsAny<IngestTransactionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransactionIngestionResult.CustomerNotFound(42));

        var controller = new IngestController(svc.Object);

        var result = await controller.IngestTransaction(
            new IngestTransactionRequest(42, DateTime.UtcNow, 10m, "USD", "web"),
            CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(404, obj.StatusCode);
        var pd = Assert.IsType<ProblemDetails>(obj.Value);
        Assert.Equal(404, pd.Status);
        Assert.True(pd.Detail?.Contains("Customer 42 not found", StringComparison.OrdinalIgnoreCase));
        Assert.True(pd.Extensions.TryGetValue("code", out var code));
        Assert.Equal("customer_not_found", code);
    }

    [Fact]
    public async Task IngestTransaction_WhenInvalid_Returns400ProblemDetailsWithCode()
    {
        var svc = new Mock<ITransactionIngestionService>(MockBehavior.Strict);
        svc.Setup(x => x.IngestAsync(It.IsAny<IngestTransactionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransactionIngestionResult.Invalid("bad input"));

        var controller = new IngestController(svc.Object);

        var result = await controller.IngestTransaction(
            new IngestTransactionRequest(1, DateTime.UtcNow, 10m, "USD", "web"),
            CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(400, obj.StatusCode);
        var pd = Assert.IsType<ProblemDetails>(obj.Value);
        Assert.Equal(400, pd.Status);
        Assert.Equal("bad input", pd.Detail);
        Assert.True(pd.Extensions.TryGetValue("code", out var code));
        Assert.Equal("validation_error", code);
    }

    [Fact]
    public async Task IngestBatch_WhenEmptyFile_Returns400ProblemDetailsWithCode()
    {
        var svc = new Mock<ITransactionIngestionService>(MockBehavior.Strict);
        var controller = new IngestController(svc.Object);

        var emptyStream = new MemoryStream(Array.Empty<byte>());
        var file = new FormFile(emptyStream, 0, 0, "file", "empty.csv");

        // Batch service isn't used in this path; can be any valid instance.
        await using var db = CreateDb(Guid.NewGuid().ToString("n"));
        var batch = new BatchIngestionService(db, new TransactionValidationService());

        var result = await controller.IngestBatch(file, batch, CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(400, obj.StatusCode);
        var pd = Assert.IsType<ProblemDetails>(obj.Value);
        Assert.Equal(400, pd.Status);
        Assert.True(pd.Extensions.TryGetValue("code", out var code));
        Assert.Equal("empty_file", code);
    }

    [Fact]
    public async Task IngestBatch_WhenValidCsv_Returns200WithAcceptedRows()
    {
        var ingestionSvc = new Mock<ITransactionIngestionService>(MockBehavior.Strict);
        var controller = new IngestController(ingestionSvc.Object);

        var dbName = Guid.NewGuid().ToString("n");
        await using var db = CreateDb(dbName);
        db.Customers.Add(new Customer { Name = "C1" });
        await db.SaveChangesAsync();
        var customerId = await db.Customers.Select(x => x.Id).SingleAsync();

        var csv = $"""
customerId,transactionDate,amount,currency,sourceChannel
{customerId},2026-05-08T12:00:00Z,10.5,USD,web
""";

        var bytes = Encoding.UTF8.GetBytes(csv);
        var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, "file", "batch.csv")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };

        var batch = new BatchIngestionService(db, new TransactionValidationService());

        var result = await controller.IngestBatch(file, batch, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<IngestBatchResponse>(ok.Value);
        Assert.Equal(1, response.TotalRows);
        Assert.Equal(1, response.AcceptedRows);
        Assert.Equal(0, response.RejectedRows);
        Assert.Single(response.Rows);
        Assert.Equal("accepted", response.Rows[0].Status);
        Assert.NotNull(response.Rows[0].IngestionEventId);
    }
}

