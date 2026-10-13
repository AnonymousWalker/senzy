using Microsoft.AspNetCore.Identity;

namespace Senzy.Api.Features.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser() => Id = Guid.NewGuid();
}
