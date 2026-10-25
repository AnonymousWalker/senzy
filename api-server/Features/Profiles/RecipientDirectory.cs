using Microsoft.EntityFrameworkCore;
using Senzy.Api.Common;
using Senzy.Api.Infrastructure.Persistence;
using Senzy.Modules.Payments;

namespace Senzy.Api.Features.Profiles;

public sealed class RecipientDirectory(SenzyDbContext db) : IRecipientDirectory
{
  public async Task<PaymentRecipient> ResolveAsync(string handle, CancellationToken ct)
  {
    var normalized = ProfileRules.NormalizeHandle(handle);
    var profile = await db.Profiles.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Handle == normalized, ct)
        ?? throw new Senzy.Modules.Payments.PaymentCommandException(
            404,
            "recipient_missing",
            "That payment handle was not found.");

    return new PaymentRecipient(profile.UserId, profile.Handle);
  }

  public async Task<PaymentRecipient> RequireAsync(Guid userId, CancellationToken ct)
  {
    var profile = await db.Profiles.AsNoTracking().SingleAsync(value => value.UserId == userId, ct);
    return new PaymentRecipient(profile.UserId, profile.Handle);
  }
}
