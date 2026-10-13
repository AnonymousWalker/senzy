using System.Text.RegularExpressions;

namespace Senzy.Api.Features.Profiles;

public sealed class Profile
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = "";
    public string Handle { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record ProfileResponse(Guid UserId, string DisplayName, string Handle, DateTimeOffset CreatedAt);
public sealed record UpdateProfileRequest(string DisplayName, string Handle);

public static partial class ProfileRules
{
    [GeneratedRegex("^[a-z][a-z0-9_]{2,19}$", RegexOptions.CultureInvariant)]
    private static partial Regex HandlePattern();
    public static string NormalizeHandle(string? handle) => (handle ?? "").Trim().TrimStart('$').ToLowerInvariant();
    public static bool Valid(string? displayName, string? handle) =>
        !string.IsNullOrWhiteSpace(displayName) && displayName.Trim().Length <= 80 &&
        !displayName.Any(char.IsControl) && HandlePattern().IsMatch(NormalizeHandle(handle));
    public static ProfileResponse Response(Profile profile) => new(profile.UserId, profile.DisplayName, profile.Handle, profile.CreatedAt);
}
