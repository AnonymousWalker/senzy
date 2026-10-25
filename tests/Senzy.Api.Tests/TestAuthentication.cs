using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Senzy.Api.Features.Identity;

namespace Senzy.Api.Tests;

internal sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
  protected override Task<AuthenticateResult> HandleAuthenticateAsync()
  {
    var id = Request.Headers["X-Test-User"].FirstOrDefault() ?? "11111111-1111-4111-8111-111111111111";
    var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "Test"));
    return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "Test")));
  }
}
internal sealed class TestSessionValidator : ISessionValidator
{
  public Task<bool> IsValidAsync(ClaimsPrincipal principal) => Task.FromResult(true);
}
