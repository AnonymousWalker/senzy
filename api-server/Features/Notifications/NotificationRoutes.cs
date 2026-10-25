using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Senzy.Api.Common;
using Senzy.Api.Features.Profiles;
using Senzy.Api.Infrastructure.Persistence;

namespace Senzy.Api.Features.Notifications;

public static class NotificationRoutes
{
  public static void MapNotificationRoutes(this WebApplication app)
  {
    var group = app.MapGroup("/v1/notifications")
        .RequireAuthorization()
        .WithTags("Notifications");

    group.MapGet(
        "/",
        async (int? limit, ClaimsPrincipal actor, SenzyDbContext db, CancellationToken ct) =>
        {
          var actorId = ProfileRoutes.ActorId(actor);
          var items = await db.Notifications.AsNoTracking()
              .Where(notification => notification.UserId == actorId)
              .OrderByDescending(notification => notification.CreatedAt)
              .ThenByDescending(notification => notification.Id)
              .Take(Math.Clamp(limit ?? 50, 1, 100))
              .Select(notification => new NotificationResponse(
                  notification.Id,
                  notification.Kind,
                  notification.Title,
                  notification.Body,
                  notification.CreatedAt,
                  notification.ReadAt))
              .ToListAsync(ct);

          var unread = await db.Notifications.CountAsync(
              notification => notification.UserId == actorId && notification.ReadAt == null,
              ct);

          return Results.Ok(new NotificationListResponse(items, unread));
        })
        .WithName("ListNotifications")
        .Produces<NotificationListResponse>();

    group.MapPost(
        "/{notificationId:guid}/read",
        async (
            Guid notificationId,
            ClaimsPrincipal actor,
            SenzyDbContext db,
            TimeProvider clock,
            CancellationToken ct) =>
        {
          var actorId = ProfileRoutes.ActorId(actor);
          var notification = await db.Notifications.SingleOrDefaultAsync(
              value => value.Id == notificationId && value.UserId == actorId,
              ct);

          if (notification is null)
          {
            return ApiErrors.Problem(404, "notification_missing", "Notification not found.");
          }

          notification.ReadAt ??= clock.GetUtcNow();
          await db.SaveChangesAsync(ct);
          return Results.NoContent();
        })
        .WithName("MarkNotificationRead")
        .Produces(204)
        .ProducesProblem(404);
  }
}
