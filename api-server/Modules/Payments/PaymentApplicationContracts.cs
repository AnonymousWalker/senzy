using Senzy.Api.Infrastructure.Persistence.Models;

namespace Senzy.Modules.Payments;

public sealed record PaymentRecipient(Guid UserId, string Handle);

public interface IRecipientDirectory
{
  Task<PaymentRecipient> ResolveAsync(string handle, CancellationToken cancellationToken);
  Task<PaymentRecipient> RequireAsync(Guid userId, CancellationToken cancellationToken);
}

public interface IPaymentFailureInjector
{
  Task BeforeCommitAsync(string operation, CancellationToken cancellationToken);
}

public sealed class NoopPaymentFailureInjector : IPaymentFailureInjector
{
  public Task BeforeCommitAsync(string operation, CancellationToken cancellationToken) =>
      Task.CompletedTask;
}

public sealed class PaymentCommandException(int status, string code, string detail) : Exception(detail)
{
  public int Status { get; } = status;
  public string Code { get; } = code;
}

public sealed record PaymentSendCommand(
    string RecipientHandle,
    long AmountMinor,
    string Currency,
    string Note);

public sealed record PaymentRequestCommand(
    string PayerHandle,
    long AmountMinor,
    string Currency,
    string Note);

public sealed record WalletSnapshot(
    long AvailableMinor,
    long SettledMinor,
    string Currency,
    bool IsSimulated);

public sealed record PaymentResult(
    Guid Id,
    string Kind,
    string Status,
    Guid SenderUserId,
    string SenderHandle,
    Guid RecipientUserId,
    string RecipientHandle,
    long AmountMinor,
    string Currency,
    string Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReversedAt);

public sealed record PaymentRequestResult(
    Guid Id,
    string Status,
    Guid RequesterUserId,
    string RequesterHandle,
    Guid PayerUserId,
    string PayerHandle,
    long AmountMinor,
    string Currency,
    string Note,
    Guid? PaymentId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt);

public sealed record PaymentListResult(IReadOnlyList<PaymentResult> Items);

public sealed record PaymentRequestListResult(IReadOnlyList<PaymentRequestResult> Items);

internal static class PaymentMappings
{
  public static string Value(PaymentKind kind) => kind.ToString().ToLowerInvariant();
  public static string Value(PaymentStatus status) => status.ToString().ToLowerInvariant();
  public static string Value(PaymentRequestStatus status) => status.ToString().ToLowerInvariant();
}
