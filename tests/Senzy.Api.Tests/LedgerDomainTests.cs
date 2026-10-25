using Senzy.Modules.Ledger;
using Senzy.Api.Features.Payments;
using Senzy.Api.Infrastructure.Persistence.Models;
using Xunit;

namespace Senzy.Api.Tests;

public sealed class LedgerDomainTests
{
  [Fact]
  public void PostingPreservesValueAndAlwaysProducesBalancedEntries()
  {
    var random = new Random(42);
    var engine = new LedgerPostingEngine();
    for (var index = 0; index < 1_000; index++)
    {
      var opening = random.NextInt64(1, 10_000_000);
      var amount = random.NextInt64(1, opening + 1);
      var debit = Wallet(opening);
      var credit = Wallet(random.NextInt64(0, 10_000_000));
      var total = debit.BalanceMinor + credit.BalanceMinor;
      var plan = engine.CreatePlan(
          new(debit.Id, debit.Currency, debit.BalanceMinor, debit.AllowNegative),
          new(credit.Id, credit.Currency, credit.BalanceMinor, credit.AllowNegative),
          new(debit.Id, credit.Id, amount, "USD", LedgerMovementKind.Payment, DateTimeOffset.UnixEpoch));
      Assert.Equal(total, debit.BalanceMinor + credit.BalanceMinor);
      Assert.Equal(debit.Id, plan.DebitAccountId);
      Assert.Equal(credit.Id, plan.CreditAccountId);
      Assert.Equal(amount, plan.AmountMinor);
    }
  }

  [Fact]
  public void InsufficientFundsDoesNotMutateAccounts()
  {
    var debit = Wallet(99);
    var credit = Wallet(50);
    Assert.Throws<InsufficientFundsException>(() => new LedgerPostingEngine().CreatePlan(
        new(debit.Id, debit.Currency, debit.BalanceMinor, debit.AllowNegative),
        new(credit.Id, credit.Currency, credit.BalanceMinor, credit.AllowNegative),
        new(debit.Id, credit.Id, 100, "USD", LedgerMovementKind.Payment, DateTimeOffset.UnixEpoch)));
    Assert.Equal(99, debit.BalanceMinor);
    Assert.Equal(50, credit.BalanceMinor);
  }

  [Theory]
  [InlineData(PaymentStatus.Pending, PaymentStatus.Posted, true)]
  [InlineData(PaymentStatus.Pending, PaymentStatus.Failed, true)]
  [InlineData(PaymentStatus.Posted, PaymentStatus.Reversed, true)]
  [InlineData(PaymentStatus.Posted, PaymentStatus.Failed, false)]
  [InlineData(PaymentStatus.Reversed, PaymentStatus.Posted, false)]
  [InlineData(PaymentStatus.Failed, PaymentStatus.Posted, false)]
  public void PaymentStateMachineAllowsOnlyLegalTransitions(PaymentStatus from, PaymentStatus to, bool expected) =>
      Assert.Equal(expected, PaymentStateMachine.CanTransition(from, to));

  private static LedgerAccount Wallet(long balance) => new()
  {
    Id = Guid.NewGuid(),
    OwnerUserId = Guid.NewGuid(),
    Purpose = LedgerAccountPurpose.Wallet,
    Currency = "USD",
    BalanceMinor = balance,
    AllowNegative = false
  };
}
