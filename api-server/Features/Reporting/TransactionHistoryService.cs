using Microsoft.EntityFrameworkCore;
using Senzy.Api.Common;
using Senzy.Api.Infrastructure.Persistence;
using Senzy.Api.Infrastructure.Persistence.Models;

namespace Senzy.Api.Features.Reporting;

public sealed class TransactionHistoryService(SenzyDbContext db)
{
    public async Task<TransactionHistoryResponse> ListAsync(Guid actorId, int limit, CancellationToken ct)
    {
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
        return new(payments.Concat(funding)
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Take(Math.Clamp(limit, 1, 100)).ToList());
    }
}
