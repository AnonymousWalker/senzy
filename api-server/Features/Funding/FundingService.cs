using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Senzy.Api.Common;
using Senzy.Api.Features.Banking;
using Senzy.Api.Features.Ledger;
using Senzy.Api.Features.Operations;
using Senzy.Api.Infrastructure.Persistence;
using Senzy.Api.Infrastructure.Persistence.Models;

namespace Senzy.Api.Features.Funding;

public sealed class FundingService(SenzyDbContext db, IBankingProvider banking, LedgerPostingService ledger,
    TimeProvider clock, OutboundPolicy outboundPolicy, IFundingFailureInjector failureInjector)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const long MaximumAmountMinor = 1_000_000;

    public async Task<FundingTransferResponse> CreateAsync(Guid actorId, CreateFundingTransferRequest request,
        string idempotencyKey, CancellationToken ct)
    {
        Validate(request, idempotencyKey);
        var direction = request.Direction switch
        {
            "cash_in" => FundingDirection.CashIn,
            "cash_out" => FundingDirection.CashOut,
            _ => throw new PaymentCommandException(400, "invalid_funding", "Direction must be cash_in or cash_out.")
        };
        const string operation = "funding.create";
        var normalized = request with { Direction = OperationsMappings.Value(direction), Currency = "USD" };
        var requestHash = Hash(normalized);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockIdempotencyScopeAsync(actorId, operation, idempotencyKey, ct);
        if (await ReplayAsync(actorId, operation, idempotencyKey, requestHash, ct) is { } replay) return replay;

        var externalAccount = banking.FindAccount(actorId, request.ExternalAccountId)
            ?? throw new PaymentCommandException(404, "funding_source_missing", "That sandbox funding source is unavailable. Link it again if the API restarted.");
        if (externalAccount.Status != "linked")
            throw new PaymentCommandException(409, "funding_source_unavailable", "That sandbox funding source is unavailable.");

        var accounts = await db.LedgerAccounts.FromSqlInterpolated(
            $"""SELECT * FROM "LedgerAccounts" WHERE "OwnerUserId" = {actorId} AND "Currency" = 'USD' ORDER BY "Id" FOR UPDATE""")
            .ToListAsync(ct);
        var wallet = accounts.SingleOrDefault(account => account.Purpose == LedgerAccountPurpose.Wallet)
            ?? throw new PaymentCommandException(409, "wallet_unavailable", "Your simulated wallet is unavailable.");
        var settlement = accounts.SingleOrDefault(account => account.Purpose == LedgerAccountPurpose.SandboxIssuance)
            ?? throw new PaymentCommandException(409, "wallet_unavailable", "Your simulated settlement account is unavailable.");
        if (direction == FundingDirection.CashOut)
        {
            await outboundPolicy.EnsureAllowedAsync(actorId, request.AmountMinor, ct);
        }

        var now = clock.GetUtcNow();
        var transfer = new FundingTransfer
        {
            UserId = actorId, ExternalAccountId = externalAccount.Id, Direction = direction, Status = FundingStatus.Posted,
            AmountMinor = request.AmountMinor, Currency = "USD", Provider = externalAccount.Provider,
            ProviderTransferId = $"stub_transfer_{Guid.NewGuid():N}", CreatedAt = now
        };
        LedgerTransaction posting;
        try
        {
            posting = direction == FundingDirection.CashIn
                ? ledger.Post(settlement, wallet, transfer.AmountMinor, LedgerTransactionKind.FundingIn, now, fundingTransferId: transfer.Id)
                : ledger.Post(wallet, settlement, transfer.AmountMinor, LedgerTransactionKind.FundingOut, now, fundingTransferId: transfer.Id);
        }
        catch (InsufficientFundsException)
        {
            throw new PaymentCommandException(409, "insufficient_funds", "Your simulated balance is too low for this cash-out.");
        }
        transfer.LedgerTransactionId = posting.Id;
        db.LedgerTransactions.Add(posting);
        db.FundingTransfers.Add(transfer);
        db.SandboxProviderTransfers.Add(new()
        {
            FundingTransferId = transfer.Id, UserId = actorId, ExternalAccountId = externalAccount.Id,
            ProviderTransferId = transfer.ProviderTransferId, Direction = direction, AmountMinor = transfer.AmountMinor,
            Currency = transfer.Currency, CreatedAt = now
        });
        var response = Map(transfer);
        db.IdempotencyRecords.Add(new()
        {
            ActorUserId = actorId, Operation = operation, Key = idempotencyKey, RequestHash = requestHash,
            ResponseJson = JsonSerializer.Serialize(response, JsonOptions), FundingTransferId = transfer.Id, CreatedAt = now
        });
        db.OutboxMessages.Add(new()
        {
            Type = "funding.posted", AggregateId = transfer.Id, CreatedAt = now,
            PayloadJson = JsonSerializer.Serialize(new
            {
                transfer.Id, transfer.UserId, transfer.AmountMinor, transfer.Currency,
                direction = OperationsMappings.Value(direction), status = "posted"
            }, JsonOptions)
        });
        await db.SaveChangesAsync(ct);
        await failureInjector.BeforeCommitAsync(ct);
        await transaction.CommitAsync(ct);
        return response;
    }

    public async Task<FundingTransferListResponse> ListAsync(Guid actorId, int limit, CancellationToken ct)
    {
        var items = await db.FundingTransfers.AsNoTracking().Where(transfer => transfer.UserId == actorId)
            .OrderByDescending(transfer => transfer.CreatedAt).ThenByDescending(transfer => transfer.Id)
            .Take(Math.Clamp(limit, 1, 100)).ToListAsync(ct);
        return new(items.Select(Map).ToList());
    }

    private async Task LockIdempotencyScopeAsync(Guid actorId, string operation, string key, CancellationToken ct)
    {
        var scope = $"{actorId:N}:{operation}:{key}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({scope}, 0))", ct);
    }

    private async Task<FundingTransferResponse?> ReplayAsync(Guid actorId, string operation, string key, string requestHash, CancellationToken ct)
    {
        var record = await db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(
            value => value.ActorUserId == actorId && value.Operation == operation && value.Key == key, ct);
        if (record is null) return null;
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(record.RequestHash), Encoding.ASCII.GetBytes(requestHash)))
            throw new PaymentCommandException(409, "idempotency_conflict", "That idempotency key was already used with different input.");
        return JsonSerializer.Deserialize<FundingTransferResponse>(record.ResponseJson, JsonOptions)
            ?? throw new InvalidOperationException("Stored idempotency response is invalid.");
    }

    private static void Validate(CreateFundingTransferRequest request, string key)
    {
        if (request.ExternalAccountId == Guid.Empty || request.AmountMinor is <= 0 or > MaximumAmountMinor || request.Currency != "USD")
            throw new PaymentCommandException(400, "invalid_funding", "Use a linked sandbox account and a USD amount from 1 to 1,000,000 minor units.");
        if (string.IsNullOrWhiteSpace(key) || key.Length is < 16 or > 128 || key.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)))
            throw new PaymentCommandException(400, "invalid_idempotency_key", "Provide an Idempotency-Key of 16–128 non-whitespace characters.");
    }

    private static string Hash<T>(T request) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions)));
    private static FundingTransferResponse Map(FundingTransfer transfer) => new(transfer.Id, transfer.ExternalAccountId,
        transfer.Provider, transfer.ProviderTransferId, OperationsMappings.Value(transfer.Direction), OperationsMappings.Value(transfer.Status),
        transfer.AmountMinor, transfer.Currency, transfer.CreatedAt);
}
