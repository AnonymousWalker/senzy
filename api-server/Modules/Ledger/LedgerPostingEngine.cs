namespace Senzy.Modules.Ledger;

public sealed record LedgerAccountState(
    Guid Id,
    string Currency,
    long BalanceMinor,
    bool AllowNegative);

public sealed record LedgerPostingPlan(
    Guid DebitAccountId,
    Guid CreditAccountId,
    long AmountMinor,
    string Currency,
    LedgerMovementKind Kind,
    DateTimeOffset CreatedAt,
    Guid? PaymentId,
    Guid? ReversesTransactionId,
    Guid? FundingTransferId);

public sealed class LedgerPostingEngine
{
  public LedgerPostingPlan CreatePlan(
      LedgerAccountState debit,
      LedgerAccountState credit,
      LedgerPostingRequest request)
  {
    if (request.AmountMinor <= 0)
    {
      throw new ArgumentOutOfRangeException(nameof(request.AmountMinor));
    }

    if (debit.Id == credit.Id)
    {
      throw new InvalidOperationException("Ledger accounts must differ.");
    }

    if (!string.Equals(debit.Currency, credit.Currency, StringComparison.Ordinal)
        || debit.Currency != request.Currency
        || debit.Currency != "USD")
    {
      throw new InvalidOperationException("Ledger currencies must match.");
    }

    if (!debit.AllowNegative && debit.BalanceMinor < request.AmountMinor)
    {
      throw new InsufficientFundsException();
    }

    return new(
        debit.Id,
        credit.Id,
        request.AmountMinor,
        request.Currency,
        request.Kind,
        request.CreatedAt,
        request.PaymentId,
        request.ReversesTransactionId,
        request.FundingTransferId);
  }
}

public sealed class InsufficientFundsException : Exception;
