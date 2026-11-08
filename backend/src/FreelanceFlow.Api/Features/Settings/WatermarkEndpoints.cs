using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using SettingsEntity = FreelanceFlow.Api.Domain.Settings.Settings;

namespace FreelanceFlow.Api.Features.Settings;

/// <summary>Spec §4: watermark config, separate from PUT /api/admin/settings so that body stays unchanged.</summary>
public static class WatermarkEndpoints
{
    public sealed record WatermarkDto(bool Enabled, string? Text, string EffectiveText, int OpacityPercent);

    public sealed record UpdateWatermarkBody(bool? Enabled, string? Text, int? OpacityPercent);

    public static void MapWatermarkEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/settings").RequireAuthorization("DesignerOnly");
        admin.MapGet("watermark", GetWatermark);
        admin.MapPut("watermark", UpdateWatermark);
    }

    private static WatermarkDto ToDto(SettingsEntity s) =>
        new(s.WatermarkEnabled, s.WatermarkText, s.EffectiveWatermarkText, s.WatermarkOpacity);

    private static async Task<IResult> GetWatermark(AppDbContext db, CancellationToken ct) =>
        Results.Ok(ToDto(await db.Settings.AsNoTracking().FirstAsync(ct)));

    private static async Task<IResult> UpdateWatermark(
        UpdateWatermarkBody body, AppDbContext db, ActivityWriter activity, IClock clock, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (body.Enabled is null) errors["enabled"] = ["Bắt buộc"];
        if (body.OpacityPercent is not (>= SettingsEntity.MinWatermarkOpacity and <= SettingsEntity.MaxWatermarkOpacity))
            errors["opacityPercent"] = [$"Từ {SettingsEntity.MinWatermarkOpacity} đến {SettingsEntity.MaxWatermarkOpacity}%"];
        if (body.Text is { } text && text.Trim().Length > SettingsEntity.MaxWatermarkText)
            errors["text"] = [$"Tối đa {SettingsEntity.MaxWatermarkText} ký tự"];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var settings = await db.Settings.FirstAsync(ct);
        settings.SetWatermark(body.Enabled!.Value, body.Text, body.OpacityPercent!.Value, clock.UtcNow);
        activity.Write("settings", Guid.Empty, "settings.watermark_updated",
            data: new { enabled = settings.WatermarkEnabled, custom = settings.WatermarkText != null, opacity = settings.WatermarkOpacity });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { code = "CONCURRENCY_CONFLICT" });
        }
        return Results.Ok(ToDto(settings));
    }
}
