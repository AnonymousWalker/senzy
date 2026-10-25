using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Senzy.Api.Infrastructure.Persistence;
using Xunit;

namespace Senzy.Api.Tests;

public sealed class IdentityTests
{
  [PostgresFact]
  public async Task RegistrationProfileOwnershipRefreshAndLogoutWorkAcrossSessions()
  {
    await using var host = await DatabaseHost.Create();
    using var alice = host.Factory.CreateClient();
    using var bob = host.Factory.CreateClient();
    Assert.Equal(HttpStatusCode.Unauthorized, (await alice.GetAsync("/v1/profile/")).StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, (await alice.GetAsync("/v1/activity")).StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, (await alice.PostAsJsonAsync("/v1/banking/link-sessions", new { })).StatusCode);
    var aliceProfile = await Register(alice, "alice@example.test", "AliceHandle");
    var duplicate = await bob.PostAsJsonAsync("/v1/auth/register", Registration("bob@example.test", "alicehandle"));
    Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    // Failed registration rolled back the user too, so this same email can succeed.
    await Register(bob, "bob@example.test", "bobhandle");
    var token = await Login(alice, "alice@example.test");
    await Login(bob, "bob@example.test");
    var me = await alice.GetFromJsonAsync<JsonElement>("/v1/profile/");
    Assert.Equal("alicehandle", me.GetProperty("handle").GetString());
    Assert.Equal(aliceProfile.GetProperty("userId").GetGuid(), me.GetProperty("userId").GetGuid());
    Assert.Equal(HttpStatusCode.Conflict, (await alice.PutAsJsonAsync("/v1/profile/", new { displayName = "Alice", handle = "BOBHANDLE" })).StatusCode);
    var saved = await alice.PutAsJsonAsync("/v1/profile/", new { displayName = "Alice updated", handle = "alice_new" });
    Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
    using var secondAliceSession = host.Factory.CreateClient();
    await Login(secondAliceSession, "alice@example.test");
    var persisted = await secondAliceSession.GetFromJsonAsync<JsonElement>("/v1/profile/");
    Assert.Equal("Alice updated", persisted.GetProperty("displayName").GetString());
    var bobMe = await bob.GetFromJsonAsync<JsonElement>("/v1/profile/");
    Assert.Equal("bobhandle", bobMe.GetProperty("handle").GetString());
    var activity = await alice.GetFromJsonAsync<JsonElement>("/v1/activity?userId=" + bobMe.GetProperty("userId").GetString());
    Assert.Equal(aliceProfile.GetProperty("userId").GetGuid(), activity.GetProperty("userId").GetGuid());
    Assert.True(activity.GetProperty("isFixture").GetBoolean());
    var sessionResponse = await alice.PostAsJsonAsync("/v1/banking/link-sessions", new { });
    var session = await sessionResponse.Content.ReadFromJsonAsync<JsonElement>();
    var account = new { linkToken = session.GetProperty("linkToken").GetString(), institutionId = "stub-first-national", displayName = "Test", accountKind = "checking" };
    Assert.Equal(HttpStatusCode.BadRequest, (await bob.PostAsJsonAsync("/v1/banking/accounts", account)).StatusCode);
    Assert.Equal(HttpStatusCode.Created, (await alice.PostAsJsonAsync("/v1/banking/accounts", account)).StatusCode);
    var bobAccounts = await bob.GetFromJsonAsync<JsonElement>("/v1/banking/accounts");
    Assert.Empty(bobAccounts.GetProperty("accounts").EnumerateArray());
    using var refreshClient = host.Factory.CreateClient();
    var refreshed = await refreshClient.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = token.GetProperty("refreshToken").GetString() });
    Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
    Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsJsonAsync("/v1/auth/logout", new { })).StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, (await secondAliceSession.GetAsync("/v1/profile/")).StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, (await refreshClient.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = token.GetProperty("refreshToken").GetString() })).StatusCode);
    Assert.Equal(HttpStatusCode.OK, (await bob.GetAsync("/v1/profile/")).StatusCode);
  }

  [PostgresFact]
  public async Task BrowserCookiesRequireCsrfAndAllowedOrigin()
  {
    await using var host = await DatabaseHost.Create();
    using var browser = host.Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost:4001"), HandleCookies = true });
    await Register(browser, "browser@example.test", "browser_user");
    var credentials = new { email = "browser@example.test", password = Password };
    Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsJsonAsync("/v1/auth/login?useCookies=true", credentials)).StatusCode);
    var csrf = await browser.GetFromJsonAsync<JsonElement>("/v1/auth/csrf");
    browser.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
    var login = await browser.PostAsJsonAsync("/v1/auth/login?useCookies=true", credentials);
    Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    Assert.Contains(login.Headers.GetValues("Set-Cookie"), value => value.Contains("senzy.session=") && value.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/v1/profile/")).StatusCode);
    browser.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
    Assert.Equal(HttpStatusCode.BadRequest, (await browser.PutAsJsonAsync("/v1/profile/", new { displayName = "Test", handle = "browser_new" })).StatusCode);
    csrf = await browser.GetFromJsonAsync<JsonElement>("/v1/auth/csrf");
    browser.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
    browser.DefaultRequestHeaders.Add("Origin", "https://untrusted.example");
    Assert.Equal(HttpStatusCode.Forbidden, (await browser.PutAsJsonAsync("/v1/profile/", new { displayName = "Test", handle = "browser_new" })).StatusCode);
    browser.DefaultRequestHeaders.Remove("Origin");
    Assert.Equal(HttpStatusCode.OK, (await browser.PutAsJsonAsync("/v1/profile/", new { displayName = "Test", handle = "browser_new" })).StatusCode);
    Assert.Equal(HttpStatusCode.NoContent, (await browser.PostAsJsonAsync("/v1/auth/logout", new { })).StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/v1/profile/")).StatusCode);
  }

  [PostgresFact]
  public async Task ValidationFlagsAndTelemetryHaveConsistentErrors()
  {
    await using var host = await DatabaseHost.Create();
    using var client = host.Factory.CreateClient();
    var invalid = await client.PostAsJsonAsync("/v1/auth/register", Registration("not-an-email", "no"));
    Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType?.MediaType);
    var problem = await invalid.Content.ReadFromJsonAsync<JsonElement>();
    Assert.True(problem.TryGetProperty("code", out _));
    Assert.Equal(invalid.Headers.GetValues("X-Correlation-ID").Single(), problem.GetProperty("traceId").GetString());
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = "invalid" })).StatusCode);
    await Register(client, "flags@example.test", "flags_user");
    await Login(client, "flags@example.test");
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/v1/profile/", new { displayName = "Spoof", handle = "valid_handle", userId = Guid.NewGuid() })).StatusCode);
    Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/v1/telemetry", new { @event = "client_error", screen = "app" })).StatusCode);
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/v1/telemetry", new { @event = "secret text", screen = "app" })).StatusCode);
    using var disabled = host.Factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
    { ["Features:Registration"] = "false", ["Features:Activity"] = "false" })));
    using var disabledClient = disabled.CreateClient();
    Assert.Equal(HttpStatusCode.Forbidden, (await disabledClient.PostAsJsonAsync("/v1/auth/register", Registration("disabled@example.test", "disabled_user"))).StatusCode);
  }

  [PostgresFact]
  public async Task RegistrationAcceptsEightCharacterPasswordsAndRejectsShorterPasswords()
  {
    await using var host = await DatabaseHost.Create();
    using var client = host.Factory.CreateClient();
    var accepted = await client.PostAsJsonAsync("/v1/auth/register", new
    {
      email = "eight@example.test",
      password = "eight123",
      displayName = "Eight",
      handle = "eight_user"
    });
    Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    var rejected = await client.PostAsJsonAsync("/v1/auth/register", new
    {
      email = "short@example.test",
      password = "short7",
      displayName = "Short",
      handle = "short_user"
    });
    Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
  }

  [PostgresFact]
  public async Task ConcurrentRegistrationsEnforceUniqueHandlesAndLockoutBlocksWrongPasswords()
  {
    await using var host = await DatabaseHost.Create();
    using var client = host.Factory.CreateClient();
    var attempts = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => client.PostAsJsonAsync("/v1/auth/register", Registration($"race{i}@example.test", "same_handle"))));
    Assert.Single(attempts, response => response.StatusCode == HttpStatusCode.Created);
    Assert.Equal(3, attempts.Count(response => response.StatusCode == HttpStatusCode.Conflict));
    await Register(client, "lock@example.test", "lock_user");
    for (var i = 0; i < 5; i++)
      Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/v1/auth/login", new { email = "lock@example.test", password = "wrong-password" })).StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/v1/auth/login", new { email = "lock@example.test", password = Password })).StatusCode);
  }

  private const string Password = "example-test-passphrase";
  private static object Registration(string email, string handle) => new { email, password = Password, displayName = "Test Member", handle };
  private static async Task<JsonElement> Register(HttpClient client, string email, string handle)
  {
    var response = await client.PostAsJsonAsync("/v1/auth/register", Registration(email, handle));
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    return await response.Content.ReadFromJsonAsync<JsonElement>();
  }
  private static async Task<JsonElement> Login(HttpClient client, string email)
  {
    var response = await client.PostAsJsonAsync("/v1/auth/login", new { email, password = Password });
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var token = await response.Content.ReadFromJsonAsync<JsonElement>();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.GetProperty("accessToken").GetString());
    return token;
  }
}

internal sealed class DatabaseHost : IAsyncDisposable
{
  public required WebApplicationFactory<Program> Factory { get; init; }
  private string Connection { get; init; } = "";
  private string Schema { get; init; } = "";
  public static async Task<DatabaseHost> Create(Action<IServiceCollection>? configure = null)
  {
    var connection = Environment.GetEnvironmentVariable("SENZY_TEST_POSTGRES") ?? throw new InvalidOperationException("SENZY_TEST_POSTGRES is required.");
    var schema = "senzy_test_" + Guid.NewGuid().ToString("N");
    await using var admin = NpgsqlDataSource.Create(connection);
    await using var command = admin.CreateCommand($"CREATE SCHEMA {schema}");
    await command.ExecuteNonQueryAsync();
    var scoped = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema }.ConnectionString;
    var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
      builder.UseEnvironment("Development").UseContentRoot(Path.GetFullPath("../../../../../api-server", AppContext.BaseDirectory));
      builder.ConfigureLogging(logging => logging.ClearProviders());
      builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["RateLimits:AuthPermitLimit"] = "1000" }));
      builder.ConfigureServices(services =>
          {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.RemoveAll<DbContextOptions<SenzyDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SenzyDbContext>>();
            services.AddDbContext<SenzyDbContext>(options => options.UseNpgsql(scoped));
            configure?.Invoke(services);
          });
    });
    await using var scope = factory.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<SenzyDbContext>().Database.MigrateAsync();
    return new DatabaseHost { Factory = factory, Connection = connection, Schema = schema };
  }
  public async ValueTask DisposeAsync()
  {
    await Factory.DisposeAsync();
    await using var admin = NpgsqlDataSource.Create(Connection);
    await using var command = admin.CreateCommand($"DROP SCHEMA {Schema} CASCADE");
    await command.ExecuteNonQueryAsync();
  }
}
