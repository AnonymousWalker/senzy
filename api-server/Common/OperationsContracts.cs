using Senzy.Api.Infrastructure.Persistence.Models;

using Senzy.Modules.Funding;

namespace Senzy.Api.Common;

public sealed record CreateFundingTransferRequest(
    Guid ExternalAccountId,
    string Direction,
    long AmountMinor,
    string Currency);

public sealed record FundingTransferResponse(
    Guid Id,
    Guid ExternalAccountId,
    string Provider,
    string ProviderTransferId,
    string Direction,
    string Status,
    long AmountMinor,
    string Currency,
    DateTimeOffset CreatedAt);

public sealed record FundingTransferListResponse(IReadOnlyList<FundingTransferResponse> Items);

public sealed record NotificationResponse(
    Guid Id,
    string Kind,
    string Title,
    string Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

public sealed record NotificationListResponse(
    IReadOnlyList<NotificationResponse> Items,
    int UnreadCount);

public sealed record OperationsStatusResponse(
    bool IsFrozen,
    long DailyOutboundLimitMinor,
    long DailyOutboundUsedMinor,
    string Currency,
    ReconciliationResponse? LatestReconciliation);

public sealed record ReconciliationResponse(
    Guid Id,
    string Status,
    int UnbalancedTransactions,
    int BalanceMismatches,
    int PaymentMismatches,
    int FundingMismatches,
    long UnexplainedVarianceMinor,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);

public sealed record TransactionHistoryItem(
    Guid Id,
    string Kind,
    string Status,
    long AmountMinor,
    string Currency,
    string Description,
    DateTimeOffset CreatedAt);

public sealed record TransactionHistoryResponse(IReadOnlyList<TransactionHistoryItem> Items);

internal static class OperationsMappings
{
  public static string Value(FundingDirection value) =>
      value == FundingDirection.CashIn ? "cash_in" : "cash_out";

  public static string Value(FundingStatus value) => value.ToString().ToLowerInvariant();
}
