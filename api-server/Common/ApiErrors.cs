using System.Diagnostics;

namespace Senzy.Api.Common;

public static class ApiErrors
{
    public static IResult Problem(int status, string code, string detail) => Results.Problem(
        statusCode: status, detail: detail, extensions: new Dictionary<string, object?> { ["code"] = code });

    public static void Customize(ProblemDetailsContext context)
    {
        context.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;
        context.ProblemDetails.Extensions.TryAdd("code", context.ProblemDetails.Status switch
        {
            400 => "invalid_request", 401 => "unauthenticated", 403 => "forbidden", 404 => "not_found",
            409 => "conflict", 429 => "rate_limited", 503 => "service_unavailable", _ => "server_error"
        });
    }
}
