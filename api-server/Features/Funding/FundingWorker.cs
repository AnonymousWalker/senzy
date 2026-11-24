using Senzy.Modules.Funding;

namespace Senzy.Api.Features.Funding;

public sealed class FundingWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<FundingWorker> logger) : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<FundingService>()
            .ProcessPendingAsync(stoppingToken);
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
      {
        return;
      }
      catch (Exception error)
      {
        logger.LogError(error, "Funding provider workflow failed");
      }

      try
      {
        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
      }
      catch (OperationCanceledException)
      {
        return;
      }
    }
  }
}
