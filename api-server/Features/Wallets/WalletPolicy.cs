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
    SenzyDbContext db,
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

    var ids = await db.LedgerAccounts.AsNoTracking()
        .Where(account =>
            ((account.OwnerUserId == request.DebitOwnerUserId && account.Purpose == ToPurpose(request.DebitRole))
                || (account.OwnerUserId == request.CreditOwnerUserId && account.Purpose == ToPurpose(request.CreditRole)))
            && account.Currency == request.Currency)
        .Select(account => account.Id)
        .ToListAsync(ct);

    if (ids.Count != 2)
    {
      throw new PaymentCommandException(409, "wallet_unavailable", "A simulated wallet is unavailable.");
    }

    var low = ids.Min();
    var high = ids.Max();
    var accounts = await db.LedgerAccounts.FromSqlInterpolated(
        $"""SELECT * FROM "LedgerAccounts" WHERE "Id" IN ({low}, {high}) ORDER BY "Id" FOR UPDATE""")
        .ToListAsync(ct);

    var debit = accounts.Single(account =>
        account.OwnerUserId == request.DebitOwnerUserId && account.Purpose == ToPurpose(request.DebitRole));
    var credit = accounts.Single(account =>
        account.OwnerUserId == request.CreditOwnerUserId && account.Purpose == ToPurpose(request.CreditRole));

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

  private static LedgerAccountPurpose ToPurpose(LedgerAccountRole role) => role switch
  {
    LedgerAccountRole.Wallet => LedgerAccountPurpose.Wallet,
    LedgerAccountRole.SandboxSettlement => LedgerAccountPurpose.SandboxIssuance,
    _ => throw new ArgumentOutOfRangeException(nameof(role))
  };
}
