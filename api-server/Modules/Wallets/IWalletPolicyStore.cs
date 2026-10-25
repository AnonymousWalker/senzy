namespace Senzy.Modules.Wallets;

public interface IWalletPolicyStore
{
  Task<bool> IsFrozenAsync(Guid actorId, CancellationToken ct);
  Task<long> GetUsedTodayAsync(Guid actorId, DateTimeOffset start, CancellationToken ct);
  Task SetFrozenAsync(Guid actorId, bool frozen, DateTimeOffset updatedAt, CancellationToken ct);
}
