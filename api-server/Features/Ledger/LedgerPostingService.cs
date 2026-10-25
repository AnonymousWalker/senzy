using Senzy.Api.Infrastructure.Persistence.Models;
using Senzy.Modules.Ledger;

namespace Senzy.Api.Features.Ledger;

public sealed class LedgerPostingService(
    ILedgerPostingStore? store = null,
    LedgerPostingEngine? engine = null) : ILedgerPostingService
{
  public LedgerPostingResult Post(LedgerPostingRequest request, CancellationToken cancellationToken = default)
  {
    if (store is null)
    {
      throw new InvalidOperationException("The application ledger contract requires a persistence adapter.");
    }

    var context = store.Load(request.DebitAccountId, request.CreditAccountId);
    var plan = (engine ?? new LedgerPostingEngine()).CreatePlan(context.Debit, context.Credit, request);
    return new(store.Save(plan));
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
