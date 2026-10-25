using Microsoft.EntityFrameworkCore;
using Senzy.Api.Features.Reconciliation;
using Senzy.Api.Infrastructure.Persistence;

namespace Senzy.Api.Features.Reconciliation;

public sealed class ReconciliationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ReconciliationWorker> logger) : BackgroundService
{
  private const long ReconciliationLockKey = 7_391_204;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    try
    {
      await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
    }
    catch (OperationCanceledException)
    {
      return;
    }

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SenzyDbContext>();
        await db.Database.OpenConnectionAsync(stoppingToken);

        try
        {
          await using var command = db.Database.GetDbConnection().CreateCommand();
          command.CommandText = "SELECT pg_try_advisory_lock(@lock_key)";
          var parameter = command.CreateParameter();
          parameter.ParameterName = "lock_key";
          parameter.Value = ReconciliationLockKey;
          command.Parameters.Add(parameter);

          var acquired = (bool?)await command.ExecuteScalarAsync(stoppingToken) ?? false;
          if (!acquired)
          {
            logger.LogInformation("Reconciliation is already running on another worker");
            continue;
          }

          await scope.ServiceProvider
              .GetRequiredService<ReconciliationService>()
              .RunAsync(stoppingToken);

          await using var release = db.Database.GetDbConnection().CreateCommand();
          release.CommandText = "SELECT pg_advisory_unlock(@lock_key)";
          var releaseParameter = release.CreateParameter();
          releaseParameter.ParameterName = "lock_key";
          releaseParameter.Value = ReconciliationLockKey;
          release.Parameters.Add(releaseParameter);
          await release.ExecuteScalarAsync(stoppingToken);
        }
        finally
        {
          await db.Database.CloseConnectionAsync();
        }
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
      {
      }
      catch (Exception error)
      {
        logger.LogError(error, "Daily reconciliation failed");
      }

      try
      {
        await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
      }
      catch (OperationCanceledException)
      {
        return;
      }
    }
  }
}
