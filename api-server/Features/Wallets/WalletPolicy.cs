using Microsoft.EntityFrameworkCore;
using Senzy.Api.Common;
using Senzy.Api.Infrastructure.Persistence;
using Senzy.Api.Infrastructure.Persistence.Models;
using Senzy.Modules.Ledger;
using Senzy.Modules.Wallets;

namespace Senzy.Api.Features.Wallets;

/// <summary>
/// Owns wallet controls and outbound movement policy while participating in the
/// caller's PostgreSQL transaction. The service never commits independently.
/// </summary>
public sealed class WalletPolicy(
    IWalletPolicyStore store,
    TimeProvider clock,
    IConfiguration configuration,
    WalletPolicyEngine engine) : IWalletPolicy
{
  public long DailyLimitMinor => configuration.GetValue("Operations:DailyOutboundLimitMinor", 1_000_000L);

  public async Task<bool> IsFrozenAsync(Guid actorId, CancellationToken ct) =>
      await store.IsFrozenAsync(actorId, ct);

  public async Task<long> UsedTodayAsync(Guid actorId, CancellationToken ct)
  {
    var start = new DateTimeOffset(clock.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero);

    return await store.GetUsedTodayAsync(actorId, start, ct);
  }

  public async Task EnsureAllowedAsync(Guid actorId, long amountMinor, CancellationToken ct)
  {
    var frozen = await IsFrozenAsync(actorId, ct);
    var usedToday = await UsedTodayAsync(actorId, ct);

    try
    {
      engine.EnsureOutboundAllowed(frozen, usedToday, DailyLimitMinor, amountMinor);
    }
    catch (WalletFrozenException)
    {
      throw new PaymentCommandException(
          409,
          "wallet_frozen",
          "Your wallet is frozen. Unfreeze it before moving money out.");
    }
    catch (DailyLimitExceededException)
    {
      throw new PaymentCommandException(
          409,
          "daily_limit_exceeded",
          "This transaction would exceed your simulated daily outbound limit.");
    }
  }

  public async Task SetFrozenAsync(Guid actorId, bool frozen, CancellationToken ct)
  {
    await store.SetFrozenAsync(actorId, frozen, clock.GetUtcNow(), ct);
  }
}

public sealed class WalletMovementService(
    IWalletMovementStore store,
    WalletPolicy policy,
    ILedgerPostingService ledger,
    TimeProvider clock) : IWalletMovementService
{
  public async Task<WalletMovementResult> MoveAsync(WalletMovementRequest request, CancellationToken ct)
  {
    if (request.AmountMinor <= 0 || request.Currency != "USD")
    {
      throw new PaymentCommandException(400, "invalid_movement", "The wallet movement is invalid.");
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
      throw new PaymentCommandException(409, "wallet_unavailable", "A simulated wallet is unavailable.");
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
      throw new PaymentCommandException(
          409,
          "insufficient_funds",
          "Your simulated balance is too low for this movement.");
    }
  }

}
