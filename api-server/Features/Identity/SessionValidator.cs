using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace Senzy.Api.Features.Identity;

public interface ISessionValidator
{
    Task<bool> IsValidAsync(ClaimsPrincipal principal);
}

public sealed class SessionValidator(SignInManager<ApplicationUser> signIn) : ISessionValidator
{
    public async Task<bool> IsValidAsync(ClaimsPrincipal principal) =>
        await signIn.ValidateSecurityStampAsync(principal) is not null;
}
