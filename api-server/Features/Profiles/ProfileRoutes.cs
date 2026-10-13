using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Senzy.Api.Common;
using Senzy.Api.Infrastructure.Persistence;

namespace Senzy.Api.Features.Profiles;

public static class ProfileRoutes
{
    public static Guid ActorId(ClaimsPrincipal actor) => Guid.Parse(actor.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static void MapProfileRoutes(this WebApplication app)
    {
        var group = app.MapGroup("/v1/profile").RequireAuthorization().WithTags("Profiles");
        group.MapGet("/", async (ClaimsPrincipal actor, SenzyDbContext db, CancellationToken ct) =>
        {
            var profile = await db.Profiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == ActorId(actor), ct);
            return profile is null ? ApiErrors.Problem(404, "profile_missing", "Profile not found.") : Results.Ok(ProfileRules.Response(profile));
        }).WithName("GetProfile").Produces<ProfileResponse>().ProducesProblem(404);
        group.MapPut("/", async (UpdateProfileRequest request, ClaimsPrincipal actor, SenzyDbContext db, CancellationToken ct) =>
        {
            if (!ProfileRules.Valid(request.DisplayName, request.Handle))
                return ApiErrors.Problem(400, "invalid_profile", "Name must be 1–80 characters. Handle must be 3–20 lowercase letters, numbers or underscores, starting with a letter.");
            var profile = await db.Profiles.SingleOrDefaultAsync(p => p.UserId == ActorId(actor), ct);
            if (profile is null) return ApiErrors.Problem(404, "profile_missing", "Profile not found.");
            profile.DisplayName = request.DisplayName.Trim();
            profile.Handle = ProfileRules.NormalizeHandle(request.Handle);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            { return ApiErrors.Problem(409, "handle_taken", "That handle is already in use."); }
            return Results.Ok(ProfileRules.Response(profile));
        }).WithName("UpdateProfile").Produces<ProfileResponse>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
    }
}
