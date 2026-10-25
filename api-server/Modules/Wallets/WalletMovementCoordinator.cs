using Senzy.Modules.Ledger;

namespace Senzy.Modules.Wallets;

public sealed class WalletMovementFailureException(
    int status,
    string code,
    string message) : Exception(message)
{
  public int Status { get; } = status;
  public string Code { get; } = code;
}

public sealed class WalletMovementCoordinator(
    IWalletMovementStore store,
    IWalletPolicy policy,
    ILedgerPostingService ledger,
    TimeProvider clock) : IWalletMovementService
{
  public async Task<WalletMovementResult> MoveAsync(WalletMovementRequest request, CancellationToken ct)
  {
    if (request.AmountMinor <= 0 || request.Currency != "USD")
    {
      throw new WalletMovementFailureException(400, "invalid_movement", "The wallet movement is invalid.");
    }

    var accounts = await store.LoadAndLockAsync(
        request.DebitOwnerUserId,
        request.DebitRole,
        request.CreditOwnerUserId,
        request.CreditRole,
        request.Currency,
        ct);

    if (accounts.Count != 2)
    {
      throw new WalletMovementFailureException(409, "wallet_unavailable", "A simulated wallet is unavailable.");
    }

    var debit = accounts.Single(account =>
        account.OwnerUserId == request.DebitOwnerUserId && account.Role == request.DebitRole);
    var credit = accounts.Single(account =>
        account.OwnerUserId == request.CreditOwnerUserId && account.Role == request.CreditRole);

    if (request.EnforceOutboundPolicy)
    {
      await policy.EnsureAllowedAsync(request.DebitOwnerUserId, request.AmountMinor, ct);
    }

    try
    {
      var posting = ledger.Post(
          new LedgerPostingRequest(
              debit.Id,
              credit.Id,
              request.AmountMinor,
              request.Currency,
              request.Kind,
              clock.GetUtcNow(),
              request.PaymentId,
              request.ReversesTransactionId,
              request.FundingTransferId),
          ct);

      return new(posting.TransactionId);
    }
    catch (InsufficientFundsException)
    {
      throw new WalletMovementFailureException(
          409,
          "insufficient_funds",
          "Your simulated balance is too low for this movement.");
    }
  }
}
