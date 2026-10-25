namespace Senzy.Modules.Wallets;

public sealed class WalletPolicyFailureException(
    int status,
    string code,
    string message) : Exception(message)
{
  public int Status { get; } = status;
  public string Code { get; } = code;
}

/// <summary>
/// Owns wallet controls and outbound movement policy while participating in the
/// caller's PostgreSQL transaction. The service never commits independently.
/// </summary>
public sealed class WalletPolicy(
    IWalletPolicyStore store,
    TimeProvider clock,
    WalletPolicyEngine engine,
    long dailyLimitMinor) : IWalletPolicy
{
  public long DailyLimitMinor => dailyLimitMinor;

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
      throw new WalletPolicyFailureException(
          409,
          "wallet_frozen",
          "Your wallet is frozen. Unfreeze it before moving money out.");
    }
    catch (DailyLimitExceededException)
    {
      throw new WalletPolicyFailureException(
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
    WalletMovementCoordinator coordinator) : IWalletMovementService
{
  public async Task<WalletMovementResult> MoveAsync(WalletMovementRequest request, CancellationToken ct)
  {
    try
    {
      return await coordinator.MoveAsync(request, ct);
    }
    catch (WalletMovementFailureException)
    {
      throw;
    }
  }
}
