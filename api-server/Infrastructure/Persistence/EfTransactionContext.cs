using Microsoft.EntityFrameworkCore;
using Senzy.Infrastructure.Transactions;

namespace Senzy.Api.Infrastructure.Persistence;

public sealed class EfTransactionContext(SenzyDbContext db) : ITransactionContext
{
  public async Task<ITransactionScope> BeginAsync(CancellationToken ct)
  {
    if (db.Database.CurrentTransaction is not null)
    {
      throw new InvalidOperationException("A transaction is already active.");
    }

    return new EfTransactionScope(await db.Database.BeginTransactionAsync(ct));
  }

  private sealed class EfTransactionScope(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
      : ITransactionScope
  {
    public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);

    public Task RollbackAsync(CancellationToken ct) => transaction.RollbackAsync(ct);

    public ValueTask DisposeAsync() => transaction.DisposeAsync();
  }
}
