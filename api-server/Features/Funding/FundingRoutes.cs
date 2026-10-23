using System.Security.Claims;
using Senzy.Api.Common;
using Senzy.Api.Features.Profiles;

namespace Senzy.Api.Features.Funding;

public static class FundingRoutes
{
    public static void MapFundingRoutes(this WebApplication app)
    {
        var group = app.MapGroup("/v1/funding-transfers").RequireAuthorization()
            .RequireRateLimiting("payments").WithTags("Sandbox funding");
        group.MapGet("/", async (int? limit, ClaimsPrincipal actor, FundingService funding, CancellationToken ct) =>
            await Execute(() => funding.ListAsync(ProfileRoutes.ActorId(actor), limit ?? 50, ct)))
            .WithName("ListFundingTransfers").Produces<FundingTransferListResponse>();
        group.MapPost("/", async (CreateFundingTransferRequest request, ClaimsPrincipal actor, HttpContext context,
            FundingService funding, CancellationToken ct) =>
        {
            var result = await Execute(() => funding.CreateAsync(ProfileRoutes.ActorId(actor), request,
                context.Request.Headers["Idempotency-Key"].ToString(), ct));
            return result is IValueHttpResult<FundingTransferResponse> { Value: { } value }
                ? Results.Created($"/v1/funding-transfers/{value.Id}", value)
                : result;
        }).WithName("CreateFundingTransfer").Produces<FundingTransferResponse>(201)
            .ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
    }

    private static async Task<IResult> Execute<T>(Func<Task<T>> action)
    {
        try { return Results.Ok(await action()); }
        catch (PaymentCommandException error) { return ApiErrors.Problem(error.Status, error.Code, error.Message); }
    }
}
