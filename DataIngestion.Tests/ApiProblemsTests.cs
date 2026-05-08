using DataIngestion.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DataIngestion.Tests;

public sealed class ApiProblemsTests
{
    private sealed class TestController : ControllerBase;

    [Fact]
    public void Problem_SetsStatusDetailTitleAndCodeExtension()
    {
        var controller = new TestController();

        var result = controller.Problem(
            statusCode: 400,
            code: "validation_error",
            detail: "bad request detail");

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, obj.StatusCode);

        var pd = Assert.IsType<ProblemDetails>(obj.Value);
        Assert.Equal(400, pd.Status);
        Assert.Equal("Bad Request", pd.Title);
        Assert.Equal("bad request detail", pd.Detail);
        Assert.True(pd.Extensions.TryGetValue("code", out var code));
        Assert.Equal("validation_error", code);
    }
}