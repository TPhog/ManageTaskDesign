using FreelanceFlow.Api.Common;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SettingsEntity = FreelanceFlow.Api.Domain.Settings.Settings;

namespace FreelanceFlow.Api.Features.Settings;

/// <summary>Plan 13 (BR-133): the public intake link /f/{slug} — on/off and "Tạo link mới".</summary>
public static class IntakeLinkEndpoints
{
    public sealed record IntakeLinkDto(bool Enabled, string? Slug, string? Url);

    public sealed record UpdateIntakeLinkBody(bool? Enabled);

    public static void MapIntakeLinkEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/settings").RequireAuthorization("DesignerOnly");
        admin.MapGet("intake", async (AppDbContext db, IOptions<AppSettings> options, CancellationToken ct) =>
            Results.Ok(ToDto(await db.Settings.AsNoTracking().FirstAsync(ct), options.Value)));
        admin.MapPut("intake", Update);
        admin.MapPost("intake/regenerate", Regenerate);
    }

    /// <summary>"{App:PublicBaseUrl}/f/{slug}", or the relative "/f/{slug}" when no base URL is configured.</summary>
    public static string PublicUrl(string slug, AppSettings app) =>
        string.IsNullOrWhiteSpace(app.PublicBaseUrl) ? $"/f/{slug}" : $"{app.PublicBaseUrl.TrimEnd('/')}/f/{slug}";

    private static IntakeLinkDto ToDto(SettingsEntity s, AppSettings app) =>
        new(s.IntakeEnabled, s.IntakeSlug, s.IntakeSlug is null ? null : PublicUrl(s.IntakeSlug, app));

    private static async Task<IResult> Update(UpdateIntakeLinkBody body, AppDbContext db, ActivityWriter activity, IClock clock,
        IOptions<AppSettings> options, CancellationToken ct)
    {
        if (body.Enabled is not { } enabled)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["enabled"] = ["Bắt buộc"] });
        var settings = await db.Settings.FirstAsync(ct);
        settings.SetIntakeEnabled(enabled, clock.UtcNow);
        activity.Write("settings", Guid.Empty, "settings.intake_updated", data: new { enabled });   // never the slug
        return await SaveAsync(db, settings, options.Value, ct);
    }

    private static async Task<IResult> Regenerate(AppDbContext db, ActivityWriter activity, IClock clock,
        IOptions<AppSettings> options, CancellationToken ct)
    {
        var settings = await db.Settings.FirstAsync(ct);
        settings.RegenerateIntakeSlug(clock.UtcNow);
        activity.Write("settings", Guid.Empty, "settings.intake_link_regenerated");
        return await SaveAsync(db, settings, options.Value, ct);
    }

    private static async Task<IResult> SaveAsync(AppDbContext db, SettingsEntity settings, AppSettings app, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { code = "CONCURRENCY_CONFLICT", detail = "Cài đặt vừa được đổi ở nơi khác — vui lòng tải lại trang" });
        }
        return Results.Ok(ToDto(settings, app));
    }
}
