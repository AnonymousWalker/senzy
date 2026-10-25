using Microsoft.EntityFrameworkCore;
using Senzy.Api.Common;
using Senzy.Api.Infrastructure.Persistence;
using Senzy.Api.Infrastructure.Persistence.Models;
using Senzy.Modules.Payments;
using Senzy.Modules.Funding;

namespace Senzy.Api.Features.Reconciliation;

public sealed class ReconciliationService(SenzyDbContext db, TimeProvider clock)
{
  public async Task<ReconciliationResponse> RunAsync(CancellationToken ct)
  {
    var started = clock.GetUtcNow();

    var entrySums = await db.LedgerEntries.AsNoTracking()
        .GroupBy(entry => entry.LedgerTransactionId)
        .Select(group => new
        {
          Id = group.Key,
          Debits = group.Where(entry => entry.Direction == LedgerEntryDirection.Debit)
              .Sum(entry => entry.AmountMinor),
          Credits = group.Where(entry => entry.Direction == LedgerEntryDirection.Credit)
              .Sum(entry => entry.AmountMinor)
        })
        .ToListAsync(ct);

    var transactionCount = await db.LedgerTransactions.CountAsync(ct);
    var unbalanced = entrySums.Count(sum => sum.Debits != sum.Credits) + transactionCount - entrySums.Count;

    var projected = await db.LedgerEntries.AsNoTracking()
        .GroupBy(entry => entry.LedgerAccountId)
        .Select(group => new
        {
          Id = group.Key,
          Balance = group.Sum(entry =>
              entry.Direction == LedgerEntryDirection.Credit ? entry.AmountMinor : -entry.AmountMinor)
        })
        .ToDictionaryAsync(value => value.Id, value => value.Balance, ct);

    var accounts = await db.LedgerAccounts.AsNoTracking()
        .Select(account => new { account.Id, account.BalanceMinor })
        .ToListAsync(ct);

    var balanceMismatches = accounts.Count(account =>
        account.BalanceMinor != projected.GetValueOrDefault(account.Id));
    var unexplainedVariance = accounts.Sum(account =>
        Math.Abs(account.BalanceMinor - projected.GetValueOrDefault(account.Id)));

    var paymentMismatches = await db.Payments.AsNoTracking().CountAsync(payment =>
        !db.LedgerTransactions.Any(transaction =>
            transaction.Id == payment.PostedLedgerTransactionId
            && transaction.PaymentId == payment.Id
            && transaction.Kind == LedgerTransactionKind.Payment
            && transaction.Entries.Count == 2
            && transaction.Entries.All(entry =>
                entry.AmountMinor == payment.AmountMinor && entry.Currency == payment.Currency))
        || (payment.Status == PaymentStatus.Reversed
            && (payment.ReversalLedgerTransactionId == null
                || !db.LedgerTransactions.Any(transaction =>
                    transaction.Id == payment.ReversalLedgerTransactionId
                    && transaction.ReversesTransactionId == payment.PostedLedgerTransactionId))),
        ct);

    var fundingMismatches = await db.FundingTransfers.AsNoTracking().CountAsync(transfer =>
        !db.LedgerTransactions.Any(transaction =>
            transaction.Id == transfer.LedgerTransactionId
            && transaction.FundingTransferId == transfer.Id
            && transaction.Kind == (transfer.Direction == FundingDirection.CashIn
                ? LedgerTransactionKind.FundingIn
                : LedgerTransactionKind.FundingOut)
            && transaction.Entries.Count == 2
            && transaction.Entries.All(entry =>
                entry.AmountMinor == transfer.AmountMinor && entry.Currency == transfer.Currency))
        || !db.SandboxProviderTransfers.Any(provider =>
            provider.FundingTransferId == transfer.Id
            && provider.ProviderTransferId == transfer.ProviderTransferId
            && provider.AmountMinor == transfer.AmountMinor
            && provider.Direction == transfer.Direction
            && provider.Currency == transfer.Currency
            && provider.Status == "posted"),
        ct);

    fundingMismatches += await db.SandboxProviderTransfers.AsNoTracking()
        .CountAsync(provider => !db.FundingTransfers.Any(transfer => transfer.Id == provider.FundingTransferId), ct);

    var clean = unbalanced == 0
        && balanceMismatches == 0
        && paymentMismatches == 0
        && fundingMismatches == 0
        && unexplainedVariance == 0;

    var run = new ReconciliationRun
    {
      StartedAt = started,
      CompletedAt = clock.GetUtcNow(),
      Status = clean ? "reconciled" : "variance_detected",
      UnbalancedTransactions = unbalanced,
      BalanceMismatches = balanceMismatches,
      PaymentMismatches = paymentMismatches,
      FundingMismatches = fundingMismatches,
      UnexplainedVarianceMinor = unexplainedVariance
    };

    db.ReconciliationRuns.Add(run);
    await db.SaveChangesAsync(ct);
    return Map(run);
  }

  public async Task<ReconciliationResponse?> LatestAsync(CancellationToken ct)
  {
    var run = await db.ReconciliationRuns.AsNoTracking()
        .OrderByDescending(value => value.CompletedAt)
        .FirstOrDefaultAsync(ct);

    return run is null ? null : Map(run);
  }

  private static ReconciliationResponse Map(ReconciliationRun run) =>
      new(
          run.Id,
          run.Status,
          run.UnbalancedTransactions,
          run.BalanceMismatches,
          run.PaymentMismatches,
          run.FundingMismatches,
          run.UnexplainedVarianceMinor,
          run.StartedAt,
          run.CompletedAt);
}
