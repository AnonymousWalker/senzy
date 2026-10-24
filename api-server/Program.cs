using Npgsql;
using Senzy.Api.Features.Banking;
using Senzy.Api.Features.Health;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Senzy.Api.Common;
using Senzy.Api.Features.Identity;
using Senzy.Api.Features.Profiles;
using Senzy.Api.Features.Activity;
using Senzy.Api.Features.Configuration;
using Senzy.Api.Features.Telemetry;
using Senzy.Api.Features.Ledger;
using Senzy.Api.Features.Payments;
using Senzy.Api.Features.Funding;
using Senzy.Api.Features.Notifications;
using Senzy.Api.Features.Operations;
using Senzy.Api.Features.Wallets;
using Senzy.Api.Infrastructure.Outbox;
using Senzy.Api.Infrastructure.Persistence;
using Senzy.Api.Infrastructure.Banking;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IBankingProvider, StubBankingProvider>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ApiErrors.Customize);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

var connectionString = builder.Configuration.GetConnectionString("Postgres");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
}

builder.Services.AddDbContext<SenzyDbContext>(options => options.UseNpgsql(connectionString ??
    "Host=localhost;Port=5433;Database=senzy;Username=senzy;Password=senzy"));
builder.Services.AddIdentityApiEndpoints<ApplicationUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<SenzyDbContext>();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "senzy.session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.Configure<BearerTokenOptions>(IdentityConstants.BearerScheme, options =>
{
    options.BearerTokenExpiration = TimeSpan.FromMinutes(15);
    options.RefreshTokenExpiration = TimeSpan.FromDays(7);
});
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "senzy.csrf";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
var protection = builder.Services.AddDataProtection().SetApplicationName("Senzy");
if (builder.Configuration["DataProtection:KeyPath"] is { Length: > 0 } keyPath)
    protection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
builder.Services.AddScoped<ISessionValidator, SessionValidator>();
builder.Services.AddScoped<LedgerPostingService>();
builder.Services.AddScoped<SandboxWalletService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<FundingService>();
builder.Services.AddScoped<WalletPolicy>();
builder.Services.AddScoped<ReconciliationService>();
builder.Services.AddScoped<OutboxDispatcher>();
builder.Services.AddScoped<IPaymentFailureInjector, NoopPaymentFailureInjector>();
builder.Services.AddScoped<IFundingFailureInjector, NoopFundingFailureInjector>();
builder.Services.AddHostedService<OutboxWorker>();
builder.Services.AddHostedService<ReconciliationWorker>();
builder.Services.AddAuthorization();
var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173", "http://localhost:4001", "http://localhost:8081"];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("RateLimits:AuthPermitLimit", 30), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("telemetry", context => RateLimitPartition.GetFixedWindowLimiter(context.User.Identity?.Name ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("payments", context => RateLimitPartition.GetFixedWindowLimiter(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.OnRejected = async (context, _) => await ApiErrors.Problem(429, "rate_limited", "Too many requests. Try again shortly.").ExecuteAsync(context.HttpContext);
});

var app = builder.Build();

if (args.Contains("--migrate"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<SenzyDbContext>().Database.MigrateAsync();
    return;
}

app.Use(async (context, next) =>
{
    var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    context.Response.Headers["X-Correlation-ID"] = traceId;
    context.Response.Headers.CacheControl = "no-store";
    using var scope = app.Logger.BeginScope(new Dictionary<string, object> { ["TraceId"] = traceId });
    var timer = Stopwatch.StartNew();
    await next(context);
    app.Logger.LogInformation("HTTP {Method} {Path} returned {Status} in {ElapsedMs}ms", context.Request.Method,
        context.Request.Path, context.Response.StatusCode, timer.ElapsedMilliseconds);
});
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var status = error is BadHttpRequestException ? 400 : error is NpgsqlException ? 503 : 500;
    await ApiErrors.Problem(status, status == 400 ? "invalid_request" : status == 503 ? "service_unavailable" : "server_error",
        status == 400 ? "The request body is invalid." : "The request could not be completed. Try again.").ExecuteAsync(context);
}));
app.UseStatusCodePages();
app.UseCors();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        if (!await context.RequestServices.GetRequiredService<ISessionValidator>().IsValidAsync(context.User))
        {
            await ApiErrors.Problem(401, "invalid_session", "Your session has expired. Sign in again.").ExecuteAsync(context);
            return;
        }
        var cookie = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (cookie.Succeeded && !context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) &&
            context.Request.Method is not ("GET" or "HEAD" or "OPTIONS"))
        {
            var origin = context.Request.Headers.Origin.ToString();
            if (origin.Length > 0 && !origins.Contains(origin, StringComparer.OrdinalIgnoreCase))
            {
                await ApiErrors.Problem(403, "origin_denied", "This origin is not allowed.").ExecuteAsync(context);
                return;
            }
            try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException)
            {
                await ApiErrors.Problem(400, "csrf_invalid", "Refresh this page and try again.").ExecuteAsync(context);
                return;
            }
        }
    }
    await next(context);
});
app.UseRateLimiter();
app.UseAuthorization();

app.MapHealthRoutes();
app.MapConfigurationRoutes();
app.MapIdentityRoutes();
app.MapProfileRoutes();
app.MapActivityRoutes();
app.MapTelemetryRoutes();
app.MapPaymentRoutes();
app.MapNotificationRoutes();
app.MapOperationsRoutes();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapBankingRoutes();
    app.MapFundingRoutes();
}

app.Run();

public partial class Program;
