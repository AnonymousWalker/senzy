using Microsoft.EntityFrameworkCore;
using Senzy.Infrastructure.Transactions;

namespace Senzy.Api.Infrastructure.Persistence;

public sealed class EfTransactionContext(SenzyDbContext db) : ITransactionContext
{
  public async Task BeginAsync(CancellationToken ct)
  {
    if (db.Database.CurrentTransaction is null)
    {
      await db.Database.BeginTransactionAsync(ct);
    }
  }

  public async Task CommitAsync(CancellationToken ct)
  {
    var transaction = db.Database.CurrentTransaction
        ?? throw new InvalidOperationException("No transaction is active.");

    await transaction.CommitAsync(ct);
  }

  public async Task RollbackAsync(CancellationToken ct)
  {
    var transaction = db.Database.CurrentTransaction
        ?? throw new InvalidOperationException("No transaction is active.");

    await transaction.RollbackAsync(ct);
  }
}
