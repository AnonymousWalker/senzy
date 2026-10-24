using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Senzy.Api.Common;
using Senzy.Api.Features.Profiles;
using Senzy.Api.Features.Wallets;
using Senzy.Api.Infrastructure.Persistence;
using Senzy.Api.Infrastructure.Persistence.Models;

namespace Senzy.Api.Features.Operations;

public static class OperationsRoutes
{
    public static void MapOperationsRoutes(this WebApplication app)
    {
        var group = app.MapGroup("/v1/operations").RequireAuthorization().RequireRateLimiting("payments").WithTags("Operations");
        group.MapGet("/status", async (ClaimsPrincipal actor, WalletPolicy walletPolicy,
            ReconciliationService reconciliation, CancellationToken ct) =>
        {
            var actorId = ProfileRoutes.ActorId(actor);
            var frozen = await walletPolicy.IsFrozenAsync(actorId, ct);
            return Results.Ok(new OperationsStatusResponse(frozen, walletPolicy.DailyLimitMinor,
                await walletPolicy.UsedTodayAsync(actorId, ct), "USD", await reconciliation.LatestAsync(ct)));
        }).WithName("GetOperationsStatus").Produces<OperationsStatusResponse>();

        group.MapPost("/freeze", async (ClaimsPrincipal actor, WalletPolicy walletPolicy, CancellationToken ct) =>
        {
            var actorId = ProfileRoutes.ActorId(actor);
            await walletPolicy.SetFrozenAsync(actorId, true, ct);
            return Results.NoContent();
        }).WithName("FreezeWallet").Produces(204);

        group.MapPost("/unfreeze", async (ClaimsPrincipal actor, WalletPolicy walletPolicy, CancellationToken ct) =>
        {
            var actorId = ProfileRoutes.ActorId(actor);
            await walletPolicy.SetFrozenAsync(actorId, false, ct);
            return Results.NoContent();
        }).WithName("UnfreezeWallet").Produces(204);

        group.MapGet("/transactions", async (int? limit, ClaimsPrincipal actor, SenzyDbContext db, CancellationToken ct) =>
        {
            var actorId = ProfileRoutes.ActorId(actor);
            var payments = await (from payment in db.Payments.AsNoTracking()
                join sender in db.Profiles.AsNoTracking() on payment.SenderUserId equals sender.UserId
                join recipient in db.Profiles.AsNoTracking() on payment.RecipientUserId equals recipient.UserId
                where payment.SenderUserId == actorId || payment.RecipientUserId == actorId
                select new TransactionHistoryItem(payment.Id,
                    payment.SenderUserId == actorId ? "payment_sent" : "payment_received",
                    payment.Status.ToString().ToLower(),
                    payment.SenderUserId == actorId ? -payment.AmountMinor : payment.AmountMinor, payment.Currency,
                    payment.SenderUserId == actorId ? $"To ${recipient.Handle}" : $"From ${sender.Handle}", payment.CreatedAt))
                .ToListAsync(ct);
            var funding = await db.FundingTransfers.AsNoTracking().Where(transfer => transfer.UserId == actorId)
                .Select(transfer => new TransactionHistoryItem(transfer.Id,
                    transfer.Direction == FundingDirection.CashIn ? "cash_in" : "cash_out",
                    transfer.Status.ToString().ToLower(),
                    transfer.Direction == FundingDirection.CashIn ? transfer.AmountMinor : -transfer.AmountMinor,
                    transfer.Currency, transfer.Direction == FundingDirection.CashIn ? "Sandbox cash-in" : "Sandbox cash-out",
                    transfer.CreatedAt)).ToListAsync(ct);
            return Results.Ok(new TransactionHistoryResponse(payments.Concat(funding)
                .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
                .Take(Math.Clamp(limit ?? 50, 1, 100)).ToList()));
        }).WithName("ListTransactionHistory").Produces<TransactionHistoryResponse>();

        if (app.Environment.IsDevelopment())
        {
            group.MapPost("/reconciliation", async (ReconciliationService reconciliation, CancellationToken ct) =>
                Results.Ok(await reconciliation.RunAsync(ct)))
                .WithName("RunReconciliation").Produces<ReconciliationResponse>();
        }
    }
}
