using System.Security.Claims;
using Senzy.Api.Common;
using Senzy.Api.Features.Payments.Application;
using Senzy.Api.Features.Profiles;

namespace Senzy.Api.Features.Payments;

public static class PaymentRoutes
{
    public static void MapPaymentRoutes(this WebApplication app)
    {
        var wallet = app.MapGroup("/v1/wallet").RequireAuthorization().WithTags("Wallet");
        wallet.MapGet("/", async (ClaimsPrincipal actor, PaymentService payments, CancellationToken ct) =>
            await Execute(() => payments.GetWalletAsync(ProfileRoutes.ActorId(actor), ct)))
            .WithName("GetWallet").Produces<WalletResponse>().ProducesProblem(404);

        var paymentGroup = app.MapGroup("/v1/payments").RequireAuthorization().RequireRateLimiting("payments").WithTags("Payments");
        paymentGroup.MapGet("/", async (int? limit, ClaimsPrincipal actor, PaymentService payments, CancellationToken ct) =>
            await Execute(() => payments.ListPaymentsAsync(ProfileRoutes.ActorId(actor), limit ?? 50, ct)))
            .WithName("ListPayments").Produces<PaymentListResponse>();
        paymentGroup.MapPost("/", async (SendPaymentRequest request, ClaimsPrincipal actor, HttpContext context,
            PaymentService payments, CancellationToken ct) =>
        {
            var result = await Execute(() => payments.SendAsync(ProfileRoutes.ActorId(actor), request,
                context.Request.Headers["Idempotency-Key"].ToString(), ct));
            return result is IValueHttpResult<PaymentResponse> { Value: { } value }
                ? Results.Created($"/v1/payments/{value.Id}", value)
                : result;
        }).WithName("SendPayment").Produces<PaymentResponse>(201).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        paymentGroup.MapPost("/{paymentId:guid}/reverse", async (Guid paymentId, ClaimsPrincipal actor, HttpContext context,
            PaymentService payments, CancellationToken ct) =>
            await Execute(() => payments.ReverseAsync(ProfileRoutes.ActorId(actor), paymentId,
                context.Request.Headers["Idempotency-Key"].ToString(), ct)))
            .WithName("ReversePayment").Produces<PaymentResponse>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        var requestGroup = app.MapGroup("/v1/payment-requests").RequireAuthorization().RequireRateLimiting("payments").WithTags("Payment requests");
        requestGroup.MapGet("/", async (int? limit, ClaimsPrincipal actor, PaymentService payments, CancellationToken ct) =>
            await Execute(() => payments.ListRequestsAsync(ProfileRoutes.ActorId(actor), limit ?? 50, ct)))
            .WithName("ListPaymentRequests").Produces<PaymentRequestListResponse>();
        requestGroup.MapPost("/", async (CreatePaymentRequest request, ClaimsPrincipal actor, HttpContext context,
            PaymentService payments, CancellationToken ct) =>
        {
            var result = await Execute(() => payments.CreateRequestAsync(ProfileRoutes.ActorId(actor), request,
                context.Request.Headers["Idempotency-Key"].ToString(), ct));
            return result is IValueHttpResult<PaymentRequestResponse> { Value: { } value }
                ? Results.Created($"/v1/payment-requests/{value.Id}", value)
                : result;
        }).WithName("CreatePaymentRequest").Produces<PaymentRequestResponse>(201).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        requestGroup.MapPost("/{requestId:guid}/accept", async (Guid requestId, ClaimsPrincipal actor, HttpContext context,
            PaymentService payments, CancellationToken ct) =>
            await Execute(() => payments.AcceptRequestAsync(ProfileRoutes.ActorId(actor), requestId,
                context.Request.Headers["Idempotency-Key"].ToString(), ct)))
            .WithName("AcceptPaymentRequest").Produces<PaymentResponse>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
    }

    private static async Task<IResult> Execute<T>(Func<Task<T>> action)
    {
        try { return Results.Ok(await action()); }
        catch (PaymentCommandException error) { return ApiErrors.Problem(error.Status, error.Code, error.Message); }
    }
}
