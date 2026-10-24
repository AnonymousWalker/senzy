using Microsoft.EntityFrameworkCore;
using Senzy.Api.Common;
using Senzy.Api.Infrastructure.Persistence;

namespace Senzy.Api.Features.Profiles;

public sealed record RecipientReference(Guid UserId, string Handle);

public sealed class RecipientDirectory(SenzyDbContext db)
{
    public async Task<RecipientReference> ResolveAsync(string handle, CancellationToken ct)
    {
        var normalized = ProfileRules.NormalizeHandle(handle);
        var profile = await db.Profiles.AsNoTracking().SingleOrDefaultAsync(value => value.Handle == normalized, ct)
            ?? throw new PaymentCommandException(404, "recipient_missing", "That payment handle was not found.");
        return new(profile.UserId, profile.Handle);
    }

    public async Task<RecipientReference> RequireAsync(Guid userId, CancellationToken ct)
    {
        var profile = await db.Profiles.AsNoTracking().SingleAsync(value => value.UserId == userId, ct);
        return new(profile.UserId, profile.Handle);
    }
}
