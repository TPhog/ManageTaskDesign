using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Dashboard;
using FreelanceFlow.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Dashboard;

/// <summary>P11 BR-96 (DF-04): first-run setup checklist. Facts are read live; only "dismissed" is stored.</summary>
internal static class SetupChecklistEndpoints
{
    public static void MapSetupChecklistEndpoints(this IEndpointRouteBuilder admin)
    {
        admin.MapGet("dashboard/setup", Get);
        admin.MapPost("dashboard/setup/dismiss", Dismiss);
    }

    private static async Task<IResult> Get(AppDbContext db, CancellationToken ct)
    {
        var settings = await db.Settings.AsNoTracking()
            .Select(s => new { s.DisplayName, s.BankBin, s.BankAccountNo, s.BankAccountName })
            .FirstAsync(ct);
        var hasPricedService = await db.Services.AsNoTracking().AnyAsync(s => s.Active && s.BasePrice > 0, ct);
        var hasTemplate = await db.Services.AsNoTracking().AnyAsync(s => s.Active && s.TaskTemplateJson != "[]", ct);
        var watermarkReviewed = await db.ActivityLogs.AsNoTracking().AnyAsync(a => a.Action == "settings.watermark_updated", ct);
        var hasClient = await db.Clients.AsNoTracking().AnyAsync(c => c.ArchivedAt == null, ct);
        var dismissedAt = await db.DashboardStates.AsNoTracking().Select(s => s.SetupDismissedAt).FirstOrDefaultAsync(ct);

        var items = SetupChecklist.Build(new SetupFacts(
            !string.IsNullOrWhiteSpace(settings.DisplayName),
            !string.IsNullOrWhiteSpace(settings.BankBin) && !string.IsNullOrWhiteSpace(settings.BankAccountNo)
                && !string.IsNullOrWhiteSpace(settings.BankAccountName),
            hasPricedService, hasTemplate, watermarkReviewed, hasClient));
        var done = items.Count(i => i.Done);
        return Results.Ok(new { dismissed = dismissedAt != null, complete = done == items.Count, doneCount = done, total = items.Count, items });
    }

    private static async Task<IResult> Dismiss(AppDbContext db, ActivityWriter activity, IClock clock, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var now = clock.UtcNow;
        var rows = await db.DashboardStates.Where(s => s.Id == 1 && s.SetupDismissedAt == null)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.SetupDismissedAt, now), ct);
        if (rows == 1)
        {
            activity.Write("settings", Guid.Empty, "settings.setup_dismissed");
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return Results.NoContent();
    }
}
