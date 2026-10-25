namespace Senzy.Api.Infrastructure.Persistence.Models;

public enum FundingPersistenceDirection
{
  CashIn,
  CashOut
}

public enum FundingPersistenceStatus
{
  Posted
}

public sealed class FundingTransfer
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid UserId { get; set; }
  public Guid ExternalAccountId { get; set; }
  public string Provider { get; set; } = "stub-banking";
  public string ProviderTransferId { get; set; } = "";
  public FundingPersistenceDirection Direction { get; set; }
  public FundingPersistenceStatus Status { get; set; }
  public long AmountMinor { get; set; }
  public string Currency { get; set; } = "USD";
  public Guid LedgerTransactionId { get; set; }
  public DateTimeOffset CreatedAt { get; set; }
}

// A durable local stand-in for the record a real provider would return during reconciliation.
public sealed class SandboxProviderTransfer
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid FundingTransferId { get; set; }
  public Guid UserId { get; set; }
  public Guid ExternalAccountId { get; set; }
  public string ProviderTransferId { get; set; } = "";
  public FundingPersistenceDirection Direction { get; set; }
  public long AmountMinor { get; set; }
  public string Currency { get; set; } = "USD";
  public string Status { get; set; } = "posted";
  public DateTimeOffset CreatedAt { get; set; }
}
