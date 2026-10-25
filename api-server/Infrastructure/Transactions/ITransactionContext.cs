namespace Senzy.Infrastructure.Transactions;

public interface ITransactionContext
{
  Task BeginAsync(CancellationToken ct);
  Task CommitAsync(CancellationToken ct);
  Task RollbackAsync(CancellationToken ct);
}
