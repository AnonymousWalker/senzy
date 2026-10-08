using System.Text.Json.Serialization;

namespace Senzy.Api.Banking;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateLinkSessionRequest(Guid UserId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConnectAccountRequest(Guid UserId, string LinkToken, string InstitutionId,
    string DisplayName, string AccountKind);

public sealed record InstitutionsResponse(string Provider, bool Sandbox, IReadOnlyList<BankingInstitution> Institutions);
public sealed record AccountsResponse(string Provider, bool Sandbox, IReadOnlyList<ExternalBankAccount> Accounts);
public sealed record BankingError(string Error);

public static class BankingRoutes
{
    public static void MapBankingRoutes(this WebApplication app)
    {
        var group = app.MapGroup("/v1/banking").WithTags("Sandbox banking");

        group.MapGet("/institutions", (IBankingProvider provider) =>
            TypedResults.Ok(new InstitutionsResponse("stub-banking", true, provider.ListInstitutions())))
            .WithName("ListInstitutions");

        group.MapPost("/link-sessions", (CreateLinkSessionRequest request, IBankingProvider provider) =>
            request.UserId == Guid.Empty
                ? Results.BadRequest(new { error = "A valid userId is required." })
                : Results.Created("/v1/banking/link-sessions", provider.CreateLinkSession(request.UserId)))
            .WithName("CreateLinkSession")
            .Produces<BankLinkSession>(201).Produces<BankingError>(400);

        group.MapPost("/accounts", (ConnectAccountRequest request, IBankingProvider provider) =>
        {
            if (request.UserId == Guid.Empty || string.IsNullOrWhiteSpace(request.LinkToken) ||
                string.IsNullOrWhiteSpace(request.InstitutionId) ||
                string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 80 ||
                request.AccountKind is not ("checking" or "savings"))
                return Results.BadRequest(new { error = "Invalid sandbox account request." });

            try
            {
                return Results.Created("/v1/banking/accounts", provider.ConnectAccount(request.UserId,
                    request.LinkToken, request.InstitutionId, request.DisplayName, request.AccountKind));
            }
            catch (BankingProviderException error)
            {
                return Results.BadRequest(new { error = error.Message });
            }
        }).WithName("ConnectAccount")
            .Produces<ExternalBankAccount>(201).Produces<BankingError>(400);

        group.MapGet("/accounts/{userId:guid}", (Guid userId, IBankingProvider provider) =>
            userId == Guid.Empty
                ? Results.BadRequest(new { error = "A valid userId is required." })
                : Results.Ok(new AccountsResponse("stub-banking", true, provider.ListAccounts(userId))))
            .WithName("ListAccounts")
            .Produces<AccountsResponse>().Produces<BankingError>(400);
    }
}
