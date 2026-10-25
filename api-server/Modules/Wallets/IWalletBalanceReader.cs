namespace Senzy.Modules.Wallets;

public sealed record WalletBalance(long AvailableMinor, long SettledMinor, string Currency);

public interface IWalletBalanceReader
{
  Task<WalletBalance?> GetAsync(Guid actorId, CancellationToken cancellationToken);
}
