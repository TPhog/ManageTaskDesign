using System.Threading.RateLimiting;
using FreelanceFlow.Api.Common;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Auth;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Features.Auth;
using FreelanceFlow.Api.Features.Clients;
using FreelanceFlow.Api.Features.Dashboard;
using FreelanceFlow.Api.Features.Images;
using FreelanceFlow.Api.Features.Portal;
using FreelanceFlow.Api.Features.Projects;
using FreelanceFlow.Api.Features.Quotations;
using FreelanceFlow.Api.Features.Requests;
using FreelanceFlow.Api.Features.Revisions;
using FreelanceFlow.Api.Features.Search;
using FreelanceFlow.Api.Features.Services;
using FreelanceFlow.Api.Features.Settings;
using FreelanceFlow.Api.Features.Storage;
using FreelanceFlow.Api.Features.Tasks;
using FreelanceFlow.Api.Infrastructure;
using FreelanceFlow.Api.Infrastructure.Notifications;
using FreelanceFlow.Api.Infrastructure.Security;
using FreelanceFlow.Api.Infrastructure.Storage;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Formatting.Compact;
using StampValidator = FreelanceFlow.Api.Infrastructure.Security.DesignerStampValidator;

// Builder setup
var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console(new CompactJsonFormatter()));

// Core services
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<ActivityWriter>();
builder.Services.AddFreelanceFlowProblemDetails();
builder.Services.AddMemoryCache();

// Config
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("App"));
builder.Services.Configure<SeedSettings>(builder.Configuration.GetSection("Seed"));

// Notifications (Discord webhook; Noop when the URL is empty)
builder.Services.Configure<NotificationsSettings>(builder.Configuration.GetSection("Notifications"));
builder.Services.AddSingleton<NotificationQueue>();
builder.Services.AddSingleton<INotifier>(sp => sp.GetRequiredService<NotificationQueue>());
builder.Services.AddHttpClient("discord-webhook");
builder.Services.AddSingleton<INotificationSender>(sp =>
    string.IsNullOrWhiteSpace(sp.GetRequiredService<IOptions<NotificationsSettings>>().Value.Discord.WebhookUrl)
        ? new NoopNotificationSender()
        : ActivatorUtilities.CreateInstance<DiscordWebhookSender>(sp,
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("discord-webhook")));
builder.Services.AddHostedService<NotificationDispatcher>();

// Image storage (plan 08): local folder Storage:Local:RootPath with quota Storage:QuotaBytes
builder.Services.AddFileStorage(builder.Configuration);
builder.Services.AddScoped<StorageAlertService>();
builder.Services.AddScoped<ImageUploader>();
builder.Services.AddSingleton(new FreelanceFlow.Api.Infrastructure.Images.ImageProcessingGate(
    builder.Configuration.GetValue("Storage:MaxConcurrentImageProcessing",
        FreelanceFlow.Api.Infrastructure.Images.ImageProcessingGate.DefaultPermits)));
// Upload endpoints raise their own request size (RequestSizeLimit); multipart parsing must allow the same
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = ImageLimits.MaxAdminBodyBytes);
if (builder.Configuration.GetValue("Storage:ReconcileIntervalHours", 24) > 0)
    builder.Services.AddHostedService<StorageReconciliationService>();

// DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionString 'DefaultConnection' missing");

builder.Services.AddSingleton<AuditInterceptor>();
builder.Services.AddDbContext<AppDbContext>((sp, opt) =>
    opt.UseNpgsql(connectionString)
       .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));

// Auth
builder.Services.AddScoped<IPasswordHasher<DesignerAccount>, PasswordHasher<DesignerAccount>>();
builder.Services.AddHttpContextAccessor();
// Portal handlers take PortalPrincipal from DI; clientId never comes from the request (07-security §4.4)
builder.Services.AddScoped(sp => PortalPrincipal.FromUser(
    sp.GetRequiredService<IHttpContextAccessor>().HttpContext!.User));

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Designer";
    options.DefaultChallengeScheme = "Designer";
})
.AddCookie("Designer", options =>
{
    options.Cookie.Name = "ff_admin";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.Events.OnValidatePrincipal = StampValidator.ValidatePrincipal;
    options.Events.OnRedirectToLogin = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
})
// Client portal session (07-security §4.2): own cookie; SameSite=Lax so links opened from Zalo keep it
.AddCookie(PortalPrincipal.Scheme, options =>
{
    options.Cookie.Name = PortalPrincipal.CookieName;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.Events.OnValidatePrincipal = PortalSessionValidator.ValidatePrincipal;
    options.Events.OnRedirectToLogin = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status404NotFound; // portal never reveals existence (404, not 403)
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization(opt =>
{
    opt.AddPolicy("DesignerOnly", policy => policy.RequireAuthenticatedUser());
    // Only the Portal scheme is evaluated: a designer cookie never satisfies this policy, and the
    // default (Designer) scheme used by /api/admin/* never reads ff_portal.
    opt.AddPolicy(PortalPrincipal.Policy, policy => policy
        .AddAuthenticationSchemes(PortalPrincipal.Scheme)
        .RequireAuthenticatedUser()
        .RequireClaim(PortalPrincipal.ClientIdClaim));
});

// Rate limiting per IP per minute — login (07-security §2) and portal token exchange (§4.2);
// tests raise both via config
var loginPermitLimit = builder.Configuration.GetValue("RateLimit:LoginPerMinute", 5);
var portalSessionPermitLimit = builder.Configuration.GetValue("RateLimit:PortalSessionPerMinute", 10);
builder.Services.AddRateLimiter(opt =>
{
    opt.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = loginPermitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
    opt.AddPolicy("portal-session", ctx => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = portalSessionPermitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// Behind Caddy (deploy/Caddyfile): trust one X-Forwarded-For / X-Forwarded-Proto hop so rate limits
// partition on the real client IP and SecurePolicy.SameAsRequest sees https. KnownNetworks/KnownProxies
// are cleared, which is safe ONLY because docker-compose.yml publishes no port for `api` (only `web`
// is reachable from outside; `api` is on the internal compose network). If `api` ever gets a published
// port, pin the compose subnet in KnownNetworks instead.
builder.Services.Configure<ForwardedHeadersOptions>(opt =>
{
    opt.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    opt.ForwardLimit = 1;
    opt.KnownNetworks.Clear();
    opt.KnownProxies.Clear();
});

// App
var app = builder.Build();

// A relative portal link and a skipped CSRF Origin check are unacceptable outside dev/tests
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    var publicBaseUrl = builder.Configuration["App:PublicBaseUrl"];
    if (!Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps)
        throw new InvalidOperationException("App:PublicBaseUrl must be set to an absolute https URL outside Development.");
}

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// Seed designer account from env
var seedCfg = app.Services.GetRequiredService<IOptions<SeedSettings>>().Value;
if (!string.IsNullOrEmpty(seedCfg.DesignerUsername) && !string.IsNullOrEmpty(seedCfg.DesignerPassword))
{
    using var seedScope = app.Services.CreateScope();
    var seedDb = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var seedHasher = seedScope.ServiceProvider.GetRequiredService<IPasswordHasher<DesignerAccount>>();
    if (!await seedDb.DesignerAccounts.AnyAsync())
    {
        var account = new DesignerAccount
        {
            Id = Guid.CreateVersion7(),
            Username = seedCfg.DesignerUsername.ToLowerInvariant(),
            SecurityStamp = Guid.NewGuid().ToString("N"),
        };
        account.PasswordHash = seedHasher.HashPassword(account, seedCfg.DesignerPassword);
        seedDb.DesignerAccounts.Add(account);
        await seedDb.SaveChangesAsync();
    }
}

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<CsrfMiddleware>();

// Endpoints
app.MapAuthEndpoints();
app.MapServicesEndpoints();
app.MapSettingsEndpoints();
app.MapClientsEndpoints();
app.MapProjectsEndpoints();
app.MapBriefsEndpoints();
app.MapDeliverablesEndpoints();
app.MapTasksEndpoints();
app.MapSearchEndpoints();
app.MapQuotationsEndpoints();
app.MapQuotationWorkflowEndpoints();
app.MapAdditionalQuotationEndpoints();
app.MapRequestsEndpoints();
app.MapRevisionsEndpoints();
app.MapProgressUpdatesEndpoints();
app.MapDashboardEndpoints();
app.MapImagesEndpoints();
app.MapStorageEndpoints();
app.MapWatermarkEndpoints();
app.MapProjectFlowEndpoints();
app.MapPortalSessionEndpoints();
app.MapPortalProjectsEndpoints();
app.MapPortalQuotationsEndpoints();
app.MapPortalRequestsEndpoints();
app.MapPortalDeliverablesEndpoints();
app.MapPortalImagesEndpoints();

app.MapGet("/health", async (AppDbContext db) =>
{
    await db.Database.ExecuteSqlRawAsync("SELECT 1");
    return Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow });
});

app.Run();

// Make Program class accessible for integration tests
public partial class Program { }
