namespace Senzy.Api.Infrastructure.Persistence.Models;

public enum PaymentKind
{
  Send,
  Request
}

public enum PaymentStatus
{
  Pending,
  Posted,
  Failed,
  Reversed
}

public enum PaymentRequestStatus
{
  Pending,
  Paid,
  Declined,
  Cancelled
}

public sealed class Payment
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid SenderUserId { get; set; }
  public Guid RecipientUserId { get; set; }
  public Guid? PaymentRequestId { get; set; }
  public Guid PostedLedgerTransactionId { get; set; }
  public Guid? ReversalLedgerTransactionId { get; set; }
  public PaymentKind Kind { get; set; }
  public PaymentStatus Status { get; set; }
  public long AmountMinor { get; set; }
  public string Currency { get; set; } = "USD";
  public string Note { get; set; } = "";
  public DateTimeOffset CreatedAt { get; set; }
  public DateTimeOffset? ReversedAt { get; set; }
}

public sealed class PaymentRequest
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid RequesterUserId { get; set; }
  public Guid PayerUserId { get; set; }
  public Guid? PaymentId { get; set; }
  public PaymentRequestStatus Status { get; set; }
  public long AmountMinor { get; set; }
  public string Currency { get; set; } = "USD";
  public string Note { get; set; } = "";
  public DateTimeOffset CreatedAt { get; set; }
  public DateTimeOffset? ResolvedAt { get; set; }
}
