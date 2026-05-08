using DataIngestion.Contracts.Ingest;
using DataIngestion.Infrastructure;

namespace DataIngestion.Services.Ingest;

public sealed class TransactionValidationService
{
    public TransactionValidationResult ValidateAndNormalize(IngestTransactionRequest request)
    {
        if (request.CustomerId <= 0)
        {
            return new TransactionValidationResult.Invalid("customerId must be a positive integer.");
        }

        if (request.TransactionDate == default)
        {
            return new TransactionValidationResult.Invalid("transactionDate is required.");
        }

        var txDate = request.TransactionDate.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(request.TransactionDate, DateTimeKind.Utc)
            : request.TransactionDate.ToUniversalTime();

        if (txDate > DateTime.UtcNow.AddMinutes(IngestionConstants.TransactionDateFutureSkewMinutes))
        {
            return new TransactionValidationResult.Invalid("transactionDate cannot be in the future.");
        }

        if (request.Amount is <= IngestionConstants.MinTransactionAmount or > IngestionConstants.MaxTransactionAmount)
        {
            return new TransactionValidationResult.Invalid("amount must be > 0 and reasonably bounded.");
        }

        var amount = request.Amount;
        if (decimal.Round(amount, IngestionConstants.MaxAmountDecimalPlaces) != amount)
        {
            return new TransactionValidationResult.Invalid("amount must have at most 6 decimal places.");
        }

        var currency = (request.Currency ?? string.Empty).Trim().ToUpperInvariant();
        if (currency.Length != IngestionConstants.CurrencyCodeLength || currency.Any(c => c is < 'A' or > 'Z'))
        {
            return new TransactionValidationResult.Invalid("currency must be a 3-letter ISO code (e.g. USD).");
        }

        var sourceChannel = (request.SourceChannel ?? string.Empty).Trim().ToLowerInvariant();
        if (sourceChannel.Length is < IngestionConstants.SourceChannelMinLength or > IngestionConstants.SourceChannelMaxLength)
        {
            return new TransactionValidationResult.Invalid("sourceChannel must be between 2 and 32 characters.");
        }

        if (!IngestionConstants.AllowedSourceChannels.Contains(sourceChannel))
        {
            return new TransactionValidationResult.Invalid("sourceChannel must be one of: web, mobile, pos, api, batch.");
        }

        return new TransactionValidationResult.Valid(
            CustomerId: request.CustomerId,
            TransactionDateUtc: txDate,
            Amount: amount,
            Currency: currency,
            SourceChannel: sourceChannel);
    }
}