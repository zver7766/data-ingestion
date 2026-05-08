using DataIngestion.Contracts.Stats;
using DataIngestion.Services.Stats;
using DataIngestion.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DataIngestion.Controllers;

[ApiController]
[Route("api/stats")]
public sealed class StatsController(StatsSummaryService summaryService) : ControllerBase
{
    [HttpGet("summary")]
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