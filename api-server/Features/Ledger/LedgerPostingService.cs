using Senzy.Api.Infrastructure.Persistence.Models;
using Senzy.Api.Infrastructure.Persistence;

namespace Senzy.Api.Features.Ledger;

public sealed class LedgerPostingService(SenzyDbContext? db = null) : ILedgerPostingService
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

    var transaction = Post(
        debit,
        credit,
        request.AmountMinor,
        request.Kind switch
        {
          LedgerMovementKind.Payment => LedgerTransactionKind.Payment,
          LedgerMovementKind.Reversal => LedgerTransactionKind.Reversal,
          LedgerMovementKind.FundingIn => LedgerTransactionKind.FundingIn,
          LedgerMovementKind.FundingOut => LedgerTransactionKind.FundingOut,
          LedgerMovementKind.SandboxOpeningBalance => LedgerTransactionKind.SandboxOpeningBalance,
          _ => throw new ArgumentOutOfRangeException(nameof(request.Kind))
        },
        request.CreatedAt,
        request.PaymentId,
        request.ReversesTransactionId,
        request.FundingTransferId);

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
    if (amountMinor <= 0)
    {
      throw new ArgumentOutOfRangeException(nameof(amountMinor));
    }

    if (debitAccount.Id == creditAccount.Id)
    {
      throw new InvalidOperationException("Ledger accounts must differ.");
    }

    if (!string.Equals(debitAccount.Currency, creditAccount.Currency, StringComparison.Ordinal)
        || debitAccount.Currency != "USD")
    {
      throw new InvalidOperationException("Ledger currencies must match.");
    }

    if (!debitAccount.AllowNegative && debitAccount.BalanceMinor < amountMinor)
    {
      throw new InsufficientFundsException();
    }

    checked
    {
      debitAccount.BalanceMinor -= amountMinor;
      creditAccount.BalanceMinor += amountMinor;
    }

    var transaction = new LedgerTransaction
    {
      PaymentId = paymentId,
      FundingTransferId = fundingTransferId,
      ReversesTransactionId = reversesTransactionId,
      Kind = kind,
      Currency = debitAccount.Currency,
      CreatedAt = createdAt
    };

    transaction.Entries =
    [
      new()
      {
        LedgerTransactionId = transaction.Id,
        LedgerAccountId = debitAccount.Id,
        Direction = LedgerEntryDirection.Debit,
        AmountMinor = amountMinor,
        Currency = transaction.Currency
      },
      new()
      {
        LedgerTransactionId = transaction.Id,
        LedgerAccountId = creditAccount.Id,
        Direction = LedgerEntryDirection.Credit,
        AmountMinor = amountMinor,
        Currency = transaction.Currency
      }
    ];

    return transaction;
  }
}
