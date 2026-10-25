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
public sealed class WalletPolicy(SenzyDbContext db, TimeProvider clock, IConfiguration configuration) : IWalletPolicy
{
  public long DailyLimitMinor => configuration.GetValue("Operations:DailyOutboundLimitMinor", 1_000_000L);

  public async Task<bool> IsFrozenAsync(Guid actorId, CancellationToken ct) =>
      await db.WalletControls.AsNoTracking()
          .AnyAsync(control => control.UserId == actorId && control.IsFrozen, ct);

  public async Task<long> UsedTodayAsync(Guid actorId, CancellationToken ct)
  {
    var start = new DateTimeOffset(clock.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero);

    return await (
            from entry in db.LedgerEntries.AsNoTracking()
            join transaction in db.LedgerTransactions.AsNoTracking()
                on entry.LedgerTransactionId equals transaction.Id
            join account in db.LedgerAccounts.AsNoTracking()
                on entry.LedgerAccountId equals account.Id
            where account.OwnerUserId == actorId
                && account.Purpose == LedgerAccountPurpose.Wallet
                && entry.Direction == LedgerEntryDirection.Debit
                && transaction.CreatedAt >= start
                && (transaction.Kind == LedgerTransactionKind.Payment
                    || transaction.Kind == LedgerTransactionKind.Reversal
                    || transaction.Kind == LedgerTransactionKind.FundingOut)
            select (long?)entry.AmountMinor)
        .SumAsync(ct) ?? 0;
  }

  public async Task EnsureAllowedAsync(Guid actorId, long amountMinor, CancellationToken ct)
  {
    if (await IsFrozenAsync(actorId, ct))
    {
      throw new PaymentCommandException(
          409,
          "wallet_frozen",
          "Your wallet is frozen. Unfreeze it before moving money out.");
    }

    if (amountMinor > DailyLimitMinor - await UsedTodayAsync(actorId, ct))
    {
      throw new PaymentCommandException(
          409,
          "daily_limit_exceeded",
          "This transaction would exceed your simulated daily outbound limit.");
    }
  }

  public async Task SetFrozenAsync(Guid actorId, bool frozen, CancellationToken ct)
  {
    var control = await db.WalletControls.SingleOrDefaultAsync(value => value.UserId == actorId, ct);
    if (control is null)
    {
      control = new WalletControl { UserId = actorId };
      db.WalletControls.Add(control);
    }

    control.IsFrozen = frozen;
    control.UpdatedAt = clock.GetUtcNow();
    await db.SaveChangesAsync(ct);
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
