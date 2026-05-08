using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace DataIngestion.Infrastructure;

public static class ApiProblems
{
    public static ObjectResult Problem(
        this ControllerBase controller,
        int statusCode,
        string code,
        string detail,
        string? title = null,
        string? type = null)
    {
        var pd = new ProblemDetails
        {
            Status = statusCode,
            Title = title ?? ReasonPhrases.GetReasonPhrase(statusCode),
            Detail = detail,
            Type = type
        };

        pd.Extensions["code"] = code;

        return controller.StatusCode(statusCode, pd);
    }
}

