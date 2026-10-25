using Senzy.Modules.Wallets;
using Xunit;

namespace Senzy.Api.Tests;

public sealed class WalletPolicyDomainTests
{
  [Fact]
  public void FrozenWalletIsRejectedBeforeLimitEvaluation()
  {
    var engine = new WalletPolicyEngine();

    Assert.Throws<WalletFrozenException>(() =>
        engine.EnsureOutboundAllowed(true, 0, 1_000, 1));
  }

  [Fact]
  public void AmountOverRemainingDailyLimitIsRejected()
  {
    var engine = new WalletPolicyEngine();

    Assert.Throws<DailyLimitExceededException>(() =>
        engine.EnsureOutboundAllowed(false, 900, 1_000, 101));
  }

  [Fact]
  public void AmountWithinRemainingDailyLimitIsAllowed()
  {
    var engine = new WalletPolicyEngine();

    engine.EnsureOutboundAllowed(false, 900, 1_000, 100);
  }
}
