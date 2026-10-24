using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Senzy.Api.Infrastructure.Persistence;

namespace Senzy.Api.Features.Notifications;

public sealed class NotificationEventHandler(SenzyDbContext db)
{
    public const string ConsumerName = "notifications";

    public async Task HandleAsync(OutboxMessage message, DateTimeOffset now, CancellationToken ct)
    {
        using var payload = JsonDocument.Parse(message.PayloadJson);
        var root = payload.RootElement;
        var recipients = new List<(Guid UserId, string Kind, string Title, string Body)>();
        switch (message.Type)
        {
            case "payment.posted":
                var sender = root.GetProperty("senderUserId").GetGuid();
                var recipient = root.GetProperty("recipientUserId").GetGuid();
                var amount = root.GetProperty("amountMinor").GetInt64();
                recipients.Add((sender, "payment_sent", "Payment sent", $"You sent {FormatAmount(amount)} in the sandbox."));
                recipients.Add((recipient, "payment_received", "Payment received", $"You received {FormatAmount(amount)} in the sandbox."));
                break;
            case "payment.reversed":
                var payment = await db.Payments.AsNoTracking().SingleAsync(value => value.Id == message.AggregateId, ct);
                recipients.Add((payment.SenderUserId, "payment_reversed", "Payment reversed", $"Your {FormatAmount(payment.AmountMinor)} payment was reversed."));
                recipients.Add((payment.RecipientUserId, "payment_reversed", "Payment reversed", $"A {FormatAmount(payment.AmountMinor)} payment you received was reversed."));
                break;
            case "payment_request.created":
                recipients.Add((root.GetProperty("payerUserId").GetGuid(), "payment_request", "Payment requested",
                    $"You have a sandbox request for {FormatAmount(root.GetProperty("amountMinor").GetInt64())}."));
                break;
            case "payment_request.paid":
                var request = await db.PaymentRequests.AsNoTracking().SingleAsync(value => value.Id == message.AggregateId, ct);
                recipients.Add((request.RequesterUserId, "request_paid", "Request paid", $"Your {FormatAmount(request.AmountMinor)} request was paid."));
                break;
            case "funding.posted":
                var direction = root.GetProperty("direction").GetString();
                recipients.Add((root.GetProperty("userId").GetGuid(), "funding_posted",
                    direction == "cash_in" ? "Cash-in complete" : "Cash-out complete",
                    $"Your simulated {direction?.Replace('_', '-')} of {FormatAmount(root.GetProperty("amountMinor").GetInt64())} posted."));
                break;
            default:
                return;
        }
        foreach (var recipient in recipients)
        {
            if (await db.Notifications.AnyAsync(notification => notification.UserId == recipient.UserId &&
                notification.SourceOutboxMessageId == message.Id, ct)) continue;
            db.Notifications.Add(new()
            {
                UserId = recipient.UserId, SourceOutboxMessageId = message.Id, Kind = recipient.Kind,
                Title = recipient.Title, Body = recipient.Body, CreatedAt = now
            });
        }
    }

    private static string FormatAmount(long amountMinor) => $"${amountMinor / 100m:N2}";
}
