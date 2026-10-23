using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Senzy.Api.Common;
using Senzy.Api.Features.Operations;
using Senzy.Api.Infrastructure.Outbox;
using Senzy.Api.Infrastructure.Persistence;
using Senzy.Api.Infrastructure.Persistence.Models;
using Xunit;

namespace Senzy.Api.Tests;

public sealed class Phase3Tests
{
    [PostgresFact]
    public async Task FundingIsBalancedIdempotentPrivateAndReconciles()
    {
        await using var host = await DatabaseHost.Create();
        using var alice = host.Factory.CreateClient();
        using var bob = host.Factory.CreateClient();
        await RegisterAndLogin(alice, "alice-funding@example.test", "alice_funding");
        await RegisterAndLogin(bob, "bob-funding@example.test", "bob_funding");
        var accountId = await LinkAccount(alice);
        var key = Key();
        var cashIn = await Post(alice, "/v1/funding-transfers/", new
        {
            externalAccountId = accountId, direction = "cash_in", amountMinor = 25_000, currency = "USD"
        }, key);
        Assert.Equal(HttpStatusCode.Created, cashIn.StatusCode);
        var transfer = await cashIn.Content.ReadFromJsonAsync<JsonElement>();
        var replay = await Post(alice, "/v1/funding-transfers/", new
        {
            externalAccountId = accountId, direction = "cash_in", amountMinor = 25_000, currency = "USD"
        }, key);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(transfer.GetProperty("id").GetGuid(), (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict, (await Post(alice, "/v1/funding-transfers/", new
        {
            externalAccountId = accountId, direction = "cash_in", amountMinor = 25_001, currency = "USD"
        }, key)).StatusCode);
        Assert.Equal(125_000, (await alice.GetFromJsonAsync<JsonElement>("/v1/wallet/")).GetProperty("availableMinor").GetInt64());
        Assert.Empty((await bob.GetFromJsonAsync<JsonElement>("/v1/funding-transfers/")).GetProperty("items").EnumerateArray());
        var history = await alice.GetFromJsonAsync<JsonElement>("/v1/operations/transactions");
        var historyItem = Assert.Single(history.GetProperty("items").EnumerateArray());
        Assert.Equal("cash_in", historyItem.GetProperty("kind").GetString());
        Assert.Equal(25_000, historyItem.GetProperty("amountMinor").GetInt64());

        await using var scope = host.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SenzyDbContext>();
        var stored = await db.FundingTransfers.SingleAsync();
        var provider = await db.SandboxProviderTransfers.SingleAsync();
        Assert.Equal(stored.ProviderTransferId, provider.ProviderTransferId);
        var entries = await db.LedgerEntries.Where(entry => entry.LedgerTransactionId == stored.LedgerTransactionId).ToListAsync();
        Assert.Equal(entries.Where(entry => entry.Direction == LedgerEntryDirection.Debit).Sum(entry => entry.AmountMinor),
            entries.Where(entry => entry.Direction == LedgerEntryDirection.Credit).Sum(entry => entry.AmountMinor));
        var reconciliation = await scope.ServiceProvider.GetRequiredService<ReconciliationService>().RunAsync(default);
        Assert.Equal("reconciled", reconciliation.Status);
        Assert.Equal(0, reconciliation.UnexplainedVarianceMinor);
    }

    [PostgresFact]
    public async Task FreezeAndDailyLimitBlockOutboundMoneyMovement()
    {
        await using var host = await DatabaseHost.Create();
        using var alice = host.Factory.CreateClient();
        using var bob = host.Factory.CreateClient();
        await RegisterAndLogin(alice, "alice-controls@example.test", "alice_controls");
        await RegisterAndLogin(bob, "bob-controls@example.test", "bob_controls");
        var accountId = await LinkAccount(alice);
        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsJsonAsync("/v1/operations/freeze", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(alice, "/v1/payments/", new
        {
            recipientHandle = "bob_controls", amountMinor = 100, currency = "USD", note = "blocked"
        }, Key())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(alice, "/v1/funding-transfers/", new
        {
            externalAccountId = accountId, direction = "cash_out", amountMinor = 100, currency = "USD"
        }, Key())).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsJsonAsync("/v1/operations/unfreeze", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Post(alice, "/v1/funding-transfers/", new
        {
            externalAccountId = accountId, direction = "cash_in", amountMinor = 1_000_000, currency = "USD"
        }, Key())).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Post(alice, "/v1/payments/", new
        {
            recipientHandle = "bob_controls", amountMinor = 1_000_000, currency = "USD", note = "limit"
        }, Key())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(alice, "/v1/funding-transfers/", new
        {
            externalAccountId = accountId, direction = "cash_out", amountMinor = 1, currency = "USD"
        }, Key())).StatusCode);
        var status = await alice.GetFromJsonAsync<JsonElement>("/v1/operations/status");
        Assert.False(status.GetProperty("isFrozen").GetBoolean());
        Assert.Equal(1_000_000, status.GetProperty("dailyOutboundLimitMinor").GetInt64());
    }

    [PostgresFact]
    public async Task OutboxCreatesIdempotentPrivateNotifications()
    {
        await using var host = await DatabaseHost.Create();
        using var alice = host.Factory.CreateClient();
        using var bob = host.Factory.CreateClient();
        await RegisterAndLogin(alice, "alice-notify@example.test", "alice_notify");
        await RegisterAndLogin(bob, "bob-notify@example.test", "bob_notify");
        Assert.Equal(HttpStatusCode.Created, (await Post(alice, "/v1/payments/", new
        {
            recipientHandle = "bob_notify", amountMinor = 500, currency = "USD", note = "notice"
        }, Key())).StatusCode);

        await using (var scope = host.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SenzyDbContext>();
            var messageId = await db.OutboxMessages.Where(message => message.Type == "payment.posted").Select(message => message.Id).SingleAsync();
            var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
            await dispatcher.DispatchAsync(messageId, default);
            await dispatcher.DispatchAsync(messageId, default);
        }
        var aliceItems = (await alice.GetFromJsonAsync<JsonElement>("/v1/notifications/")).GetProperty("items").EnumerateArray().ToList();
        var bobItems = (await bob.GetFromJsonAsync<JsonElement>("/v1/notifications/")).GetProperty("items").EnumerateArray().ToList();
        Assert.Single(aliceItems);
        Assert.Single(bobItems);
        Assert.Equal("payment_sent", aliceItems[0].GetProperty("kind").GetString());
        Assert.Equal("payment_received", bobItems[0].GetProperty("kind").GetString());
        Assert.Equal(HttpStatusCode.NotFound,
            (await bob.PostAsJsonAsync($"/v1/notifications/{aliceItems[0].GetProperty("id").GetGuid()}/read", new { })).StatusCode);
    }

    [PostgresFact]
    public async Task ConcurrentCashOutCannotOverspend()
    {
        await using var host = await DatabaseHost.Create();
        using var alice = host.Factory.CreateClient();
        await RegisterAndLogin(alice, "alice-funding-race@example.test", "alice_funding_race");
        var accountId = await LinkAccount(alice);
        var attempts = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Post(alice, "/v1/funding-transfers/", new
        {
            externalAccountId = accountId, direction = "cash_out", amountMinor = 70_000, currency = "USD"
        }, Key())));
        Assert.Single(attempts, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(attempts, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(30_000, (await alice.GetFromJsonAsync<JsonElement>("/v1/wallet/")).GetProperty("availableMinor").GetInt64());
    }

    [PostgresFact]
    public async Task FundingFailureBeforeCommitRollsBackEveryRecordAndBalance()
    {
        await using var host = await DatabaseHost.Create(services =>
        {
            services.RemoveAll<IFundingFailureInjector>();
            services.AddScoped<IFundingFailureInjector, ThrowBeforeFundingCommit>();
        });
        using var alice = host.Factory.CreateClient();
        await RegisterAndLogin(alice, "alice-funding-fail@example.test", "alice_funding_fail");
        var accountId = await LinkAccount(alice);
        Assert.Equal(HttpStatusCode.InternalServerError, (await Post(alice, "/v1/funding-transfers/", new
        {
            externalAccountId = accountId, direction = "cash_in", amountMinor = 10_000, currency = "USD"
        }, Key())).StatusCode);
        Assert.Equal(100_000, (await alice.GetFromJsonAsync<JsonElement>("/v1/wallet/")).GetProperty("availableMinor").GetInt64());
        await using var scope = host.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SenzyDbContext>();
        Assert.Empty(await db.FundingTransfers.ToListAsync());
        Assert.Empty(await db.SandboxProviderTransfers.ToListAsync());
        Assert.Empty(await db.LedgerTransactions.Where(transaction => transaction.Kind == LedgerTransactionKind.FundingIn).ToListAsync());
        Assert.Empty(await db.IdempotencyRecords.Where(record => record.FundingTransferId != null).ToListAsync());
        Assert.Empty(await db.OutboxMessages.Where(message => message.Type == "funding.posted").ToListAsync());
    }

    private const string Password = "example-test-passphrase";
    private static string Key() => Guid.NewGuid().ToString("N");

    private static async Task RegisterAndLogin(HttpClient client, string email, string handle)
    {
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/v1/auth/register",
            new { email, password = Password, displayName = handle, handle })).StatusCode);
        var login = await client.PostAsJsonAsync("/v1/auth/login", new { email, password = Password });
        var token = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.GetProperty("accessToken").GetString());
    }

    private static async Task<Guid> LinkAccount(HttpClient client)
    {
        var session = await (await client.PostAsJsonAsync("/v1/banking/link-sessions", new { })).Content.ReadFromJsonAsync<JsonElement>();
        var response = await client.PostAsJsonAsync("/v1/banking/accounts", new
        {
            linkToken = session.GetProperty("linkToken").GetString(), institutionId = "stub-first-national",
            displayName = "Sandbox checking", accountKind = "checking"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> Post(HttpClient client, string path, object body, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request);
    }

    private sealed class ThrowBeforeFundingCommit : IFundingFailureInjector
    {
        public Task BeforeCommitAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("Injected funding failure");
    }
}
