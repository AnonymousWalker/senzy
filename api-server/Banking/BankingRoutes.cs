using System.Text.Json.Serialization;

namespace Senzy.Api.Banking;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateLinkSessionRequest(Guid UserId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConnectAccountRequest(Guid UserId, string LinkToken, string InstitutionId,
    string DisplayName, string AccountKind);

public static class BankingRoutes
{
    public static void MapBankingRoutes(this WebApplication app)
    {
        var group = app.MapGroup("/v1/banking").WithTags("Sandbox banking");

        group.MapGet("/institutions", (IBankingProvider provider) =>
            Results.Ok(new { provider = "stub-banking", sandbox = true,
                institutions = provider.ListInstitutions() }));

        group.MapPost("/link-sessions", (CreateLinkSessionRequest request, IBankingProvider provider) =>
            request.UserId == Guid.Empty
                ? Results.BadRequest(new { error = "A valid userId is required." })
                : Results.Created("/v1/banking/link-sessions", provider.CreateLinkSession(request.UserId)));

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
        });

        group.MapGet("/accounts/{userId:guid}", (Guid userId, IBankingProvider provider) =>
            userId == Guid.Empty
                ? Results.BadRequest(new { error = "A valid userId is required." })
                : Results.Ok(new { provider = "stub-banking", sandbox = true,
                    accounts = provider.ListAccounts(userId) }));
    }
}
