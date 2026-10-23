using Senzy.Api.Common;

namespace Senzy.Api.Features.Telemetry;

public sealed record TelemetryRequest(string Event, string Screen);

public static class TelemetryRoutes
{
    private static readonly string[] Events = ["signup_completed", "login_succeeded", "profile_saved", "activity_viewed", "client_error"];
    private static readonly string[] Screens = ["auth", "activity", "funding", "profile", "app"];
    public static void MapTelemetryRoutes(this WebApplication app) =>
        app.MapPost("/v1/telemetry", (TelemetryRequest request, IConfiguration config, ILoggerFactory logs) =>
        {
            if (!config.GetValue("Features:Telemetry", true)) return Results.NoContent();
            if (!Events.Contains(request.Event) || !Screens.Contains(request.Screen))
                return ApiErrors.Problem(400, "invalid_event", "Unknown telemetry event or screen.");
            logs.CreateLogger("Senzy.ClientTelemetry").LogInformation("Client event {Event} on {Screen}", request.Event, request.Screen);
            return Results.Accepted();
        }).RequireAuthorization().RequireRateLimiting("telemetry").WithName("RecordTelemetry").WithTags("Telemetry")
            .Produces(202).Produces(204).ProducesProblem(400);
}
