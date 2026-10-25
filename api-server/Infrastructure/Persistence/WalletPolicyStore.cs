using Microsoft.EntityFrameworkCore;
using Senzy.Api.Infrastructure.Persistence.Models;
using Senzy.Modules.Wallets;

namespace Senzy.Api.Infrastructure.Persistence;

public sealed class WalletPolicyStore(SenzyDbContext db) : IWalletPolicyStore
{
  public Task<bool> IsFrozenAsync(Guid actorId, CancellationToken ct) =>
      db.WalletControls.AsNoTracking()
          .AnyAsync(control => control.UserId == actorId && control.IsFrozen, ct);

  public async Task<long> GetUsedTodayAsync(Guid actorId, DateTimeOffset start, CancellationToken ct)
  {
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

  public async Task SetFrozenAsync(
      Guid actorId,
      bool frozen,
      DateTimeOffset updatedAt,
      CancellationToken ct)
  {
    var control = await db.WalletControls.SingleOrDefaultAsync(value => value.UserId == actorId, ct);
    if (control is null)
    {
      control = new WalletControl { UserId = actorId };
      db.WalletControls.Add(control);
    }

    control.IsFrozen = frozen;
    control.UpdatedAt = updatedAt;
    await db.SaveChangesAsync(ct);
  }
}
