using Microsoft.EntityFrameworkCore;
using Senzy.Api.Features.Notifications;
using Senzy.Api.Infrastructure.Persistence;

namespace Senzy.Api.Infrastructure.Outbox;

public sealed class OutboxDispatcher(
    SenzyDbContext db,
    TimeProvider clock,
    NotificationEventHandler notifications,
    ILogger<OutboxDispatcher> logger)
{
  private static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);

  public async Task<IReadOnlyList<Guid>> ClaimAsync(int batchSize, CancellationToken ct)
  {
    var now = clock.GetUtcNow();
    var lockId = Guid.NewGuid();
    await using var transaction = await db.Database.BeginTransactionAsync(ct);

    await db.Database.ExecuteSqlInterpolatedAsync(
        $"""
        INSERT INTO "OutboxConsumers" ("Name", "CreatedAt")
        VALUES ({NotificationEventHandler.ConsumerName}, {now})
        ON CONFLICT ("Name") DO NOTHING
        """,
        ct);

    await db.Database.ExecuteSqlInterpolatedAsync(
        $"""
        INSERT INTO "OutboxDeliveries" ("Id", "OutboxMessageId", "ConsumerName", "AttemptCount")
        SELECT gen_random_uuid(), message."Id", {NotificationEventHandler.ConsumerName}, 0
        FROM "OutboxMessages" message
        WHERE NOT EXISTS (
            SELECT 1 FROM "OutboxDeliveries" delivery
            WHERE delivery."OutboxMessageId" = message."Id"
              AND delivery."ConsumerName" = {NotificationEventHandler.ConsumerName})
        ON CONFLICT ("OutboxMessageId", "ConsumerName") DO NOTHING
        """,
        ct);

    var deliveries = await db.OutboxDeliveries.FromSqlInterpolated(
        $$"""
        SELECT delivery.*
        FROM "OutboxDeliveries" delivery
        WHERE delivery."ConsumerName" = {{NotificationEventHandler.ConsumerName}}
          AND delivery."DeliveredAt" IS NULL
          AND (delivery."NextAttemptAt" IS NULL OR delivery."NextAttemptAt" <= {{now}})
          AND (delivery."LockedUntil" IS NULL OR delivery."LockedUntil" <= {{now}})
        ORDER BY delivery."Id"
        LIMIT {{Math.Clamp(batchSize, 1, 100)}}
        FOR UPDATE SKIP LOCKED
        """)
        .ToListAsync(ct);

    foreach (var delivery in deliveries)
    {
      delivery.LockId = lockId;
      delivery.LockedUntil = now.Add(Lease);
    }

    await db.SaveChangesAsync(ct);
    await transaction.CommitAsync(ct);
    return deliveries.Select(delivery => delivery.Id).ToList();
  }

  public async Task DispatchAsync(Guid deliveryId, CancellationToken ct)
  {
    var now = clock.GetUtcNow();
    await using var transaction = await db.Database.BeginTransactionAsync(ct);

    var delivery = await db.OutboxDeliveries.FromSqlInterpolated(
        $"""SELECT * FROM "OutboxDeliveries" WHERE "Id" = {deliveryId} FOR UPDATE""")
        .SingleOrDefaultAsync(ct);

    if (delivery is null)
    {
      // Keep direct/manual dispatch compatible with callers that identify
      // the immutable event rather than its consumer delivery row.
      var eventMessage = await db.OutboxMessages.AsNoTracking()
          .SingleOrDefaultAsync(value => value.Id == deliveryId, ct);

      if (eventMessage is null)
      {
        return;
      }

      await db.Database.ExecuteSqlInterpolatedAsync(
          $"""
          INSERT INTO "OutboxConsumers" ("Name", "CreatedAt")
          VALUES ({NotificationEventHandler.ConsumerName}, {now})
          ON CONFLICT ("Name") DO NOTHING
          """,
          ct);

      delivery = await db.OutboxDeliveries.SingleOrDefaultAsync(
          value => value.OutboxMessageId == deliveryId
              && value.ConsumerName == NotificationEventHandler.ConsumerName,
          ct);

      if (delivery is null)
      {
        delivery = new OutboxDelivery
        {
          OutboxMessageId = deliveryId,
          ConsumerName = NotificationEventHandler.ConsumerName
        };
        db.OutboxDeliveries.Add(delivery);
        await db.SaveChangesAsync(ct);
      }
    }

    if (delivery.DeliveredAt is not null)
    {
      return;
    }

    var message = await db.OutboxMessages.AsNoTracking()
        .SingleOrDefaultAsync(value => value.Id == delivery.OutboxMessageId, ct);

    if (message is null)
    {
      delivery.DeliveredAt = now;
      delivery.LockId = null;
      delivery.LockedUntil = null;
      await db.SaveChangesAsync(ct);
      await transaction.CommitAsync(ct);
      return;
    }

    try
    {
      delivery.AttemptCount++;
      await notifications.HandleAsync(message, now, ct);
      delivery.DeliveredAt = now;
      delivery.LockId = null;
      delivery.LockedUntil = null;
      delivery.NextAttemptAt = null;
      delivery.LastError = null;
      await db.SaveChangesAsync(ct);
      await transaction.CommitAsync(ct);
    }
    catch (Exception error) when (error is not OperationCanceledException)
    {
      logger.LogWarning(error, "Outbox delivery failed for {DeliveryId} ({MessageId})", delivery.Id, message.Id);
      delivery.LockId = null;
      delivery.LockedUntil = null;
      delivery.NextAttemptAt = now.AddSeconds(Math.Min(300, Math.Pow(2, Math.Min(delivery.AttemptCount, 8))));
      delivery.LastError = error.Message.Length <= 500 ? error.Message : error.Message[..500];
      await db.SaveChangesAsync(ct);
      await transaction.CommitAsync(ct);
    }
  }
}
