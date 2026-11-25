using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Senzy.Api.Infrastructure.Persistence;
using Senzy.Api.Infrastructure.Persistence.Models;
using Senzy.Infrastructure.Banking;
using Senzy.Modules.Funding;
using Xunit;

namespace Senzy.Api.Tests;

public sealed class DurableFundingTests
{
  [PostgresFact]
  public async Task FundingStartsPendingAndPostsExactlyOnceAfterProviderSuccess()
  {
    await using var host = await DatabaseHost.Create();
    using var client = host.Factory.CreateClient();
    await RegisterAndLogin(client, "durable-funding@example.test", "durable_funding");
    var accountId = await LinkAccount(client);

    var response = await Post(client, accountId, Key());
    Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    var pending = await response.Content.ReadFromJsonAsync<JsonElement>();
    Assert.Equal("pending", pending.GetProperty("status").GetString());
    Assert.Equal(JsonValueKind.Null, pending.GetProperty("providerTransferId").ValueKind);

    await Process(host, pending.GetProperty("id").GetGuid());
    await Process(host, pending.GetProperty("id").GetGuid());

    await using var scope = host.Factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<SenzyDbContext>();
    var transfer = await db.FundingTransfers.SingleAsync();
    Assert.Equal(FundingPersistenceStatus.Posted, transfer.Status);
    Assert.Equal(FundingPersistenceProviderOutcome.Succeeded, transfer.ProviderOutcome);
    Assert.NotNull(transfer.ProviderTransferId);
    Assert.Single(await db.LedgerTransactions.Where(value => value.FundingTransferId == transfer.Id).ToListAsync());
    Assert.Single(await db.OutboxMessages.Where(value => value.AggregateId == transfer.Id).ToListAsync());
    Assert.Single(await db.SandboxProviderTransfers.Where(value => value.FundingTransferId == transfer.Id).ToListAsync());
  }

  [PostgresFact]
  public async Task ProviderFailureMarksFundingFailedWithoutPostingLedger()
  {
    await using var host = await DatabaseHost.Create(services =>
        services.Configure<StubBankingOptions>(options => options.FundingOutcome = StubFundingOutcome.Failed));
    using var client = host.Factory.CreateClient();
    await RegisterAndLogin(client, "durable-funding-failed@example.test", "durable_failed");
    var accountId = await LinkAccount(client);
    var response = await Post(client, accountId, Key());
    var pending = await response.Content.ReadFromJsonAsync<JsonElement>();

    await Process(host, pending.GetProperty("id").GetGuid());

    await using var scope = host.Factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<SenzyDbContext>();
    var transfer = await db.FundingTransfers.SingleAsync();
    Assert.Equal(FundingPersistenceStatus.Failed, transfer.Status);
    Assert.Equal(FundingPersistenceProviderOutcome.Failed, transfer.ProviderOutcome);
    Assert.Empty(await db.LedgerTransactions.Where(value => value.FundingTransferId == transfer.Id).ToListAsync());
    Assert.Empty(await db.OutboxMessages.Where(value => value.AggregateId == transfer.Id).ToListAsync());
  }

  [PostgresFact]
  public async Task UnknownProviderOutcomeIsReconciledWithoutASecondTransfer()
  {
    await using var host = await DatabaseHost.Create(services =>
        services.Configure<StubBankingOptions>(options => options.FundingOutcome = StubFundingOutcome.Unknown));
    using var client = host.Factory.CreateClient();
    await RegisterAndLogin(client, "durable-funding-unknown@example.test", "durable_unknown");
    var accountId = await LinkAccount(client);
    var response = await Post(client, accountId, Key());
    var pending = await response.Content.ReadFromJsonAsync<JsonElement>();
    var transferId = pending.GetProperty("id").GetGuid();

    await Process(host, transferId);
    await using (var optionsScope = host.Factory.Services.CreateAsyncScope())
    {
      optionsScope.ServiceProvider.GetRequiredService<IOptions<StubBankingOptions>>().Value.FundingOutcome = StubFundingOutcome.Succeeded;
    }

    await using (var scope = host.Factory.Services.CreateAsyncScope())
    {
      await scope.ServiceProvider.GetRequiredService<Senzy.Api.Features.Reconciliation.ReconciliationService>()
          .RunAsync(default);
    }

    await using var finalScope = host.Factory.Services.CreateAsyncScope();
    var db = finalScope.ServiceProvider.GetRequiredService<SenzyDbContext>();
    var transfer = await db.FundingTransfers.SingleAsync();
    Assert.Equal(FundingPersistenceStatus.Posted, transfer.Status);
    Assert.Single(await db.LedgerTransactions.Where(value => value.FundingTransferId == transferId).ToListAsync());
    Assert.Single(await db.SandboxProviderTransfers.Where(value => value.FundingTransferId == transferId).ToListAsync());
  }

  private static async Task Process(DatabaseHost host, Guid transferId)
  {
    await using var scope = host.Factory.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<FundingService>().ProcessPendingAsync(transferId, default);
  }

  private static async Task<HttpResponseMessage> Post(HttpClient client, Guid accountId, string key)
  {
    var request = new HttpRequestMessage(HttpMethod.Post, "/v1/funding-transfers/")
    {
      Content = JsonContent.Create(new
      {
        externalAccountId = accountId,
        direction = "cash_in",
        amountMinor = 10_000,
        currency = "USD"
      })
    };
    request.Headers.Add("Idempotency-Key", key);
    return await client.SendAsync(request);
  }

  private static async Task RegisterAndLogin(HttpClient client, string email, string handle)
  {
    const string password = "example-test-passphrase";
    Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/v1/auth/register",
        new { email, password, displayName = handle, handle })).StatusCode);
    var login = await client.PostAsJsonAsync("/v1/auth/login", new { email, password });
    var token = await login.Content.ReadFromJsonAsync<JsonElement>();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
        "Bearer",
        token.GetProperty("accessToken").GetString());
  }

  private static async Task<Guid> LinkAccount(HttpClient client)
  {
    var session = await (await client.PostAsJsonAsync("/v1/banking/link-sessions", new { }))
        .Content.ReadFromJsonAsync<JsonElement>();
    var response = await client.PostAsJsonAsync("/v1/banking/accounts", new
    {
      linkToken = session.GetProperty("linkToken").GetString(),
      institutionId = "stub-first-national",
      displayName = "Sandbox checking",
      accountKind = "checking"
    });
    return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
  }

  private static string Key() => Guid.NewGuid().ToString("N");
}
