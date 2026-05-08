using DataIngestion.Contracts.Stats;
using DataIngestion.Services.Stats;
using DataIngestion.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DataIngestion.Controllers;

[ApiController]
[Route("api/stats")]
public sealed class StatsController(StatsSummaryService summaryService) : ControllerBase
{
    /// <summary>
    /// Get aggregate ingestion statistics.
    /// </summary>
    /// <remarks>
    /// Returns totals, min/max transaction date, recent activity, breakdowns by currency and source channel,
    /// and top customers by total amount.
    /// </remarks>
    /// <response code="200">Stats summary returned.</response>
    /// <response code="500">Unexpected error while computing stats.</response>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(StatsSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<StatsSummaryResponse>> Summary(CancellationToken ct)
    {
        try
        {
            var response = await summaryService.GetSummaryAsync(ct);
            return Ok(response);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return this.Problem(StatusCodes.Status500InternalServerError, "stats_error", "Failed to compute stats summary.");
        }
    }
}