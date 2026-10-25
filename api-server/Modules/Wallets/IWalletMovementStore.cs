using Senzy.Modules.Ledger;

namespace Senzy.Modules.Wallets;

public sealed record WalletAccountSnapshot(
    Guid Id,
    Guid OwnerUserId,
    LedgerAccountRole Role,
    string Currency);

public interface IWalletMovementStore
{
  Task<IReadOnlyList<WalletAccountSnapshot>> LoadAndLockAsync(
      Guid debitOwnerUserId,
      LedgerAccountRole debitRole,
      Guid creditOwnerUserId,
      LedgerAccountRole creditRole,
      string currency,
      CancellationToken ct);
}
