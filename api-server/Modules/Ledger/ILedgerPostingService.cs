namespace Senzy.Modules.Ledger;

public enum LedgerAccountRole
{
  Wallet,
  SandboxSettlement
}

public enum LedgerMovementKind
{
  Payment,
  Reversal,
  FundingIn,
  FundingOut,
  SandboxOpeningBalance
}

public interface ILedgerPostingService
{
  LedgerPostingResult Post(LedgerPostingRequest request, CancellationToken cancellationToken = default);
}

public sealed record LedgerPostingRequest(
    Guid DebitAccountId,
    Guid CreditAccountId,
    long AmountMinor,
    string Currency,
    LedgerMovementKind Kind,
    DateTimeOffset CreatedAt,
    Guid? PaymentId = null,
    Guid? ReversesTransactionId = null,
    Guid? FundingTransferId = null);

public sealed record LedgerPostingResult(Guid TransactionId);
