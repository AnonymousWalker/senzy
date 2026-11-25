using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Senzy.Api.Infrastructure.Persistence;
using Senzy.Api.Infrastructure.Persistence.Models;
using Senzy.Infrastructure.Banking;
using Senzy.Infrastructure.Transactions;
using Senzy.Modules.Ledger;
using Senzy.Modules.Payments;
using Senzy.Modules.Wallets;

namespace Senzy.Modules.Funding;

public sealed class FundingService(
    SenzyDbContext db,
    IBankingProvider banking,
    TimeProvider clock,
    ITransactionContext transactions,
    IWalletMovementService walletMovement,
    IFundingFailureInjector failureInjector)
{
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
  private const long MaximumAmountMinor = 1_000_000;
  private const string Operation = "funding.create";

  public async Task<FundingTransferResult> CreateAsync(
      Guid actorId,
      FundingCreateCommand request,
      string idempotencyKey,
      CancellationToken ct)
  {
    Validate(request, idempotencyKey);
    var direction = ParseDirection(request.Direction);
    var normalized = request with { Direction = FundingMappings.Value(direction), Currency = "USD" };
    var requestHash = Hash(normalized);
    var externalAccount = banking.FindAccount(actorId, request.ExternalAccountId)
        ?? throw new PaymentCommandException(
            404,
            "funding_source_missing",
            "That sandbox funding source is unavailable. Link it again if the API restarted.");

    if (externalAccount.Status != "linked")
    {
      throw new PaymentCommandException(
          409,
          "funding_source_unavailable",
          "That sandbox funding source is unavailable.");
    }

    await using var transaction = await transactions.BeginAsync(ct);
    await LockIdempotencyScopeAsync(actorId, idempotencyKey, ct);

    if (await ReplayAsync(actorId, idempotencyKey, requestHash, ct) is { } replay)
    {
      return replay;
    }

    var now = clock.GetUtcNow();
    var transfer = new FundingTransfer
    {
      UserId = actorId,
      ExternalAccountId = externalAccount.Id,
      Direction = (FundingPersistenceDirection)direction,
      Status = FundingPersistenceStatus.Pending,
      ProviderOutcome = FundingPersistenceProviderOutcome.Pending,
      AmountMinor = request.AmountMinor,
      Currency = "USD",
      Provider = externalAccount.Provider,
      ProviderOperationId = $"funding_{Guid.NewGuid():N}",
      CreatedAt = now
    };
    var response = Map(transfer);
    db.FundingTransfers.Add(transfer);
    db.IdempotencyRecords.Add(new()
    {
      ActorUserId = actorId,
      Operation = Operation,
      Key = idempotencyKey,
      RequestHash = requestHash,
      ResponseJson = JsonSerializer.Serialize(response, JsonOptions),
      FundingTransferId = transfer.Id,
      CreatedAt = now
    });

    await db.SaveChangesAsync(ct);
    await transaction.CommitAsync(ct);
    return response;
  }

  public async Task ProcessPendingAsync(Guid transferId, CancellationToken ct)
  {
    var transfer = await db.FundingTransfers.AsNoTracking()
        .SingleOrDefaultAsync(value => value.Id == transferId, ct);
    if (transfer is null || transfer.Status is FundingPersistenceStatus.Posted or FundingPersistenceStatus.Failed)
    {
      return;
    }

    var request = new FundingProviderRequest(
        transfer.ProviderOperationId,
        transfer.UserId,
        transfer.ExternalAccountId,
        FundingMappings.Value((FundingDirection)transfer.Direction),
        transfer.AmountMinor,
        transfer.Currency);

    // Provider calls deliberately happen without a DbContext transaction or wallet locks.
    var outcome = transfer.Status == FundingPersistenceStatus.Unknown
        ? await banking.GetFundingAsync(transfer.ProviderOperationId, ct)
        : await banking.CreateFundingAsync(request, ct);

    await ApplyProviderOutcomeAsync(transferId, outcome, ct);
  }

  public async Task ProcessPendingAsync(CancellationToken ct)
  {
    var ids = await db.FundingTransfers.AsNoTracking()
        .Where(value => value.Status == FundingPersistenceStatus.Pending
            || value.Status == FundingPersistenceStatus.Unknown)
        .OrderBy(value => value.CreatedAt)
        .Select(value => value.Id)
        .Take(50)
        .ToListAsync(ct);

    foreach (var id in ids)
    {
      await ProcessPendingAsync(id, ct);
    }
  }

  public async Task<FundingTransferListResult> ListAsync(Guid actorId, int limit, CancellationToken ct)
  {
    var items = await db.FundingTransfers.AsNoTracking()
        .Where(transfer => transfer.UserId == actorId)
        .OrderByDescending(transfer => transfer.CreatedAt)
        .ThenByDescending(transfer => transfer.Id)
        .Take(Math.Clamp(limit, 1, 100))
        .ToListAsync(ct);

    return new(items.Select(Map).ToList());
  }

  private async Task ApplyProviderOutcomeAsync(
      Guid transferId,
      FundingProviderResult result,
      CancellationToken ct)
  {
    await using var transaction = await transactions.BeginAsync(ct);
    var transfer = await db.FundingTransfers
        .FromSqlInterpolated($"SELECT * FROM \"FundingTransfers\" WHERE \"Id\" = {transferId} FOR UPDATE")
        .SingleOrDefaultAsync(ct)
        ?? throw new InvalidOperationException("Funding transfer disappeared while processing.");

    if (transfer.Status == FundingPersistenceStatus.Posted)
    {
      return;
    }

    transfer.ProviderOutcome = result.Outcome switch
    {
      FundingProviderOutcome.Succeeded => FundingPersistenceProviderOutcome.Succeeded,
      FundingProviderOutcome.Failed => FundingPersistenceProviderOutcome.Failed,
      FundingProviderOutcome.Unknown => FundingPersistenceProviderOutcome.Unknown,
      _ => throw new ArgumentOutOfRangeException(nameof(result))
    };
    if (result.TransferId is not null)
    {
      transfer.ProviderTransferId = result.TransferId;
    }

    if (result.Outcome == FundingProviderOutcome.Failed)
    {
      transfer.Status = FundingPersistenceStatus.Failed;
      await SaveResponseAsync(transfer, ct);
      await transaction.CommitAsync(ct);
      return;
    }

    if (result.Outcome == FundingProviderOutcome.Unknown)
    {
      transfer.Status = FundingPersistenceStatus.Unknown;
      await SaveResponseAsync(transfer, ct);
      await transaction.CommitAsync(ct);
      return;
    }

    var direction = (FundingDirection)transfer.Direction;
    WalletMovementResult posting;
    try
    {
      posting = direction == FundingDirection.CashIn
          ? await walletMovement.MoveAsync(
              new WalletMovementRequest(
                  transfer.UserId,
                  LedgerAccountRole.SandboxSettlement,
                  transfer.UserId,
                  LedgerAccountRole.Wallet,
                  transfer.AmountMinor,
                  transfer.Currency,
                  LedgerMovementKind.FundingIn,
                  FundingTransferId: transfer.Id,
                  EnforceOutboundPolicy: false),
              ct)
          : await walletMovement.MoveAsync(
              new WalletMovementRequest(
                  transfer.UserId,
                  LedgerAccountRole.Wallet,
                  transfer.UserId,
                  LedgerAccountRole.SandboxSettlement,
                  transfer.AmountMinor,
                  transfer.Currency,
                  LedgerMovementKind.FundingOut,
                  FundingTransferId: transfer.Id),
              ct);
    }
    catch (Exception error) when (error is WalletMovementFailureException
        or WalletPolicyFailureException)
    {
      // The provider already accepted the stable operation ID. Keep the result
      // unknown so reconciliation can retry internal posting without replaying
      // the external transfer.
      transfer.Status = FundingPersistenceStatus.Unknown;
      await SaveResponseAsync(transfer, ct);
      await transaction.CommitAsync(ct);
      return;
    }

    transfer.LedgerTransactionId = posting.LedgerTransactionId;
    transfer.Status = FundingPersistenceStatus.Posted;
    var providerTransferId = transfer.ProviderTransferId
        ?? throw new InvalidOperationException(
            "A successful funding provider outcome must include a transfer ID.");
    var now = clock.GetUtcNow();
    db.SandboxProviderTransfers.Add(new()
    {
      FundingTransferId = transfer.Id,
      UserId = transfer.UserId,
      ExternalAccountId = transfer.ExternalAccountId,
      ProviderTransferId = providerTransferId,
      Direction = transfer.Direction,
      AmountMinor = transfer.AmountMinor,
      Currency = transfer.Currency,
      Status = "succeeded",
      CreatedAt = now
    });
    db.OutboxMessages.Add(new()
    {
      Type = "funding.posted",
      AggregateId = transfer.Id,
      CreatedAt = now,
      PayloadJson = JsonSerializer.Serialize(
          new
          {
            transfer.Id,
            transfer.UserId,
            transfer.AmountMinor,
            transfer.Currency,
            direction = FundingMappings.Value(direction),
            status = "posted"
          },
          JsonOptions)
    });

    await failureInjector.BeforeCommitAsync(ct);
    await SaveResponseAsync(transfer, ct);
    await transaction.CommitAsync(ct);
  }

  private async Task SaveResponseAsync(FundingTransfer transfer, CancellationToken ct)
  {
    var record = await db.IdempotencyRecords.SingleAsync(
        value => value.FundingTransferId == transfer.Id,
        ct);
    record.ResponseJson = JsonSerializer.Serialize(Map(transfer), JsonOptions);
    await db.SaveChangesAsync(ct);
  }

  private async Task LockIdempotencyScopeAsync(Guid actorId, string key, CancellationToken ct)
  {
    var scope = $"{actorId:N}:{Operation}:{key}";
    await db.Database.ExecuteSqlInterpolatedAsync(
        $"SELECT pg_advisory_xact_lock(hashtextextended({scope}, 0))",
        ct);
  }

  private async Task<FundingTransferResult?> ReplayAsync(
      Guid actorId,
      string key,
      string requestHash,
      CancellationToken ct)
  {
    var record = await db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(
        value => value.ActorUserId == actorId && value.Operation == Operation && value.Key == key,
        ct);
    if (record is null)
    {
      return null;
    }

    if (!CryptographicOperations.FixedTimeEquals(
        Encoding.ASCII.GetBytes(record.RequestHash),
        Encoding.ASCII.GetBytes(requestHash)))
    {
      throw new PaymentCommandException(
          409,
          "idempotency_conflict",
          "That idempotency key was already used with different input.");
    }

    return JsonSerializer.Deserialize<FundingTransferResult>(record.ResponseJson, JsonOptions)
        ?? throw new InvalidOperationException("Stored idempotency response is invalid.");
  }

  private static FundingDirection ParseDirection(string value) => value switch
  {
    "cash_in" => FundingDirection.CashIn,
    "cash_out" => FundingDirection.CashOut,
    _ => throw new PaymentCommandException(400, "invalid_funding", "Direction must be cash_in or cash_out.")
  };

  private static void Validate(FundingCreateCommand request, string key)
  {
    if (request.ExternalAccountId == Guid.Empty
        || request.AmountMinor is <= 0 or > MaximumAmountMinor
        || request.Currency != "USD")
    {
      throw new PaymentCommandException(
          400,
          "invalid_funding",
          "Use a linked sandbox account and a USD amount from 1 to 1,000,000 minor units.");
    }

    if (string.IsNullOrWhiteSpace(key)
        || key.Length is < 16 or > 128
        || key.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)))
    {
      throw new PaymentCommandException(
          400,
          "invalid_idempotency_key",
          "Provide an Idempotency-Key of 16–128 non-whitespace characters.");
    }
  }

  private static string Hash<T>(T request) =>
      Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions)));

  private static FundingTransferResult Map(FundingTransfer transfer) =>
      new(
          transfer.Id,
          transfer.ExternalAccountId,
          transfer.Provider,
          transfer.ProviderTransferId,
          transfer.ProviderOperationId,
          FundingMappings.Value((FundingDirection)transfer.Direction),
          FundingMappings.Value((FundingStatus)transfer.Status),
          FundingMappings.Value((FundingProviderOutcome)transfer.ProviderOutcome),
          transfer.AmountMinor,
          transfer.Currency,
          transfer.CreatedAt);
}
