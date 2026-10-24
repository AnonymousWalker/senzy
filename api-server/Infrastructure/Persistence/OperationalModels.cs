namespace Senzy.Api.Infrastructure.Persistence;

public sealed class IdempotencyRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActorUserId { get; set; }
    public string Operation { get; set; } = "";
    public string Key { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string ResponseJson { get; set; } = "";
    public Guid? PaymentId { get; set; }
    public Guid? FundingTransferId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = "";
    public int Version { get; set; } = 1;
    public Guid AggregateId { get; set; }
    public Guid? CorrelationId { get; set; }
    public Guid? CausationId { get; set; }
    public string PayloadJson { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public Guid? LockId { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public string? LastError { get; set; }
}

public sealed class OutboxConsumer
{
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class OutboxDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OutboxMessageId { get; set; }
    public string ConsumerName { get; set; } = "";
    public DateTimeOffset? DeliveredAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public Guid? LockId { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public string? LastError { get; set; }
}

public sealed class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid SourceOutboxMessageId { get; set; }
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}

public sealed class WalletControl
{
    public Guid UserId { get; set; }
    public bool IsFrozen { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ReconciliationRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public string Status { get; set; } = "";
    public int UnbalancedTransactions { get; set; }
    public int BalanceMismatches { get; set; }
    public int PaymentMismatches { get; set; }
    public int FundingMismatches { get; set; }
    public long UnexplainedVarianceMinor { get; set; }
}
