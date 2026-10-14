using Senzy.Api.Features.Ledger.Domain;
using Senzy.Api.Infrastructure.Persistence;

namespace Senzy.Api.Features.Ledger.Application;

public sealed class SandboxWalletService(SenzyDbContext db, LedgerPostingService ledger, TimeProvider clock, IConfiguration configuration)
{
    public const string Currency = "USD";

    public void CreateOpeningBalance(Guid userId)
    {
        var now = clock.GetUtcNow();
        var amount = configuration.GetValue("Sandbox:OpeningBalanceMinor", 100_000L);
        if (amount is <= 0 or > 10_000_000) throw new InvalidOperationException("Sandbox opening balance is invalid.");
        var source = new LedgerAccount
        {
            OwnerUserId = userId, Purpose = LedgerAccountPurpose.SandboxIssuance,
            Currency = Currency, AllowNegative = true, CreatedAt = now
        };
        var wallet = new LedgerAccount
        {
            OwnerUserId = userId, Purpose = LedgerAccountPurpose.Wallet,
            Currency = Currency, AllowNegative = false, CreatedAt = now
        };
        db.LedgerAccounts.AddRange(source, wallet);
        db.LedgerTransactions.Add(ledger.Post(source, wallet, amount, LedgerTransactionKind.SandboxOpeningBalance, now));
    }
}
