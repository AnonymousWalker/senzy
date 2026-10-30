using System.Security.Claims;
using Senzy.Api.Common;
using Senzy.Api.Features.Profiles;
using Senzy.Api.Features.Reporting;
using Senzy.Api.Features.Reconciliation;
using Senzy.Modules.Wallets;

namespace Senzy.Api.Features.Operations;

public static class OperationsRoutes
{
  public static void MapOperationsRoutes(this WebApplication app)
  {
    var group = app.MapGroup("/v1/operations")
        .RequireAuthorization()
        .RequireRateLimiting("payments")
        .WithTags("Operations");

    group.MapGet(
        "/status",
        async (
            ClaimsPrincipal actor,
            IWalletPolicy walletPolicy,
            ReconciliationService reconciliation,
            CancellationToken ct) =>
        {
          var actorId = ProfileRoutes.ActorId(actor);
          var frozen = await walletPolicy.IsFrozenAsync(actorId, ct);

          return Results.Ok(new OperationsStatusResponse(
              frozen,
              walletPolicy.DailyLimitMinor,
              await walletPolicy.UsedTodayAsync(actorId, ct),
              "USD",
              await reconciliation.LatestAsync(ct)));
        })
        .RequireRateLimiting("database-reads")
        .WithName("GetOperationsStatus")
        .Produces<OperationsStatusResponse>();

    group.MapPost(
        "/freeze",
        async (ClaimsPrincipal actor, IWalletPolicy walletPolicy, CancellationToken ct) =>
        {
          var actorId = ProfileRoutes.ActorId(actor);
          await walletPolicy.SetFrozenAsync(actorId, true, ct);
          return Results.NoContent();
        })
        .RequireRateLimiting("database-writes")
        .WithName("FreezeWallet")
        .Produces(204);

    group.MapPost(
        "/unfreeze",
        async (ClaimsPrincipal actor, IWalletPolicy walletPolicy, CancellationToken ct) =>
        {
          var actorId = ProfileRoutes.ActorId(actor);
          await walletPolicy.SetFrozenAsync(actorId, false, ct);
          return Results.NoContent();
        })
        .RequireRateLimiting("database-writes")
        .WithName("UnfreezeWallet")
        .Produces(204);

    group.MapGet(
        "/transactions",
        async (int? limit, ClaimsPrincipal actor, TransactionHistoryService history, CancellationToken ct) =>
        {
          var actorId = ProfileRoutes.ActorId(actor);
          return Results.Ok(await history.ListAsync(actorId, limit ?? 50, ct));
        })
        .RequireRateLimiting("database-reads")
        .WithName("ListTransactionHistory")
        .Produces<TransactionHistoryResponse>();

    if (app.Environment.IsDevelopment())
    {
          group.MapPost(
          "/reconciliation",
          async (ReconciliationService reconciliation, CancellationToken ct) =>
              Results.Ok(await reconciliation.RunAsync(ct)))
          .RequireRateLimiting("database-writes")
          .WithName("RunReconciliation")
          .Produces<ReconciliationResponse>();
    }
  }
}
