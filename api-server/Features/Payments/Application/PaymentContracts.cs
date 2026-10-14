using Senzy.Api.Features.Payments.Domain;

namespace Senzy.Api.Features.Payments.Application;

public sealed record SendPaymentRequest(string RecipientHandle, long AmountMinor, string Currency, string Note);
public sealed record CreatePaymentRequest(string PayerHandle, long AmountMinor, string Currency, string Note);
public sealed record WalletResponse(long AvailableMinor, long SettledMinor, string Currency, bool IsSimulated);
public sealed record PaymentResponse(
    Guid Id, string Kind, string Status, Guid SenderUserId, string SenderHandle,
    Guid RecipientUserId, string RecipientHandle, long AmountMinor, string Currency,
    string Note, DateTimeOffset CreatedAt, DateTimeOffset? ReversedAt);
public sealed record PaymentListResponse(IReadOnlyList<PaymentResponse> Items);
public sealed record PaymentRequestResponse(
    Guid Id, string Status, Guid RequesterUserId, string RequesterHandle,
    Guid PayerUserId, string PayerHandle, long AmountMinor, string Currency,
    string Note, Guid? PaymentId, DateTimeOffset CreatedAt, DateTimeOffset? ResolvedAt);
public sealed record PaymentRequestListResponse(IReadOnlyList<PaymentRequestResponse> Items);

public sealed class PaymentCommandException(int status, string code, string detail) : Exception(detail)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public interface IPaymentFailureInjector
{
    Task BeforeCommitAsync(string operation, CancellationToken cancellationToken);
}

public sealed class NoopPaymentFailureInjector : IPaymentFailureInjector
{
    public Task BeforeCommitAsync(string operation, CancellationToken cancellationToken) => Task.CompletedTask;
}

internal static class PaymentMappings
{
    public static string Value(PaymentStatus status) => status.ToString().ToLowerInvariant();
    public static string Value(PaymentKind kind) => kind.ToString().ToLowerInvariant();
    public static string Value(PaymentRequestStatus status) => status.ToString().ToLowerInvariant();
}
