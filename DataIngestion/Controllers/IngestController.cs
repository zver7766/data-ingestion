using DataIngestion.Data.Entities;
using DataIngestion.Contracts.Ingest;
using DataIngestion.Services.Ingest;
using Microsoft.AspNetCore.Mvc;

namespace DataIngestion.Controllers;

[ApiController]
[Route("api/ingest")]
public sealed class IngestController(TransactionIngestionService ingestionService) : ControllerBase
{
    [HttpPost("transaction")]
    public async Task<ActionResult<IngestionEvent>> IngestTransaction([FromBody] IngestTransactionRequest request, CancellationToken ct)
    {
        var result = await ingestionService.IngestAsync(request, ct);
        return result switch
        {
            TransactionIngestionResult.Created created =>
                Created($"/api/ingest/transaction/{created.Entity.Id}", created.Entity),

            TransactionIngestionResult.Duplicate =>
                Conflict(new { message = "Duplicate transaction." }),

            TransactionIngestionResult.CustomerNotFound notFound =>
                NotFound(new { message = $"Customer {notFound.CustomerId} not found." }),

            TransactionIngestionResult.Invalid invalid =>
                BadRequest(new { message = invalid.Message }),

            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost("batch")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<IngestBatchResponse>> IngestBatch([FromForm] IFormFile file, [FromServices] BatchIngestionService batchService, CancellationToken ct)
    {
        if (file.Length == 0)
        {
            return BadRequest(new { message = "File is empty." });
        }

        await using var stream = file.OpenReadStream();
        var response = await batchService.IngestCsvAsync(stream, ct);
        return Ok(response);
    }
}