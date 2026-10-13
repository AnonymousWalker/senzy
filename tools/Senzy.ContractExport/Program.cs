using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Senzy.Api.Banking;

namespace Senzy.ContractExport;

internal static class Export
{
    public static async Task Main(string[] args)
    {
        if (args.Length != 2)
            throw new ArgumentException("Provide the OpenAPI and response sample output paths.");
        await using var factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseContentRoot(Path.GetFullPath("../../../../../api-server", AppContext.BaseDirectory));
            builder.ConfigureLogging(logging => logging.ClearProviders());
        });
        using var client = factory.CreateClient();
        var document = await client.GetStringAsync("/openapi/v1.json");
        await File.WriteAllTextAsync(args[0], document);
        var userId = Guid.NewGuid();
        // Authenticated endpoint behavior is tested with real PostgreSQL. Export stays database-independent.
        var provider = factory.Services.GetRequiredService<IBankingProvider>();
        var session = provider.CreateLinkSession(userId);
        var samples = new
        {
            health = await client.GetFromJsonAsync<JsonElement>("/health"),
            institutions = await client.GetFromJsonAsync<JsonElement>("/v1/banking/institutions"),
            session,
            account = provider.ConnectAccount(userId, session.LinkToken, "stub-first-national", "Contract sample", "checking")
        };
        await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(samples, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
