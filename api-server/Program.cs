using Npgsql;
using Senzy.Api.Banking;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IBankingProvider, StubBankingProvider>();

var connectionString = builder.Configuration.GetConnectionString("Postgres");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
}

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "senzy-api" }))
    .WithName("GetHealth");

app.MapGet("/health/ready", async (IServiceProvider services, CancellationToken cancellationToken) =>
{
    var dataSource = services.GetService<NpgsqlDataSource>();
    if (dataSource is null)
    {
        return Results.Problem("PostgreSQL is not configured.", statusCode: 503);
    }

    try
    {
        await using var command = dataSource.CreateCommand("SELECT 1");
        await command.ExecuteScalarAsync(cancellationToken);
        return Results.Ok(new { status = "ready" });
    }
    catch (Exception error) when (error is NpgsqlException or TimeoutException)
    {
        return Results.Problem("PostgreSQL is unavailable.", statusCode: 503);
    }
}).WithName("GetReadiness");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapBankingRoutes();
}

app.Run();
