using DataIngestion.Contracts.Stats;

namespace DataIngestion.Services.Stats;

public interface IStatsSummaryService
{
    Task<StatsSummaryResponse> GetSummaryAsync(CancellationToken ct);
}