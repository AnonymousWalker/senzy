using Microsoft.EntityFrameworkCore;
using Senzy.Modules.Wallets;

namespace Senzy.Api.Infrastructure.Persistence;

public sealed class WalletBalanceStore(SenzyDbContext db) : IWalletBalanceReader
{
  public async Task<WalletBalance?> GetAsync(Guid actorId, CancellationToken cancellationToken)
  {
    var wallet = await db.LedgerAccounts.AsNoTracking().SingleOrDefaultAsync(
        account => account.OwnerUserId == actorId
            && account.Purpose == Models.LedgerAccountPurpose.Wallet
            && account.Currency == "USD",
        cancellationToken);

    return wallet is null
        ? null
        : new(wallet.BalanceMinor, wallet.BalanceMinor, wallet.Currency);
  }
}
