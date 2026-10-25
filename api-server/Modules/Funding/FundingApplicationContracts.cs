namespace Senzy.Modules.Funding;

public interface IFundingFailureInjector
{
  Task BeforeCommitAsync(CancellationToken cancellationToken);
}

public sealed class NoopFundingFailureInjector : IFundingFailureInjector
{
  public Task BeforeCommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed record FundingCreateCommand(
    Guid ExternalAccountId,
    string Direction,
    long AmountMinor,
    string Currency);

public sealed record FundingTransferResult(
    Guid Id,
    Guid ExternalAccountId,
    string Provider,
    string ProviderTransferId,
    string Direction,
    string Status,
    long AmountMinor,
    string Currency,
    DateTimeOffset CreatedAt);

public sealed record FundingTransferListResult(IReadOnlyList<FundingTransferResult> Items);

internal static class FundingMappings
{
  public static string Value(FundingDirection value) =>
      value == FundingDirection.CashIn ? "cash_in" : "cash_out";

  public static string Value(FundingStatus value) => value.ToString().ToLowerInvariant();
}
