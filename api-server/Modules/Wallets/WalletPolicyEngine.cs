namespace Senzy.Modules.Wallets;

public sealed class WalletFrozenException : Exception;

public sealed class DailyLimitExceededException : Exception;

public sealed class WalletPolicyEngine
{
  public void EnsureOutboundAllowed(
      bool frozen,
      long usedTodayMinor,
      long dailyLimitMinor,
      long amountMinor)
  {
    if (frozen)
    {
      throw new WalletFrozenException();
    }

    if (amountMinor > dailyLimitMinor - usedTodayMinor)
    {
      throw new DailyLimitExceededException();
    }
  }
}
