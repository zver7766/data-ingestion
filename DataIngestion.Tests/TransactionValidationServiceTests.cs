using DataIngestion.Contracts.Ingest;
using DataIngestion.Infrastructure;
using DataIngestion.Services.Ingest;

namespace DataIngestion.Tests;

public sealed class TransactionValidationServiceTests
{
    private readonly TransactionValidationService _svc = new();

    [Fact]
    public void ValidateAndNormalize_WhenCustomerIdNotPositive_ReturnsInvalid()
    {
        var req = new IngestTransactionRequest(0, DateTime.UtcNow, 1m, "USD", "web");
        var result = _svc.ValidateAndNormalize(req);
        var invalid = Assert.IsType<TransactionValidationResult.Invalid>(result);
        Assert.Contains("customerId", invalid.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndNormalize_WhenTransactionDateDefault_ReturnsInvalid()
    {
        var req = new IngestTransactionRequest(1, default, 1m, "USD", "web");
        var result = _svc.ValidateAndNormalize(req);
        var invalid = Assert.IsType<TransactionValidationResult.Invalid>(result);
        Assert.Contains("transactionDate", invalid.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndNormalize_WhenTransactionDateTooFarInFuture_ReturnsInvalid()
    {
        var future = DateTime.UtcNow.AddMinutes(IngestionConstants.TransactionDateFutureSkewMinutes + 1);
        var req = new IngestTransactionRequest(1, future, 1m, "USD", "web");
        var result = _svc.ValidateAndNormalize(req);
        var invalid = Assert.IsType<TransactionValidationResult.Invalid>(result);
        Assert.Contains("future", invalid.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndNormalize_WhenAmountNotPositive_ReturnsInvalid()
    {
        var req = new IngestTransactionRequest(1, DateTime.UtcNow, 0m, "USD", "web");
        var result = _svc.ValidateAndNormalize(req);
        var invalid = Assert.IsType<TransactionValidationResult.Invalid>(result);
        Assert.Contains("amount", invalid.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndNormalize_WhenAmountHasTooManyDecimals_ReturnsInvalid()
    {
        var req = new IngestTransactionRequest(1, DateTime.UtcNow, 1.0000001m, "USD", "web");
        var result = _svc.ValidateAndNormalize(req);
        var invalid = Assert.IsType<TransactionValidationResult.Invalid>(result);
        Assert.Contains("decimal", invalid.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndNormalize_WhenCurrencyInvalid_ReturnsInvalid()
    {
        var req = new IngestTransactionRequest(1, DateTime.UtcNow, 1m, "US", "web");
        var result = _svc.ValidateAndNormalize(req);
        var invalid = Assert.IsType<TransactionValidationResult.Invalid>(result);
        Assert.Contains("currency", invalid.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndNormalize_WhenSourceChannelNotAllowed_ReturnsInvalid()
    {
        var req = new IngestTransactionRequest(1, DateTime.UtcNow, 1m, "USD", "unknown");
        var result = _svc.ValidateAndNormalize(req);
        var invalid = Assert.IsType<TransactionValidationResult.Invalid>(result);
        Assert.Contains("sourceChannel", invalid.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndNormalize_WhenValid_NormalizesValues()
    {
        var unspecified = new DateTime(2026, 05, 08, 12, 00, 00, DateTimeKind.Unspecified);
        var req = new IngestTransactionRequest(1, unspecified, 10.123456m, " usd ", " Web ");

        var result = _svc.ValidateAndNormalize(req);
        var valid = Assert.IsType<TransactionValidationResult.Valid>(result);

        Assert.Equal(1, valid.CustomerId);
        Assert.Equal(DateTimeKind.Utc, valid.TransactionDateUtc.Kind);
        Assert.Equal(10.123456m, valid.Amount);
        Assert.Equal("USD", valid.Currency);
        Assert.Equal("web", valid.SourceChannel);
    }
}