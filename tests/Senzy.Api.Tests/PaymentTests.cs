using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Senzy.Api.Common;
using Senzy.Api.Infrastructure.Persistence;
using Xunit;

namespace Senzy.Api.Tests;

public sealed class PaymentTests
{
    [PostgresFact]
    public async Task SendIsBalancedIdempotentAndRejectsChangedReplay()
    {
        await using var host = await DatabaseHost.Create();
        using var alice = host.Factory.CreateClient();
        using var bob = host.Factory.CreateClient();
        await RegisterAndLogin(alice, "alice-pay@example.test", "alice_pay");
        await RegisterAndLogin(bob, "bob-pay@example.test", "bob_pay");
        Assert.Equal(100_000, (await Wallet(alice)).GetProperty("availableMinor").GetInt64());
        var key = Key();
        var first = await Post(alice, "/v1/payments/", new { recipientHandle = "bob_pay", amountMinor = 12_345, currency = "USD", note = "Dinner" }, key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var payment = await first.Content.ReadFromJsonAsync<JsonElement>();
        var replay = await Post(alice, "/v1/payments/", new { recipientHandle = "bob_pay", amountMinor = 12_345, currency = "USD", note = "Dinner" }, key);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(payment.GetProperty("id").GetGuid(), (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        var changed = await Post(alice, "/v1/payments/", new { recipientHandle = "bob_pay", amountMinor = 12_346, currency = "USD", note = "Dinner" }, key);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal(87_655, (await Wallet(alice)).GetProperty("availableMinor").GetInt64());
        Assert.Equal(112_345, (await Wallet(bob)).GetProperty("availableMinor").GetInt64());
        await using var scope = host.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SenzyDbContext>();
        Assert.Single(await db.Payments.ToListAsync());
        Assert.Single(await db.LedgerTransactions.Where(transaction => transaction.Kind == LedgerTransactionKind.Payment).ToListAsync());
        var stored = await db.Payments.SingleAsync();
        var entries = await db.LedgerEntries.Where(entry => entry.LedgerTransactionId == stored.PostedLedgerTransactionId).ToListAsync();
        Assert.Equal(entries.Where(entry => entry.Direction == LedgerEntryDirection.Debit).Sum(entry => entry.AmountMinor),
            entries.Where(entry => entry.Direction == LedgerEntryDirection.Credit).Sum(entry => entry.AmountMinor));
        Assert.Single(await db.IdempotencyRecords.ToListAsync());
        Assert.Single(await db.OutboxMessages.Where(message => message.Type == "payment.posted").ToListAsync());
    }

    [PostgresFact]
    public async Task ConcurrentDifferentCommandsCannotOverspend()
    {
        await using var host = await DatabaseHost.Create();
        using var alice = host.Factory.CreateClient();
        using var bob = host.Factory.CreateClient();
        using var carol = host.Factory.CreateClient();
        await RegisterAndLogin(alice, "alice-race@example.test", "alice_race");
        await RegisterAndLogin(bob, "bob-race@example.test", "bob_race");
        await RegisterAndLogin(carol, "carol-race@example.test", "carol_race");
        var attempts = await Task.WhenAll(
            Post(alice, "/v1/payments/", new { recipientHandle = "bob_race", amountMinor = 70_000, currency = "USD", note = "one" }, Key()),
            Post(alice, "/v1/payments/", new { recipientHandle = "carol_race", amountMinor = 70_000, currency = "USD", note = "two" }, Key()));
        Assert.Single(attempts, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(attempts, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(30_000, (await Wallet(alice)).GetProperty("availableMinor").GetInt64());
        await using var scope = host.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SenzyDbContext>();
        var payment = Assert.Single(await db.Payments.ToListAsync());
        Assert.Equal(2, await db.LedgerEntries.CountAsync(entry => entry.LedgerTransactionId == payment.PostedLedgerTransactionId));
    }

    [PostgresFact]
    public async Task ConcurrentSameKeyCreatesOnePaymentAndReplaysOneResult()
    {
        await using var host = await DatabaseHost.Create();
        using var alice = host.Factory.CreateClient();
        using var bob = host.Factory.CreateClient();
        await RegisterAndLogin(alice, "alice-idem@example.test", "alice_idem");
        await RegisterAndLogin(bob, "bob-idem@example.test", "bob_idem");
        var key = Key();
        var attempts = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Post(alice, "/v1/payments/",
            new { recipientHandle = "bob_idem", amountMinor = 5_000, currency = "USD", note = "same" }, key)));
        Assert.All(attempts, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        var ids = await Task.WhenAll(attempts.Select(async response => (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid()));
        Assert.Single(ids.Distinct());
        Assert.Equal(95_000, (await Wallet(alice)).GetProperty("availableMinor").GetInt64());
    }

    [PostgresFact]
    public async Task ReversalIsCompensatingAndIdempotent()
    {
        await using var host = await DatabaseHost.Create();
        using var alice = host.Factory.CreateClient();
        using var bob = host.Factory.CreateClient();
        await RegisterAndLogin(alice, "alice-reverse@example.test", "alice_reverse");
        await RegisterAndLogin(bob, "bob-reverse@example.test", "bob_reverse");
        var sent = await Post(alice, "/v1/payments/", new { recipientHandle = "bob_reverse", amountMinor = 25_000, currency = "USD", note = "undo" }, Key());
        var paymentId = (await sent.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var key = Key();
        var reversed = await Post(alice, $"/v1/payments/{paymentId}/reverse", new { }, key);
        Assert.Equal(HttpStatusCode.OK, reversed.StatusCode);
        Assert.Equal("reversed", (await reversed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await Post(alice, $"/v1/payments/{paymentId}/reverse", new { }, key)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(alice, $"/v1/payments/{paymentId}/reverse", new { }, Key())).StatusCode);
        Assert.Equal(100_000, (await Wallet(alice)).GetProperty("availableMinor").GetInt64());
        Assert.Equal(100_000, (await Wallet(bob)).GetProperty("availableMinor").GetInt64());
        await using var scope = host.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SenzyDbContext>();
        var stored = await db.Payments.SingleAsync();
        Assert.NotNull(stored.ReversalLedgerTransactionId);
        var reversal = await db.LedgerTransactions.SingleAsync(transaction => transaction.Id == stored.ReversalLedgerTransactionId);
        Assert.Equal(stored.PostedLedgerTransactionId, reversal.ReversesTransactionId);
    }

    [PostgresFact]
    public async Task PaymentRequestCanOnlyBeAcceptedByPayerOnce()
    {
        await using var host = await DatabaseHost.Create();
        using var alice = host.Factory.CreateClient();
        using var bob = host.Factory.CreateClient();
        using var outsider = host.Factory.CreateClient();
        await RegisterAndLogin(alice, "alice-request@example.test", "alice_request");
        await RegisterAndLogin(bob, "bob-request@example.test", "bob_request");
        await RegisterAndLogin(outsider, "other-request@example.test", "other_request");
        var created = await Post(alice, "/v1/payment-requests/", new { payerHandle = "bob_request", amountMinor = 4_500, currency = "USD", note = "Tickets" }, Key());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var requestId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await Post(outsider, $"/v1/payment-requests/{requestId}/accept", new { }, Key())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(bob, $"/v1/payment-requests/{requestId}/accept", new { }, Key())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(bob, $"/v1/payment-requests/{requestId}/accept", new { }, Key())).StatusCode);
        Assert.Equal(104_500, (await Wallet(alice)).GetProperty("availableMinor").GetInt64());
        Assert.Equal(95_500, (await Wallet(bob)).GetProperty("availableMinor").GetInt64());
    }

    [PostgresFact]
    public async Task FailureBeforeCommitRollsBackEveryRecordAndBalance()
    {
        await using var host = await DatabaseHost.Create(services =>
        {
            services.RemoveAll<IPaymentFailureInjector>();
            services.AddScoped<IPaymentFailureInjector, ThrowBeforeCommit>();
        });
        using var alice = host.Factory.CreateClient();
        using var bob = host.Factory.CreateClient();
        await RegisterAndLogin(alice, "alice-fail@example.test", "alice_fail");
        await RegisterAndLogin(bob, "bob-fail@example.test", "bob_fail");
        Assert.Equal(HttpStatusCode.InternalServerError, (await Post(alice, "/v1/payments/",
            new { recipientHandle = "bob_fail", amountMinor = 10_000, currency = "USD", note = "rollback" }, Key())).StatusCode);
        Assert.Equal(100_000, (await Wallet(alice)).GetProperty("availableMinor").GetInt64());
        Assert.Equal(100_000, (await Wallet(bob)).GetProperty("availableMinor").GetInt64());
        await using var scope = host.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SenzyDbContext>();
        Assert.Empty(await db.Payments.ToListAsync());
        Assert.Empty(await db.IdempotencyRecords.ToListAsync());
        Assert.Empty(await db.OutboxMessages.Where(message => message.Type == "payment.posted").ToListAsync());
        Assert.Empty(await db.LedgerTransactions.Where(transaction => transaction.Kind == LedgerTransactionKind.Payment).ToListAsync());
    }

    private const string Password = "example-test-passphrase";
    private static string Key() => Guid.NewGuid().ToString("N");
    private static async Task RegisterAndLogin(HttpClient client, string email, string handle)
    {
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/v1/auth/register",
            new { email, password = Password, displayName = handle, handle })).StatusCode);
        var login = await client.PostAsJsonAsync("/v1/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.GetProperty("accessToken").GetString());
    }
    private static async Task<JsonElement> Wallet(HttpClient client) =>
        await client.GetFromJsonAsync<JsonElement>("/v1/wallet/");
    private static Task<HttpResponseMessage> Post(HttpClient client, string path, object body, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request);
    }

    private sealed class ThrowBeforeCommit : IPaymentFailureInjector
    {
        public Task BeforeCommitAsync(string operation, CancellationToken cancellationToken) => throw new InvalidOperationException("Injected failure");
    }
}
