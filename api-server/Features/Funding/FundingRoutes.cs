using System.Security.Claims;
using Senzy.Api.Common;
using Senzy.Api.Features.Profiles;
using Senzy.Modules.Funding;
using Senzy.Modules.Wallets;

namespace Senzy.Api.Features.Funding;

public static class FundingRoutes
{
  public static void MapFundingRoutes(this WebApplication app)
  {
    var group = app.MapGroup("/v1/funding-transfers")
        .RequireAuthorization()
        .RequireRateLimiting("payments")
        .WithTags("Sandbox funding");

    group.MapGet(
        "/",
        async (int? limit, ClaimsPrincipal actor, FundingService funding, CancellationToken ct) =>
            await Execute(async () => Map(await funding.ListAsync(ProfileRoutes.ActorId(actor), limit ?? 50, ct))))
        .RequireRateLimiting("database-reads")
        .WithName("ListFundingTransfers")
        .Produces<FundingTransferListResponse>();

    group.MapPost(
        "/",
        async (
            CreateFundingTransferRequest request,
            ClaimsPrincipal actor,
            HttpContext context,
            FundingService funding,
            CancellationToken ct) =>
        {
          var result = await Execute(async () =>
              Map(await funding.CreateAsync(
                  ProfileRoutes.ActorId(actor),
                  new FundingCreateCommand(request.ExternalAccountId, request.Direction, request.AmountMinor, request.Currency),
                  context.Request.Headers["Idempotency-Key"].ToString(),
                  ct)));

          return result is IValueHttpResult<FundingTransferResponse> { Value: { } value }
              ? Results.Created($"/v1/funding-transfers/{value.Id}", value)
              : result;
        })
        .RequireRateLimiting("database-writes")
        .WithName("CreateFundingTransfer")
        .Produces<FundingTransferResponse>(201)
        .ProducesProblem(400)
        .ProducesProblem(404)
        .ProducesProblem(409);
  }

  private static async Task<IResult> Execute<T>(Func<Task<T>> action)
  {
    try
    {
      return Results.Ok(await action());
    }
    catch (Senzy.Modules.Payments.PaymentCommandException error)
    {
      return ApiErrors.Problem(error.Status, error.Code, error.Message);
    }
    catch (WalletMovementFailureException error)
    {
      return ApiErrors.Problem(error.Status, error.Code, error.Message);
    }
    catch (WalletPolicyFailureException error)
    {
      return ApiErrors.Problem(error.Status, error.Code, error.Message);
    }
  }

  private static FundingTransferListResponse Map(FundingTransferListResult value) =>
      new(value.Items.Select(Map).ToList());

  private static FundingTransferResponse Map(FundingTransferResult value) =>
      new(value.Id, value.ExternalAccountId, value.Provider, value.ProviderTransferId,
          value.Direction, value.Status, value.AmountMinor, value.Currency, value.CreatedAt);
}
