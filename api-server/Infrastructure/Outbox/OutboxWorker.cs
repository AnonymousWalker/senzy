namespace Senzy.Api.Infrastructure.Outbox;

public sealed class OutboxWorker(IServiceScopeFactory scopeFactory, ILogger<OutboxWorker> logger)
    : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        IReadOnlyList<Guid> ids;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
          ids = await scope.ServiceProvider
              .GetRequiredService<OutboxDispatcher>()
              .ClaimAsync(50, stoppingToken);
        }

        foreach (var id in ids)
        {
          await using var scope = scopeFactory.CreateAsyncScope();
          await scope.ServiceProvider
              .GetRequiredService<OutboxDispatcher>()
              .DispatchAsync(id, stoppingToken);
        }

        await Task.Delay(
            ids.Count == 0 ? TimeSpan.FromSeconds(2) : TimeSpan.FromMilliseconds(100),
            stoppingToken);
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
      {
      }
      catch (Exception error)
      {
        logger.LogWarning(error, "Outbox worker batch failed");
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
      }
    }
  }
}
