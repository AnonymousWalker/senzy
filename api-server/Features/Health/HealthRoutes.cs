using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;

namespace Senzy.Api.Features.Health;

public sealed record HealthResponse(string Status, string Service, DateTimeOffset Timestamp);
public sealed record ReadinessResponse(string Status);

public static class HealthRoutes
{
  public static void MapHealthRoutes(this WebApplication app)
  {
    app.MapGet(
        "/health",
        () => TypedResults.Ok(new HealthResponse("ok", "senzy-api", DateTimeOffset.UtcNow)))
        .WithName("GetHealth")
        .WithTags("Health");

    app.MapGet(
        "/health/ready",
        async Task<Results<Ok<ReadinessResponse>, ProblemHttpResult>> (
            IServiceProvider services,
            CancellationToken cancellationToken) =>
        {
          var dataSource = services.GetService<NpgsqlDataSource>();
          if (dataSource is null)
          {
            return TypedResults.Problem("PostgreSQL is not configured.", statusCode: 503);
          }

          try
          {
            await using var command = dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken);
            return TypedResults.Ok(new ReadinessResponse("ready"));
          }
          catch (Exception error) when (error is NpgsqlException or TimeoutException)
          {
            return TypedResults.Problem("PostgreSQL is unavailable.", statusCode: 503);
          }
        })
        .WithName("GetReadiness")
        .WithTags("Health");
  }
}
