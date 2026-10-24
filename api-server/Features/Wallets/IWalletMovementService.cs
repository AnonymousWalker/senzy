using Senzy.Api.Features.Ledger;

namespace Senzy.Api.Features.Wallets;

public sealed record WalletMovementRequest(
    Guid DebitOwnerUserId,
    LedgerAccountRole DebitRole,
    Guid CreditOwnerUserId,
    LedgerAccountRole CreditRole,
    long AmountMinor,
    string Currency,
    LedgerMovementKind Kind,
    Guid? PaymentId = null,
    Guid? FundingTransferId = null,
    Guid? ReversesTransactionId = null,
    bool EnforceOutboundPolicy = true);

public sealed record WalletMovementResult(Guid LedgerTransactionId);

public interface IWalletMovementService
{
    Task<WalletMovementResult> MoveAsync(WalletMovementRequest request, CancellationToken ct);
}
