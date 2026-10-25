namespace Senzy.Api.Features.Profiles;

public sealed class Profile
{
  public Guid UserId { get; set; }
  public string DisplayName { get; set; } = "";
  public string Handle { get; set; } = "";
  public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
