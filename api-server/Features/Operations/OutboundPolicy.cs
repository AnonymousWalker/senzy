using Microsoft.EntityFrameworkCore;
using Senzy.Api.Common;
using Senzy.Api.Infrastructure.Persistence;
using Senzy.Api.Infrastructure.Persistence.Models;

namespace Senzy.Api.Features.Operations;

public sealed class OutboundPolicy(SenzyDbContext db, TimeProvider clock, IConfiguration configuration)
{
    public long DailyLimitMinor => configuration.GetValue("Operations:DailyOutboundLimitMinor", 1_000_000L);

    public async Task<long> UsedTodayAsync(Guid actorId, CancellationToken ct)
    {
        var start = new DateTimeOffset(clock.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero);
        return await (from entry in db.LedgerEntries.AsNoTracking()
            join transaction in db.LedgerTransactions.AsNoTracking() on entry.LedgerTransactionId equals transaction.Id
            join account in db.LedgerAccounts.AsNoTracking() on entry.LedgerAccountId equals account.Id
            where account.OwnerUserId == actorId && account.Purpose == LedgerAccountPurpose.Wallet &&
                  entry.Direction == LedgerEntryDirection.Debit && transaction.CreatedAt >= start &&
                  (transaction.Kind == LedgerTransactionKind.Payment || transaction.Kind == LedgerTransactionKind.Reversal ||
                   transaction.Kind == LedgerTransactionKind.FundingOut)
            select (long?)entry.AmountMinor).SumAsync(ct) ?? 0;
    }

    public async Task EnsureAllowedAsync(Guid actorId, long amountMinor, CancellationToken ct)
    {
        if (await db.WalletControls.AsNoTracking().AnyAsync(control => control.UserId == actorId && control.IsFrozen, ct))
            throw new PaymentCommandException(409, "wallet_frozen", "Your wallet is frozen. Unfreeze it before moving money out.");
        if (amountMinor > DailyLimitMinor - await UsedTodayAsync(actorId, ct))
            throw new PaymentCommandException(409, "daily_limit_exceeded", "This transaction would exceed your simulated daily outbound limit.");
    }
}
