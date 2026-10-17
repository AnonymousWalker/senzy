using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Senzy.Api.Common;
using Senzy.Api.Features.Configuration;
using Senzy.Api.Features.Profiles;
using Senzy.Api.Features.Ledger;
using Senzy.Api.Infrastructure.Persistence;

namespace Senzy.Api.Features.Identity;

public sealed record RegisterRequest(string Email, string Password, string DisplayName, string Handle);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record TokenResponse(string TokenType, string AccessToken, long ExpiresIn, string RefreshToken);
public sealed record CsrfResponse(string Token);

public static class IdentityRoutes
{
    public static void MapIdentityRoutes(this WebApplication app)
    {
        var group = app.MapGroup("/v1/auth").WithTags("Identity").RequireRateLimiting("auth");
        group.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return TypedResults.Ok(new CsrfResponse(antiforgery.GetAndStoreTokens(context).RequestToken!));
        }).AllowAnonymous().WithName("GetCsrfToken");

        group.MapPost("/register", async (RegisterRequest request, UserManager<ApplicationUser> users,
            SenzyDbContext db, SandboxWalletService wallets, IConfiguration config, IHostEnvironment environment, CancellationToken ct) =>
        {
            if (!ConfigurationRoutes.Read(config, environment).RegistrationEnabled)
                return ApiErrors.Problem(403, "registration_disabled", "Registration is not available.");
            if (!ProfileRules.Valid(request.DisplayName, request.Handle) || string.IsNullOrWhiteSpace(request.Email) ||
                request.Email.Length > 254 || !new EmailAddressAttribute().IsValid(request.Email.Trim()) ||
                string.IsNullOrEmpty(request.Password) || request.Password.Length is < 8 or > 128)
                return ApiErrors.Problem(400, "invalid_registration", "Use a valid email, an 8–128 character password, a name up to 80 characters, and a 3–20 character handle starting with a letter.");
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var user = new ApplicationUser { UserName = request.Email.Trim(), Email = request.Email.Trim() };
            try
            {
                var result = await users.CreateAsync(user, request.Password);
                if (!result.Succeeded)
                    return ApiErrors.Problem(400, "registration_failed", "Unable to register with those details. Check your password or try signing in.");
                var profile = new Profile { UserId = user.Id, DisplayName = request.DisplayName.Trim(), Handle = ProfileRules.NormalizeHandle(request.Handle) };
                db.Profiles.Add(profile);
                wallets.CreateOpeningBalance(user.Id);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return Results.Created("/v1/profile", ProfileRules.Response(profile));
            }
            catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return ApiErrors.Problem(409, "registration_conflict", "Unable to register with those details. Try a different handle or sign in.");
            }
        }).AllowAnonymous().WithName("Register").Produces<ProfileResponse>(201).ProducesProblem(400).ProducesProblem(403).ProducesProblem(409);

        group.MapPost("/login", async (LoginRequest request, bool? useCookies, HttpContext context,
            IAntiforgery antiforgery, SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users) =>
        {
            if (useCookies == true)
            {
                try { await antiforgery.ValidateRequestAsync(context); }
                catch (AntiforgeryValidationException) { return ApiErrors.Problem(400, "csrf_invalid", "Refresh this page and try again."); }
            }
            if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 254 || string.IsNullOrEmpty(request.Password) || request.Password.Length > 128)
                return ApiErrors.Problem(401, "invalid_credentials", "Email or password is incorrect.");
            signIn.AuthenticationScheme = useCookies == true ? IdentityConstants.ApplicationScheme : IdentityConstants.BearerScheme;
            var user = await users.FindByEmailAsync(request.Email.Trim());
            if (user is null) return ApiErrors.Problem(401, "invalid_credentials", "Email or password is incorrect.");
            var result = await signIn.PasswordSignInAsync(user, request.Password, isPersistent: true, lockoutOnFailure: true);
            return result.Succeeded ? Results.Empty : ApiErrors.Problem(401, "invalid_credentials", "Email or password is incorrect, or sign-in is temporarily unavailable.");
        }).AllowAnonymous().WithName("Login").Produces<TokenResponse>().ProducesProblem(400).ProducesProblem(401);

        group.MapPost("/refresh", async (RefreshRequest request, IOptionsMonitor<BearerTokenOptions> options,
            SignInManager<ApplicationUser> signIn, TimeProvider clock) =>
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken) || request.RefreshToken.Length > 8192)
                return ApiErrors.Problem(401, "invalid_session", "Sign in again.");
            var ticket = options.Get(IdentityConstants.BearerScheme).RefreshTokenProtector.Unprotect(request.RefreshToken);
            if (ticket?.Properties.ExpiresUtc is not { } expires || expires <= clock.GetUtcNow() ||
                await signIn.ValidateSecurityStampAsync(ticket.Principal) is not { } user)
                return ApiErrors.Problem(401, "invalid_session", "Sign in again.");
            return Results.SignIn(await signIn.CreateUserPrincipalAsync(user), authenticationScheme: IdentityConstants.BearerScheme);
        }).AllowAnonymous().WithName("RefreshSession").Produces<TokenResponse>().ProducesProblem(401);

        group.MapPost("/logout", async (ClaimsPrincipal actor, UserManager<ApplicationUser> users, HttpContext context) =>
        {
            var user = await users.GetUserAsync(actor);
            if (user is not null) await users.UpdateSecurityStampAsync(user);
            await context.SignOutAsync(IdentityConstants.ApplicationScheme);
            return Results.NoContent();
        }).RequireAuthorization().WithName("Logout").Produces(204);
    }
}
