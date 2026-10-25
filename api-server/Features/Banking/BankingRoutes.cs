using System.Security.Claims;
using System.Text.Json.Serialization;
using Senzy.Api.Common;
using Senzy.Api.Features.Profiles;

namespace Senzy.Api.Features.Banking;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateLinkSessionRequest;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConnectAccountRequest(
    string LinkToken,
    string InstitutionId,
    string DisplayName,
    string AccountKind);

public sealed record InstitutionsResponse(
    string Provider,
    bool Sandbox,
    IReadOnlyList<BankingInstitution> Institutions);

public sealed record AccountsResponse(
    string Provider,
    bool Sandbox,
    IReadOnlyList<ExternalBankAccount> Accounts);

public static class BankingRoutes
{
  public static void MapBankingRoutes(this WebApplication app)
  {
    var group = app.MapGroup("/v1/banking")
        .WithTags("Sandbox banking")
        .RequireAuthorization();

    group.MapGet(
        "/institutions",
        (IBankingProvider provider) =>
            TypedResults.Ok(new InstitutionsResponse("stub-banking", true, provider.ListInstitutions())))
        .AllowAnonymous()
        .WithName("ListInstitutions");

    group.MapPost(
        "/link-sessions",
        (CreateLinkSessionRequest request, ClaimsPrincipal actor, IBankingProvider provider) =>
            Results.Created(
                "/v1/banking/link-sessions",
                provider.CreateLinkSession(ProfileRoutes.ActorId(actor))))
        .WithName("CreateLinkSession")
        .Produces<BankLinkSession>(201)
        .ProducesProblem(400);

    group.MapPost(
        "/accounts",
        (ConnectAccountRequest request, ClaimsPrincipal actor, IBankingProvider provider) =>
        {
          if (string.IsNullOrWhiteSpace(request.LinkToken)
              || string.IsNullOrWhiteSpace(request.InstitutionId)
              || string.IsNullOrWhiteSpace(request.DisplayName)
              || request.DisplayName.Trim().Length > 80
              || request.AccountKind is not ("checking" or "savings"))
          {
            return ApiErrors.Problem(400, "invalid_account", "Invalid sandbox account request.");
          }

          try
          {
            return Results.Created(
                "/v1/banking/accounts",
                provider.ConnectAccount(
                    ProfileRoutes.ActorId(actor),
                    request.LinkToken,
                    request.InstitutionId,
                    request.DisplayName,
                    request.AccountKind));
          }
          catch (BankingProviderException error)
          {
            return ApiErrors.Problem(400, "invalid_link_session", error.Message);
          }
        })
        .WithName("ConnectAccount")
        .Produces<ExternalBankAccount>(201)
        .ProducesProblem(400);

    group.MapGet(
        "/accounts",
        (ClaimsPrincipal actor, IBankingProvider provider) =>
            Results.Ok(new AccountsResponse(
                "stub-banking",
                true,
                provider.ListAccounts(ProfileRoutes.ActorId(actor)))))
        .WithName("ListAccounts")
        .Produces<AccountsResponse>();
  }
}
