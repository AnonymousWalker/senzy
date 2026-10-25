namespace Senzy.Api.Features.Configuration;

public sealed record ClientConfiguration(
    bool RegistrationEnabled,
    bool ActivityEnabled,
    bool TelemetryEnabled,
    bool SandboxBankingEnabled);

public static class ConfigurationRoutes
{
  public static ClientConfiguration Read(IConfiguration configuration, IHostEnvironment environment) =>
      new(
          environment.IsDevelopment() && configuration.GetValue("Features:Registration", true),
          configuration.GetValue("Features:Activity", true),
          configuration.GetValue("Features:Telemetry", true),
          environment.IsDevelopment());

  public static void MapConfigurationRoutes(this WebApplication app)
  {
    app.MapGet(
        "/v1/config",
        (IConfiguration config, IHostEnvironment environment) =>
            TypedResults.Ok(Read(config, environment)))
        .AllowAnonymous()
        .WithName("GetClientConfiguration")
        .WithTags("Configuration");
  }
}
