namespace Senzy.Api.Features.Wallets;

public interface IWalletPolicy
{
    long DailyLimitMinor { get; }
    Task<bool> IsFrozenAsync(Guid actorId, CancellationToken ct);
    Task<long> UsedTodayAsync(Guid actorId, CancellationToken ct);
    Task EnsureAllowedAsync(Guid actorId, long amountMinor, CancellationToken ct);
    Task SetFrozenAsync(Guid actorId, bool frozen, CancellationToken ct);
}
