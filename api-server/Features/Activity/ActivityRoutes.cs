using System.Security.Claims;
using Senzy.Api.Common;
using Senzy.Api.Features.Profiles;

namespace Senzy.Api.Features.Activity;

public sealed record ActivityItem(
    string Id,
    string Title,
    string Description,
    string Kind,
    long AmountMinor,
    string Currency,
    DateTimeOffset OccurredAt);

public sealed record ActivityResponse(Guid UserId, bool IsFixture, IReadOnlyList<ActivityItem> Items);

public static class ActivityRoutes
{
  public static void MapActivityRoutes(this WebApplication app)
  {
    app.MapGet(
        "/v1/activity",
        (ClaimsPrincipal actor, IConfiguration config) =>
        {
          if (!config.GetValue("Features:Activity", true))
          {
            return ApiErrors.Problem(404, "feature_disabled", "Activity is not available.");
          }

          var userId = ProfileRoutes.ActorId(actor);

          // Deliberately synthetic, scoped to the authenticated user, never ledger data.
          ActivityItem[] items =
          [
            new(
                $"{userId}:sample-1",
                "Lunch with friends",
                "Example payment · no money moved",
                "sent",
                1850,
                "USD",
                new(2026, 10, 1, 12, 30, 0, TimeSpan.Zero)),
            new(
                $"{userId}:sample-2",
                "Weekend plans",
                "Example payment · no money moved",
                "received",
                3200,
                "USD",
                new(2026, 9, 30, 17, 0, 0, TimeSpan.Zero))
          ];

          return Results.Ok(new ActivityResponse(userId, true, items));
        })
        .RequireAuthorization()
        .WithName("GetActivity")
        .WithTags("Activity")
        .Produces<ActivityResponse>()
        .ProducesProblem(404);
  }
}
