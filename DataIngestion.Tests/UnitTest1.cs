using DataIngestion.Contracts.Stats;
using DataIngestion.Controllers;
using DataIngestion.Services.Stats;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace DataIngestion.Tests;

public sealed class StatsControllerTests
{
    [Fact]
    public async Task Summary_WhenServiceReturns_ReturnsOkWithBody()
    {
        var expected = new StatsSummaryResponse(
            Totals: new StatsTotals(1, 2, 3m),
            Recent: new StatsRecentActivity(4, 5, 6m, 7m),
            Range: new StatsRange(null, null),
            ByCurrency: [],
            BySourceChannel: [],
            TopCustomersByAmount: []);

        var svc = new Mock<IStatsSummaryService>(MockBehavior.Strict);
        svc.Setup(x => x.GetSummaryAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var controller = new StatsController(svc.Object);

        var result = await controller.Summary(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, ok.StatusCode);
        Assert.Same(expected, ok.Value);
    }

    [Fact]
    public async Task Summary_WhenServiceThrows_Returns500ProblemDetailsWithCode()
    {
        var svc = new Mock<IStatsSummaryService>(MockBehavior.Strict);
        svc.Setup(x => x.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var controller = new StatsController(svc.Object);

        var result = await controller.Summary(CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, obj.StatusCode);

        var pd = Assert.IsType<ProblemDetails>(obj.Value);
        Assert.Equal(500, pd.Status);
        Assert.Equal("Failed to compute stats summary.", pd.Detail);
        Assert.True(pd.Extensions.TryGetValue("code", out var code));
        Assert.Equal("stats_error", code);
    }

    [Fact]
    public async Task Summary_WhenCancellationRequested_RethrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var svc = new Mock<IStatsSummaryService>(MockBehavior.Strict);
        svc.Setup(x => x.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var controller = new StatsController(svc.Object);

        await Assert.ThrowsAsync<OperationCanceledException>(() => controller.Summary(cts.Token));
    }
}