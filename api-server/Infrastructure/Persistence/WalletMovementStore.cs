using Microsoft.EntityFrameworkCore;
using Senzy.Api.Infrastructure.Persistence.Models;
using Senzy.Modules.Ledger;
using Senzy.Modules.Wallets;

namespace Senzy.Api.Infrastructure.Persistence;

public sealed class WalletMovementStore(SenzyDbContext db) : IWalletMovementStore
{
  public async Task<IReadOnlyList<WalletAccountSnapshot>> LoadAndLockAsync(
      Guid debitOwnerUserId,
      LedgerAccountRole debitRole,
      Guid creditOwnerUserId,
      LedgerAccountRole creditRole,
      string currency,
      CancellationToken ct)
  {
    var debitPurpose = ToPurpose(debitRole);
    var creditPurpose = ToPurpose(creditRole);
    var accounts = await db.LedgerAccounts.FromSqlInterpolated(
        $"""
        SELECT * FROM "LedgerAccounts"
        WHERE (("OwnerUserId" = {debitOwnerUserId} AND "Purpose" = {debitPurpose.ToString()})
            OR ("OwnerUserId" = {creditOwnerUserId} AND "Purpose" = {creditPurpose.ToString()}))
          AND "Currency" = {currency}
        ORDER BY "Id"
        FOR UPDATE
        """)
        .ToListAsync(ct);

    if (accounts.Count != 2)
    {
      return [];
    }

    return accounts.Select(account => new WalletAccountSnapshot(
        account.Id,
        account.OwnerUserId,
        ToRole(account.Purpose),
        account.Currency)).ToList();
  }

  private static LedgerAccountPurpose ToPurpose(LedgerAccountRole role) => role switch
  {
    LedgerAccountRole.Wallet => LedgerAccountPurpose.Wallet,
    LedgerAccountRole.SandboxSettlement => LedgerAccountPurpose.SandboxIssuance,
    _ => throw new ArgumentOutOfRangeException(nameof(role))
  };

  private static LedgerAccountRole ToRole(LedgerAccountPurpose purpose) => purpose switch
  {
    LedgerAccountPurpose.Wallet => LedgerAccountRole.Wallet,
    LedgerAccountPurpose.SandboxIssuance => LedgerAccountRole.SandboxSettlement,
    _ => throw new ArgumentOutOfRangeException(nameof(purpose))
  };
}
