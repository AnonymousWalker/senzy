using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Senzy.Api.Features.Identity;
using Microsoft.Extensions.Logging;

namespace Senzy.Api.Tests;

public sealed class ApiTests
{
  private static WebApplicationFactory<Program> CreateFactory(string environment = "Development", string? postgres = null) =>
      new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
      {
        builder.UseEnvironment(environment);
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.UseContentRoot(Path.GetFullPath("../../../../../api-server", AppContext.BaseDirectory));
        builder.ConfigureServices(services =>
          {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddAuthentication(options => { options.DefaultAuthenticateScheme = "Test"; options.DefaultChallengeScheme = "Test"; })
                  .AddScheme<AuthenticationSchemeOptions, TestAuthentication>("Test", _ => { });
            services.AddScoped<ISessionValidator, TestSessionValidator>();
            services.RemoveAll<NpgsqlDataSource>();
            if (postgres is not null)
              services.AddSingleton(NpgsqlDataSource.Create(postgres));
          });
      });

  [Fact]
  public async Task HealthWorksWithoutDatabaseAndIncludesTimestamp()
  {
    await using var factory = CreateFactory();
    using var client = factory.CreateClient();
    var health = await client.GetFromJsonAsync<JsonElement>("/health");
    Assert.Equal("ok", health.GetProperty("status").GetString());
    Assert.Equal("senzy-api", health.GetProperty("service").GetString());
    Assert.True(health.GetProperty("timestamp").TryGetDateTimeOffset(out _));
    var ready = await client.GetAsync("/health/ready");
    Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
    Assert.Equal("application/problem+json", ready.Content.Headers.ContentType?.MediaType);
  }

  [Fact]
  public async Task ReadinessFailsWhenDatabaseCannotBeReached()
  {
    await using var factory = CreateFactory(postgres:
        "Host=127.0.0.1;Port=1;Database=senzy;Username=senzy;Password=senzy;Timeout=1");
    using var client = factory.CreateClient();
    Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
  }

  [PostgresFact]
  public async Task ReadinessUsesRealPostgresWhenConfigured()
  {
    var postgres = Environment.GetEnvironmentVariable("SENZY_TEST_POSTGRES");
    if (string.IsNullOrWhiteSpace(postgres))
    {
      Assert.NotEqual("true", Environment.GetEnvironmentVariable("CI"));
      throw new InvalidOperationException("CI must supply SENZY_TEST_POSTGRES.");
    }
    await using var factory = CreateFactory(postgres: postgres);
    using var client = factory.CreateClient();
    Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
  }

  [Theory]
  [InlineData("/v1/banking/institutions")]
  [InlineData("/v1/banking/accounts/11111111-1111-4111-8111-111111111111")]
  [InlineData("/v1/funding-transfers/")]
  [InlineData("/openapi/v1.json")]
  public async Task SandboxAndOpenApiAreAbsentInProduction(string path)
  {
    await using var factory = CreateFactory("Production");
    using var client = factory.CreateClient();
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
  }

  [Fact]
  public async Task InstitutionsAreClearlyMarkedAsSandbox()
  {
    await using var factory = CreateFactory();
    using var client = factory.CreateClient();
    var body = await client.GetFromJsonAsync<JsonElement>("/v1/banking/institutions");
    Assert.Equal("stub-banking", body.GetProperty("provider").GetString());
    Assert.True(body.GetProperty("sandbox").GetBoolean());
    Assert.NotEmpty(body.GetProperty("institutions").EnumerateArray());
  }

  [Theory]
  [InlineData("accountNumber")]
  [InlineData("routingNumber")]
  public async Task RawBankDetailsAreRejected(string field)
  {
    await using var factory = CreateFactory();
    using var client = factory.CreateClient();
    var payload = new Dictionary<string, object> { [field] = "123456789" };
    Assert.Equal(HttpStatusCode.BadRequest,
        (await client.PostAsJsonAsync("/v1/banking/link-sessions", payload)).StatusCode);
    payload["linkToken"] = "stub_link_test";
    payload["institutionId"] = "stub-first-national";
    payload["displayName"] = "Test";
    payload["accountKind"] = "checking";
    Assert.Equal(HttpStatusCode.BadRequest,
        (await client.PostAsJsonAsync("/v1/banking/accounts", payload)).StatusCode);
  }

  [Fact]
  public async Task ClientSuppliedUserIdIsRejected()
  {
    await using var factory = CreateFactory();
    using var client = factory.CreateClient();
    Assert.Equal(HttpStatusCode.BadRequest,
        (await client.PostAsJsonAsync("/v1/banking/link-sessions", new { userId = Guid.Empty })).StatusCode);
  }

  [Fact]
  public async Task LinkTokenIsBoundToUserAndConsumedOnlyOnceUnderConcurrency()
  {
    await using var factory = CreateFactory();
    using var client = factory.CreateClient();
    var userId = Guid.NewGuid();
    client.DefaultRequestHeaders.Add("X-Test-User", userId.ToString());
    var response = await client.PostAsJsonAsync("/v1/banking/link-sessions", new { });
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    var session = await response.Content.ReadFromJsonAsync<JsonElement>();
    var token = session.GetProperty("linkToken").GetString();
    object Payload(string kind = "checking") => new
    {
      linkToken = token,
      institutionId = "stub-first-national",
      displayName = " Test account ",
      accountKind = kind
    };
    client.DefaultRequestHeaders.Remove("X-Test-User");
    Assert.Equal(HttpStatusCode.BadRequest,
        (await client.PostAsJsonAsync("/v1/banking/accounts", Payload())).StatusCode);
    client.DefaultRequestHeaders.Add("X-Test-User", userId.ToString());
    Assert.Equal(HttpStatusCode.BadRequest,
        (await client.PostAsJsonAsync("/v1/banking/accounts", Payload("invalid"))).StatusCode);
    var attempts = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
        client.PostAsJsonAsync("/v1/banking/accounts", Payload())));
    Assert.Single(attempts, result => result.StatusCode == HttpStatusCode.Created);
    Assert.Equal(7, attempts.Count(result => result.StatusCode == HttpStatusCode.BadRequest));
    var accounts = await client.GetFromJsonAsync<JsonElement>("/v1/banking/accounts");
    var account = Assert.Single(accounts.GetProperty("accounts").EnumerateArray());
    Assert.Equal("Test account", account.GetProperty("displayName").GetString());
    Assert.True(account.GetProperty("sandbox").GetBoolean());
    client.DefaultRequestHeaders.Remove("X-Test-User");
    var other = await client.GetFromJsonAsync<JsonElement>("/v1/banking/accounts");
    Assert.Empty(other.GetProperty("accounts").EnumerateArray());
  }
}

public sealed class PostgresFactAttribute : FactAttribute
{
  public PostgresFactAttribute()
  {
    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SENZY_TEST_POSTGRES")) &&
        Environment.GetEnvironmentVariable("CI") != "true")
      Skip = "Set SENZY_TEST_POSTGRES to run the real PostgreSQL readiness test.";
  }
}
