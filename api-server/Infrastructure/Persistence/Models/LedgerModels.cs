namespace Senzy.Api.Infrastructure.Persistence.Models;

public enum LedgerAccountPurpose
{
  Wallet,
  SandboxIssuance
}

public enum LedgerEntryDirection
{
  Debit,
  Credit
}

public enum LedgerTransactionKind
{
  SandboxOpeningBalance,
  Payment,
  Reversal,
  FundingIn,
  FundingOut
}

public sealed class LedgerAccount
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid OwnerUserId { get; set; }
  public LedgerAccountPurpose Purpose { get; set; }
  public string Currency { get; set; } = "USD";
  public long BalanceMinor { get; set; }
  public bool AllowNegative { get; set; }
  public DateTimeOffset CreatedAt { get; set; }
}

public sealed class LedgerTransaction
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid? PaymentId { get; set; }
  public Guid? FundingTransferId { get; set; }
  public Guid? ReversesTransactionId { get; set; }
  public LedgerTransactionKind Kind { get; set; }
  public string Currency { get; set; } = "USD";
  public DateTimeOffset CreatedAt { get; set; }
  public List<LedgerEntry> Entries { get; set; } = [];
}

public sealed class LedgerEntry
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid LedgerTransactionId { get; set; }
  public Guid LedgerAccountId { get; set; }
  public LedgerEntryDirection Direction { get; set; }
  public long AmountMinor { get; set; }
  public string Currency { get; set; } = "USD";
}
