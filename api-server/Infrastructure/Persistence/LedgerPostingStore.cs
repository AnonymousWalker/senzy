using Senzy.Api.Infrastructure.Persistence.Models;
using Senzy.Modules.Ledger;

namespace Senzy.Api.Infrastructure.Persistence;

public sealed class LedgerPostingStore(SenzyDbContext db) : ILedgerPostingStore
{
  public LedgerPostingContext Load(Guid debitAccountId, Guid creditAccountId)
  {
    var debit = db.LedgerAccounts.Local.SingleOrDefault(account => account.Id == debitAccountId)
        ?? throw new InvalidOperationException("The debit ledger account must be loaded before posting.");
    var credit = db.LedgerAccounts.Local.SingleOrDefault(account => account.Id == creditAccountId)
        ?? throw new InvalidOperationException("The credit ledger account must be loaded before posting.");

    return new(
        new(debit.Id, debit.Currency, debit.BalanceMinor, debit.AllowNegative),
        new(credit.Id, credit.Currency, credit.BalanceMinor, credit.AllowNegative));
  }

  public Guid Save(LedgerPostingPlan plan)
  {
    var debit = db.LedgerAccounts.Local.Single(account => account.Id == plan.DebitAccountId);
    var credit = db.LedgerAccounts.Local.Single(account => account.Id == plan.CreditAccountId);

    debit.BalanceMinor -= plan.AmountMinor;
    credit.BalanceMinor += plan.AmountMinor;

    var transaction = new LedgerTransaction
    {
      PaymentId = plan.PaymentId,
      FundingTransferId = plan.FundingTransferId,
      ReversesTransactionId = plan.ReversesTransactionId,
      Kind = ToLedgerKind(plan.Kind),
      Currency = plan.Currency,
      CreatedAt = plan.CreatedAt,
      Entries =
      [
        new()
        {
          LedgerAccountId = plan.DebitAccountId,
          Direction = LedgerEntryDirection.Debit,
          AmountMinor = plan.AmountMinor,
          Currency = plan.Currency
        },
        new()
        {
          LedgerAccountId = plan.CreditAccountId,
          Direction = LedgerEntryDirection.Credit,
          AmountMinor = plan.AmountMinor,
          Currency = plan.Currency
        }
      ]
    };

    db.LedgerTransactions.Add(transaction);
    return transaction.Id;
  }

  private static LedgerTransactionKind ToLedgerKind(LedgerMovementKind kind) => kind switch
  {
    LedgerMovementKind.Payment => LedgerTransactionKind.Payment,
    LedgerMovementKind.Reversal => LedgerTransactionKind.Reversal,
    LedgerMovementKind.FundingIn => LedgerTransactionKind.FundingIn,
    LedgerMovementKind.FundingOut => LedgerTransactionKind.FundingOut,
    LedgerMovementKind.SandboxOpeningBalance => LedgerTransactionKind.SandboxOpeningBalance,
    _ => throw new ArgumentOutOfRangeException(nameof(kind))
  };
}
