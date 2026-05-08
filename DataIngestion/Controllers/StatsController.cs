using DataIngestion.Contracts.Stats;
using DataIngestion.Services.Stats;
using Microsoft.AspNetCore.Mvc;

namespace DataIngestion.Controllers;

[ApiController]
[Route("api/stats")]
public sealed class StatsController(StatsSummaryService summaryService) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<StatsSummaryResponse>> Summary(CancellationToken ct)
    {
        var response = await summaryService.GetSummaryAsync(ct);
        
        return Ok(response);
    }
}