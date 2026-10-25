using Senzy.Api.Infrastructure.Persistence.Models;
using Senzy.Api.Infrastructure.Persistence;
using Senzy.Modules.Ledger;

namespace Senzy.Api.Features.Ledger;

public sealed class LedgerPostingService(
    SenzyDbContext? db = null,
    LedgerPostingEngine? engine = null) : ILedgerPostingService
{
  public LedgerPostingResult Post(LedgerPostingRequest request, CancellationToken cancellationToken = default)
  {
    if (db is null)
    {
      throw new InvalidOperationException("The application ledger contract requires a persistence context.");
    }

    var debit = db.LedgerAccounts.Local.SingleOrDefault(account => account.Id == request.DebitAccountId)
        ?? throw new InvalidOperationException("The debit ledger account must be loaded before posting.");
    var credit = db.LedgerAccounts.Local.SingleOrDefault(account => account.Id == request.CreditAccountId)
        ?? throw new InvalidOperationException("The credit ledger account must be loaded before posting.");

    var plan = (engine ?? new LedgerPostingEngine()).CreatePlan(
        new(debit.Id, debit.Currency, debit.BalanceMinor, debit.AllowNegative),
        new(credit.Id, credit.Currency, credit.BalanceMinor, credit.AllowNegative),
        request);

    debit.BalanceMinor -= plan.AmountMinor;
    credit.BalanceMinor += plan.AmountMinor;

    var transaction = CreateTransaction(plan);

    db.LedgerTransactions.Add(transaction);
    return new(transaction.Id);
  }

  public LedgerTransaction Post(
      LedgerAccount debitAccount,
      LedgerAccount creditAccount,
      long amountMinor,
      LedgerTransactionKind kind,
      DateTimeOffset createdAt,
      Guid? paymentId = null,
      Guid? reversesTransactionId = null,
      Guid? fundingTransferId = null)
  {
    var request = new LedgerPostingRequest(
        debitAccount.Id,
        creditAccount.Id,
        amountMinor,
        debitAccount.Currency,
        ToMovementKind(kind),
        createdAt,
        paymentId,
        reversesTransactionId,
        fundingTransferId);

    var plan = (engine ?? new LedgerPostingEngine()).CreatePlan(
        new(debitAccount.Id, debitAccount.Currency, debitAccount.BalanceMinor, debitAccount.AllowNegative),
        new(creditAccount.Id, creditAccount.Currency, creditAccount.BalanceMinor, creditAccount.AllowNegative),
        request);

    debitAccount.BalanceMinor -= plan.AmountMinor;
    creditAccount.BalanceMinor += plan.AmountMinor;

    return CreateTransaction(plan);
  }

  private static LedgerTransaction CreateTransaction(LedgerPostingPlan plan)
  {
    var transaction = new LedgerTransaction
    {
      PaymentId = plan.PaymentId,
      FundingTransferId = plan.FundingTransferId,
      ReversesTransactionId = plan.ReversesTransactionId,
      Kind = ToLedgerKind(plan.Kind),
      Currency = plan.Currency,
      CreatedAt = plan.CreatedAt
    };

    transaction.Entries =
    [
      new()
      {
        LedgerTransactionId = transaction.Id,
        LedgerAccountId = plan.DebitAccountId,
        Direction = LedgerEntryDirection.Debit,
        AmountMinor = plan.AmountMinor,
        Currency = transaction.Currency
      },
      new()
      {
        LedgerTransactionId = transaction.Id,
        LedgerAccountId = plan.CreditAccountId,
        Direction = LedgerEntryDirection.Credit,
        AmountMinor = plan.AmountMinor,
        Currency = transaction.Currency
      }
    ];

    return transaction;
  }

  private static LedgerMovementKind ToMovementKind(LedgerTransactionKind kind) => kind switch
  {
    LedgerTransactionKind.Payment => LedgerMovementKind.Payment,
    LedgerTransactionKind.Reversal => LedgerMovementKind.Reversal,
    LedgerTransactionKind.FundingIn => LedgerMovementKind.FundingIn,
    LedgerTransactionKind.FundingOut => LedgerMovementKind.FundingOut,
    LedgerTransactionKind.SandboxOpeningBalance => LedgerMovementKind.SandboxOpeningBalance,
    _ => throw new ArgumentOutOfRangeException(nameof(kind))
  };

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
