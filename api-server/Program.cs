using Npgsql;
using Senzy.Api.Banking;
using Senzy.Api.Features.Health;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IBankingProvider, StubBankingProvider>();

var connectionString = builder.Configuration.GetConnectionString("Postgres");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
}

var app = builder.Build();

app.MapHealthRoutes();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapBankingRoutes();
}

app.Run();

public partial class Program;
