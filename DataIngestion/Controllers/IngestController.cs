using DataIngestion.Data.Entities;
using DataIngestion.Contracts.Ingest;
using DataIngestion.Services.Ingest;
using DataIngestion.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DataIngestion.Controllers;

[ApiController]
[Route("api/ingest")]
public sealed class IngestController(TransactionIngestionService ingestionService) : ControllerBase
{
    /// <summary>
    /// Ingest a single transaction.
    /// </summary>
    /// <remarks>
    /// Applies the same validation and duplicate rules as batch ingestion.
    /// Returns ProblemDetails on errors.
    /// </remarks>
    /// <response code="201">Transaction accepted and stored.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="404">Customer not found.</response>
    /// <response code="409">Duplicate transaction.</response>
    [HttpPost("transaction")]
    [ProducesResponseType(typeof(IngestionEvent), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IngestionEvent>> IngestTransaction([FromBody] IngestTransactionRequest request, CancellationToken ct)
    {
        var result = await ingestionService.IngestAsync(request, ct);
        return result switch
        {
            TransactionIngestionResult.Created created =>
                Created($"/api/ingest/transaction/{created.Entity.Id}", created.Entity),

            TransactionIngestionResult.Duplicate =>
                this.Problem(StatusCodes.Status409Conflict, "duplicate_transaction", "Duplicate transaction."),

            TransactionIngestionResult.CustomerNotFound notFound =>
                this.Problem(StatusCodes.Status404NotFound, "customer_not_found", $"Customer {notFound.CustomerId} not found."),

            TransactionIngestionResult.Invalid invalid =>
                this.Problem(StatusCodes.Status400BadRequest, "validation_error", invalid.Message),

            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>
    /// Ingest transactions from a CSV file upload.
    /// </summary>
    /// <remarks>
    /// Upload a CSV using <c>multipart/form-data</c> with field name <c>file</c>.
    /// Returns per-row accept/reject results and error details.
    /// </remarks>
    /// <response code="200">Batch processed (accepted/rejected counts included).</response>
    /// <response code="400">File missing/empty or request invalid.</response>
    [HttpPost("batch")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(IngestBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IngestBatchResponse>> IngestBatch([FromForm] IFormFile file, [FromServices] BatchIngestionService batchService, CancellationToken ct)
    {
        if (file.Length == 0)
        {
            return this.Problem(StatusCodes.Status400BadRequest, "empty_file", "File is empty.");
        }

        await using var stream = file.OpenReadStream();
        var response = await batchService.IngestCsvAsync(stream, ct);
        return Ok(response);
    }
}