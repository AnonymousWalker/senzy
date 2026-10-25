namespace Senzy.Infrastructure.Transactions;

public interface ITransactionContext
{
  Task<ITransactionScope> BeginAsync(CancellationToken ct);
}

public interface ITransactionScope : IAsyncDisposable
{
  Task CommitAsync(CancellationToken ct);
  Task RollbackAsync(CancellationToken ct);
}
