using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Infrastructure;
using FreelanceFlow.Api.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SettingsEntity = FreelanceFlow.Api.Domain.Settings.Settings;

namespace FreelanceFlow.Api.Features.Settings;

/// <summary>Plan 11 BR-94: digest on/off + VN hour, separate from PUT /api/admin/settings (same pattern as watermark).</summary>
public static class DigestSettingsEndpoints
{
    public sealed record DigestDto(bool Enabled, int HourVn, bool ChannelConfigured);
    public sealed record UpdateDigestBody(bool? Enabled, int? HourVn);

    public static void MapDigestSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/settings").RequireAuthorization("DesignerOnly");
        admin.MapGet("digest", Get);
        admin.MapPut("digest", Update);
    }

    private static DigestDto ToDto(SettingsEntity s, IOptions<NotificationsSettings> n) =>
        new(s.DigestEnabled, s.DigestHourVn, !string.IsNullOrWhiteSpace(n.Value.Discord.WebhookUrl));

    private static async Task<IResult> Get(AppDbContext db, IOptions<NotificationsSettings> n, CancellationToken ct) =>
        Results.Ok(ToDto(await db.Settings.AsNoTracking().FirstAsync(ct), n));

    private static async Task<IResult> Update(
        UpdateDigestBody body, AppDbContext db, ActivityWriter activity, IClock clock, IOptions<NotificationsSettings> n, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (body.Enabled is null) errors["enabled"] = ["Bắt buộc"];
        if (body.HourVn is not (>= SettingsEntity.MinDigestHour and <= SettingsEntity.MaxDigestHour))
            errors["hourVn"] = [$"Từ {SettingsEntity.MinDigestHour} đến {SettingsEntity.MaxDigestHour}"];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var settings = await db.Settings.FirstAsync(ct);
        settings.SetDigest(body.Enabled!.Value, body.HourVn!.Value, clock.UtcNow);
        activity.Write("settings", Guid.Empty, "settings.digest_updated",
            data: new { enabled = settings.DigestEnabled, hourVn = settings.DigestHourVn });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Results.Conflict(new { code = "CONCURRENCY_CONFLICT" }); }
        return Results.Ok(ToDto(settings, n));
    }
}
