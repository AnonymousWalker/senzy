using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Senzy.Api.Common;
using Senzy.Api.Features.Ledger;
using Senzy.Api.Features.Profiles;
using Senzy.Api.Infrastructure.Persistence;
using Senzy.Api.Infrastructure.Persistence.Models;

namespace Senzy.Api.Features.Payments;

public sealed class PaymentService(
    SenzyDbContext db,
    LedgerPostingService ledger,
    TimeProvider clock,
    IPaymentFailureInjector failureInjector)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const long MaximumAmountMinor = 1_000_000;

    public async Task<WalletResponse> GetWalletAsync(Guid actorId, CancellationToken ct)
    {
        var wallet = await db.LedgerAccounts.AsNoTracking().SingleOrDefaultAsync(
            account => account.OwnerUserId == actorId && account.Purpose == LedgerAccountPurpose.Wallet && account.Currency == "USD", ct);
        if (wallet is null) throw new PaymentCommandException(404, "wallet_missing", "Wallet not found.");
        return new(wallet.BalanceMinor, wallet.BalanceMinor, wallet.Currency, true);
    }

    public async Task<PaymentResponse> SendAsync(Guid actorId, SendPaymentRequest request, string idempotencyKey, CancellationToken ct)
    {
        ValidateTransfer(request.RecipientHandle, request.AmountMinor, request.Currency, request.Note);
        ValidateIdempotencyKey(idempotencyKey);
        var recipient = await FindProfileAsync(request.RecipientHandle, ct);
        if (recipient.UserId == actorId) throw new PaymentCommandException(400, "self_payment", "You cannot send a payment to yourself.");
        var sender = await RequiredProfileAsync(actorId, ct);
        var normalized = request with { RecipientHandle = recipient.Handle, Currency = "USD", Note = NormalizeNote(request.Note) };
        return await PostPaymentAsync(actorId, sender, recipient, normalized.AmountMinor, normalized.Currency, normalized.Note,
            PaymentKind.Send, null, "send", idempotencyKey, Hash(normalized), ct);
    }

    public async Task<PaymentResponse> AcceptRequestAsync(Guid actorId, Guid requestId, string idempotencyKey, CancellationToken ct)
    {
        ValidateIdempotencyKey(idempotencyKey);
        var operation = $"request.accept:{requestId}";
        var requestHash = Hash(new { requestId });
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockIdempotencyScopeAsync(actorId, operation, idempotencyKey, ct);
        if (await ReplayAsync<PaymentResponse>(actorId, operation, idempotencyKey, requestHash, ct) is { } replay) return replay;
        var paymentRequest = await db.PaymentRequests.FromSqlInterpolated(
            $"""SELECT * FROM "PaymentRequests" WHERE "Id" = {requestId} FOR UPDATE""").SingleOrDefaultAsync(ct);
        if (paymentRequest is null || paymentRequest.PayerUserId != actorId)
            throw new PaymentCommandException(404, "request_missing", "Payment request not found.");
        if (paymentRequest.Status != PaymentRequestStatus.Pending)
            throw new PaymentCommandException(409, "request_not_pending", "This payment request is no longer pending.");
        var sender = await RequiredProfileAsync(actorId, ct);
        var recipient = await RequiredProfileAsync(paymentRequest.RequesterUserId, ct);
        var response = await AddPostedPaymentAsync(sender, recipient, paymentRequest.AmountMinor, paymentRequest.Currency,
            paymentRequest.Note, PaymentKind.Request, paymentRequest.Id, ct);
        paymentRequest.Status = PaymentRequestStatus.Paid;
        paymentRequest.PaymentId = response.Id;
        paymentRequest.ResolvedAt = clock.GetUtcNow();
        AddOutbox("payment_request.paid", paymentRequest.Id, new { paymentRequest.Id, paymentRequest.PaymentId, status = "paid" });
        AddIdempotency(actorId, operation, idempotencyKey, requestHash, response, response.Id);
        await db.SaveChangesAsync(ct);
        await failureInjector.BeforeCommitAsync(operation, ct);
        await transaction.CommitAsync(ct);
        return response;
    }

    public async Task<PaymentResponse> ReverseAsync(Guid actorId, Guid paymentId, string idempotencyKey, CancellationToken ct)
    {
        ValidateIdempotencyKey(idempotencyKey);
        var operation = $"payment.reverse:{paymentId}";
        var requestHash = Hash(new { paymentId });
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockIdempotencyScopeAsync(actorId, operation, idempotencyKey, ct);
        if (await ReplayAsync<PaymentResponse>(actorId, operation, idempotencyKey, requestHash, ct) is { } replay) return replay;
        var payment = await db.Payments.FromSqlInterpolated(
            $"""SELECT * FROM "Payments" WHERE "Id" = {paymentId} FOR UPDATE""").SingleOrDefaultAsync(ct);
        if (payment is null || payment.SenderUserId != actorId)
            throw new PaymentCommandException(404, "payment_missing", "Payment not found.");
        if (!PaymentStateMachine.CanTransition(payment.Status, PaymentStatus.Reversed))
            throw new PaymentCommandException(409, "payment_not_reversible", "This payment cannot be reversed.");
        var accounts = await LockWalletsAsync(payment.SenderUserId, payment.RecipientUserId, ct);
        var senderWallet = accounts.Single(account => account.OwnerUserId == payment.SenderUserId);
        var recipientWallet = accounts.Single(account => account.OwnerUserId == payment.RecipientUserId);
        LedgerTransaction reversal;
        try
        {
            reversal = ledger.Post(recipientWallet, senderWallet, payment.AmountMinor, LedgerTransactionKind.Reversal,
                clock.GetUtcNow(), payment.Id, payment.PostedLedgerTransactionId);
        }
        catch (InsufficientFundsException)
        {
            throw new PaymentCommandException(409, "reversal_insufficient_funds", "The recipient no longer has enough simulated funds for this reversal.");
        }
        db.LedgerTransactions.Add(reversal);
        payment.Status = PaymentStatus.Reversed;
        payment.ReversalLedgerTransactionId = reversal.Id;
        payment.ReversedAt = clock.GetUtcNow();
        var sender = await RequiredProfileAsync(payment.SenderUserId, ct);
        var recipient = await RequiredProfileAsync(payment.RecipientUserId, ct);
        var response = Map(payment, sender.Handle, recipient.Handle);
        AddOutbox("payment.reversed", payment.Id, new { payment.Id, payment.AmountMinor, payment.Currency, status = "reversed" });
        AddIdempotency(actorId, operation, idempotencyKey, requestHash, response, payment.Id);
        await db.SaveChangesAsync(ct);
        await failureInjector.BeforeCommitAsync(operation, ct);
        await transaction.CommitAsync(ct);
        return response;
    }

    public async Task<PaymentRequestResponse> CreateRequestAsync(Guid actorId, CreatePaymentRequest request, string idempotencyKey, CancellationToken ct)
    {
        ValidateTransfer(request.PayerHandle, request.AmountMinor, request.Currency, request.Note);
        ValidateIdempotencyKey(idempotencyKey);
        var payer = await FindProfileAsync(request.PayerHandle, ct);
        if (payer.UserId == actorId) throw new PaymentCommandException(400, "self_request", "You cannot request a payment from yourself.");
        var requester = await RequiredProfileAsync(actorId, ct);
        var normalized = request with { PayerHandle = payer.Handle, Currency = "USD", Note = NormalizeNote(request.Note) };
        const string operation = "request.create";
        var requestHash = Hash(normalized);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockIdempotencyScopeAsync(actorId, operation, idempotencyKey, ct);
        if (await ReplayAsync<PaymentRequestResponse>(actorId, operation, idempotencyKey, requestHash, ct) is { } replay) return replay;
        var paymentRequest = new PaymentRequest
        {
            RequesterUserId = actorId, PayerUserId = payer.UserId, Status = PaymentRequestStatus.Pending,
            AmountMinor = normalized.AmountMinor, Currency = normalized.Currency, Note = normalized.Note, CreatedAt = clock.GetUtcNow()
        };
        db.PaymentRequests.Add(paymentRequest);
        var response = Map(paymentRequest, requester.Handle, payer.Handle);
        AddOutbox("payment_request.created", paymentRequest.Id,
            new { paymentRequest.Id, paymentRequest.RequesterUserId, paymentRequest.PayerUserId, paymentRequest.AmountMinor, paymentRequest.Currency, status = "pending" });
        AddIdempotency(actorId, operation, idempotencyKey, requestHash, response);
        await db.SaveChangesAsync(ct);
        await failureInjector.BeforeCommitAsync(operation, ct);
        await transaction.CommitAsync(ct);
        return response;
    }

    public async Task<PaymentListResponse> ListPaymentsAsync(Guid actorId, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 100);
        var rows = await (from payment in db.Payments.AsNoTracking()
                          join sender in db.Profiles.AsNoTracking() on payment.SenderUserId equals sender.UserId
                          join recipient in db.Profiles.AsNoTracking() on payment.RecipientUserId equals recipient.UserId
                          where payment.SenderUserId == actorId || payment.RecipientUserId == actorId
                          orderby payment.CreatedAt descending, payment.Id descending
                          select new { payment, SenderHandle = sender.Handle, RecipientHandle = recipient.Handle })
            .Take(limit).ToListAsync(ct);
        return new(rows.Select(row => Map(row.payment, row.SenderHandle, row.RecipientHandle)).ToList());
    }

    public async Task<PaymentRequestListResponse> ListRequestsAsync(Guid actorId, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 100);
        var rows = await (from request in db.PaymentRequests.AsNoTracking()
                          join requester in db.Profiles.AsNoTracking() on request.RequesterUserId equals requester.UserId
                          join payer in db.Profiles.AsNoTracking() on request.PayerUserId equals payer.UserId
                          where request.RequesterUserId == actorId || request.PayerUserId == actorId
                          orderby request.CreatedAt descending, request.Id descending
                          select new { request, RequesterHandle = requester.Handle, PayerHandle = payer.Handle })
            .Take(limit).ToListAsync(ct);
        return new(rows.Select(row => Map(row.request, row.RequesterHandle, row.PayerHandle)).ToList());
    }

    private async Task<PaymentResponse> PostPaymentAsync(Guid actorId, Profile sender, Profile recipient, long amountMinor,
        string currency, string note, PaymentKind kind, Guid? paymentRequestId, string operation, string key, string requestHash, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockIdempotencyScopeAsync(actorId, operation, key, ct);
        if (await ReplayAsync<PaymentResponse>(actorId, operation, key, requestHash, ct) is { } replay) return replay;
        var response = await AddPostedPaymentAsync(sender, recipient, amountMinor, currency, note, kind, paymentRequestId, ct);
        AddIdempotency(actorId, operation, key, requestHash, response, response.Id);
        await db.SaveChangesAsync(ct);
        await failureInjector.BeforeCommitAsync(operation, ct);
        await transaction.CommitAsync(ct);
        return response;
    }

    private async Task<PaymentResponse> AddPostedPaymentAsync(Profile sender, Profile recipient, long amountMinor,
        string currency, string note, PaymentKind kind, Guid? paymentRequestId, CancellationToken ct)
    {
        var accounts = await LockWalletsAsync(sender.UserId, recipient.UserId, ct);
        var senderWallet = accounts.Single(account => account.OwnerUserId == sender.UserId);
        var recipientWallet = accounts.Single(account => account.OwnerUserId == recipient.UserId);
        var now = clock.GetUtcNow();
        var payment = new Payment
        {
            SenderUserId = sender.UserId, RecipientUserId = recipient.UserId, PaymentRequestId = paymentRequestId,
            Kind = kind, Status = PaymentStatus.Posted, AmountMinor = amountMinor, Currency = currency, Note = note, CreatedAt = now
        };
        LedgerTransaction posting;
        try
        {
            posting = ledger.Post(senderWallet, recipientWallet, amountMinor, LedgerTransactionKind.Payment, now, payment.Id);
        }
        catch (InsufficientFundsException)
        {
            throw new PaymentCommandException(409, "insufficient_funds", "Your simulated balance is too low for this payment.");
        }
        payment.PostedLedgerTransactionId = posting.Id;
        db.Payments.Add(payment);
        db.LedgerTransactions.Add(posting);
        AddOutbox("payment.posted", payment.Id,
            new { payment.Id, payment.SenderUserId, payment.RecipientUserId, payment.AmountMinor, payment.Currency, status = "posted" });
        return Map(payment, sender.Handle, recipient.Handle);
    }

    private async Task<List<LedgerAccount>> LockWalletsAsync(Guid firstUserId, Guid secondUserId, CancellationToken ct)
    {
        var ids = await db.LedgerAccounts.AsNoTracking()
            .Where(account => (account.OwnerUserId == firstUserId || account.OwnerUserId == secondUserId) &&
                              account.Purpose == LedgerAccountPurpose.Wallet && account.Currency == "USD")
            .Select(account => account.Id).ToListAsync(ct);
        if (ids.Count != 2) throw new PaymentCommandException(409, "wallet_unavailable", "A simulated wallet is unavailable.");
        var low = ids.Min();
        var high = ids.Max();
        return await db.LedgerAccounts.FromSqlInterpolated(
            $"""SELECT * FROM "LedgerAccounts" WHERE "Id" IN ({low}, {high}) ORDER BY "Id" FOR UPDATE""").ToListAsync(ct);
    }

    private async Task LockIdempotencyScopeAsync(Guid actorId, string operation, string key, CancellationToken ct)
    {
        var scope = $"{actorId:N}:{operation}:{key}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({scope}, 0))", ct);
    }

    private async Task<T?> ReplayAsync<T>(Guid actorId, string operation, string key, string requestHash, CancellationToken ct) where T : class
    {
        var record = await db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(
            value => value.ActorUserId == actorId && value.Operation == operation && value.Key == key, ct);
        if (record is null) return null;
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(record.RequestHash), Encoding.ASCII.GetBytes(requestHash)))
            throw new PaymentCommandException(409, "idempotency_conflict", "That idempotency key was already used with different input.");
        return JsonSerializer.Deserialize<T>(record.ResponseJson, JsonOptions)
            ?? throw new InvalidOperationException("Stored idempotency response is invalid.");
    }

    private void AddIdempotency<T>(Guid actorId, string operation, string key, string requestHash, T response, Guid? paymentId = null)
    {
        db.IdempotencyRecords.Add(new()
        {
            ActorUserId = actorId, Operation = operation, Key = key, RequestHash = requestHash,
            ResponseJson = JsonSerializer.Serialize(response, JsonOptions), PaymentId = paymentId, CreatedAt = clock.GetUtcNow()
        });
    }

    private void AddOutbox(string type, Guid aggregateId, object payload)
    {
        db.OutboxMessages.Add(new()
        {
            Type = type, AggregateId = aggregateId, PayloadJson = JsonSerializer.Serialize(payload, JsonOptions), CreatedAt = clock.GetUtcNow()
        });
    }

    private async Task<Profile> FindProfileAsync(string handle, CancellationToken ct)
    {
        var normalized = ProfileRules.NormalizeHandle(handle);
        return await db.Profiles.AsNoTracking().SingleOrDefaultAsync(profile => profile.Handle == normalized, ct)
            ?? throw new PaymentCommandException(404, "recipient_missing", "That payment handle was not found.");
    }

    private async Task<Profile> RequiredProfileAsync(Guid userId, CancellationToken ct) =>
        await db.Profiles.AsNoTracking().SingleAsync(profile => profile.UserId == userId, ct);

    private static void ValidateTransfer(string handle, long amountMinor, string currency, string? note)
    {
        if (string.IsNullOrWhiteSpace(handle) || amountMinor is <= 0 or > MaximumAmountMinor || currency != "USD" ||
            note is null || note.Trim().Length > 280 || note.Any(char.IsControl))
            throw new PaymentCommandException(400, "invalid_payment", "Use a valid handle, an amount from 1 to 1,000,000 minor units, USD, and a note up to 280 characters.");
    }

    private static void ValidateIdempotencyKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length is < 16 or > 128 || key.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)))
            throw new PaymentCommandException(400, "invalid_idempotency_key", "Provide an Idempotency-Key of 16–128 non-whitespace characters.");
    }

    private static string NormalizeNote(string note) => note.Trim();
    private static string Hash<T>(T request) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions)));

    private static PaymentResponse Map(Payment payment, string senderHandle, string recipientHandle) => new(
        payment.Id, PaymentMappings.Value(payment.Kind), PaymentMappings.Value(payment.Status), payment.SenderUserId, senderHandle,
        payment.RecipientUserId, recipientHandle, payment.AmountMinor, payment.Currency, payment.Note, payment.CreatedAt, payment.ReversedAt);

    private static PaymentRequestResponse Map(PaymentRequest request, string requesterHandle, string payerHandle) => new(
        request.Id, PaymentMappings.Value(request.Status), request.RequesterUserId, requesterHandle, request.PayerUserId, payerHandle,
        request.AmountMinor, request.Currency, request.Note, request.PaymentId, request.CreatedAt, request.ResolvedAt);
}
