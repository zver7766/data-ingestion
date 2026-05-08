using DataIngestion.Contracts.Ingest;
using DataIngestion.Data;
using DataIngestion.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataIngestion.Services.Ingest;

public sealed class TransactionIngestionService(
    AppDbContext db,
    TransactionDuplicateChecker duplicateChecker,
    TransactionValidationService validationService)
{
    public async Task<TransactionIngestionResult> IngestAsync(IngestTransactionRequest request, CancellationToken ct)
    {
        var validation = validationService.ValidateAndNormalize(request);
        if (validation is TransactionValidationResult.Invalid invalid)
        {
            return new TransactionIngestionResult.Invalid(invalid.Message);
        }

        var valid = (TransactionValidationResult.Valid)validation;

        var customerExists = await db.Customers.AsNoTracking().AnyAsync(x => x.Id == valid.CustomerId, ct);
        if (!customerExists)
        {
            return new TransactionIngestionResult.CustomerNotFound(valid.CustomerId);
        }

        var isDuplicate = await duplicateChecker.IsDuplicateAsync(
            customerId: valid.CustomerId,
            transactionDateUtc: valid.TransactionDateUtc,
            amount: valid.Amount,
            currency: valid.Currency,
            sourceChannel: valid.SourceChannel,
            ct: ct);

        if (isDuplicate)
        {
            return new TransactionIngestionResult.Duplicate();
        }

        var entity = new IngestionEvent
        {
            CustomerId = valid.CustomerId,
            TransactionDate = valid.TransactionDateUtc,
            Amount = valid.Amount,
            Currency = valid.Currency,
            SourceChannel = valid.SourceChannel
        };

        db.IngestionEvents.Add(entity);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return new TransactionIngestionResult.Duplicate();
        }

        return new TransactionIngestionResult.Created(entity);
    }
}