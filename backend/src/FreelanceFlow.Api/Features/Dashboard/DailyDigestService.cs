using FreelanceFlow.Api.Common;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Dashboard;
using FreelanceFlow.Api.Domain.Quotations;
using FreelanceFlow.Api.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FreelanceFlow.Api.Features.Dashboard;

public enum DigestOutcome { Disabled, TooEarly, AlreadyRan, NothingToReport, Sent }

/// <summary>
/// P11 BR-94: once per Vietnam day, at or after the configured hour. The day is claimed with a conditional UPDATE on
/// dashboard_state (idempotent, safe across instances) BEFORE reading and enqueueing, so a retry never double-sends.
/// </summary>
public sealed class DailyDigestService(
    AppDbContext db, IClock clock, INotifier notifier, IOptions<AppSettings> app, ILogger<DailyDigestService> logger)
{
    private const int MaxPerGroup = 5;

    public async Task<DigestOutcome> RunAsync(CancellationToken ct = default)
    {
        var settings = await db.Settings.AsNoTracking().Select(s => new { s.DigestEnabled, s.DigestHourVn }).FirstAsync(ct);
        if (!settings.DigestEnabled) return DigestOutcome.Disabled;
        if (DashboardRules.VnHour(clock.UtcNow) < settings.DigestHourVn) return DigestOutcome.TooEarly;

        var today = clock.TodayVn;

        // One transaction: claim the day, read, enqueue, commit. Concurrent callers wait on the row lock and then match 0 rows;
        // any failure before the commit rolls the claim back, so the day is retried on the next tick instead of being lost.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var claimed = await db.DashboardStates
            .Where(s => s.Id == 1 && (s.DigestLastRunDate == null || s.DigestLastRunDate < today))
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.DigestLastRunDate, today), ct);
        if (claimed == 0)
        {
            if (!await db.DashboardStates.AnyAsync(ct))
                logger.LogWarning("dashboard_state row is missing; the daily digest cannot run");
            return DigestOutcome.AlreadyRan;
        }

        var facts = await LoadFactsAsync(today, ct);
        var message = DailyDigestMessage.Build(facts, today, app.Value.PublicBaseUrl);
        if (message is null)
        {
            await tx.CommitAsync(ct);
            return DigestOutcome.NothingToReport;
        }

        await notifier.EnqueueAsync(message, ct);   // in-memory queue; a throw propagates and rolls the claim back
        await tx.CommitAsync(ct);
        return DigestOutcome.Sent;
    }

    private async Task<DigestFacts> LoadFactsAsync(DateOnly today, CancellationToken ct)
    {
        var tomorrow = today.AddDays(1);

        // BR-81/82: not DONE, project not CANCELLED
        var open = from t in db.Tasks.AsNoTracking()
                   join p in db.Projects.AsNoTracking() on t.ProjectId equals p.Id
                   where p.Status != "CANCELLED" && t.Status != "DONE"
                   select new { t.Title, t.DueDate, t.SortOrder, ProjectName = p.Name };

        var overdueQ = open.Where(x => x.DueDate < today);
        var overdueCount = await overdueQ.CountAsync(ct);
        var overdue = await overdueQ.OrderBy(x => x.DueDate).ThenBy(x => x.SortOrder).Take(MaxPerGroup)
            .Select(x => new DigestTask(x.Title, x.ProjectName, x.DueDate!.Value)).ToListAsync(ct);

        var todayQ = open.Where(x => x.DueDate == today);
        var todayCount = await todayQ.CountAsync(ct);
        var todayItems = await todayQ.OrderBy(x => x.SortOrder).Take(MaxPerGroup)
            .Select(x => new DigestTask(x.Title, x.ProjectName, x.DueDate!.Value)).ToListAsync(ct);

        var tomorrowQ = open.Where(x => x.DueDate == tomorrow);
        var tomorrowCount = await tomorrowQ.CountAsync(ct);
        var tomorrowItems = await tomorrowQ.OrderBy(x => x.SortOrder).Take(MaxPerGroup)
            .Select(x => new DigestTask(x.Title, x.ProjectName, x.DueDate!.Value)).ToListAsync(ct);

        var until = today.AddDays(DashboardRules.DigestDeadlineDays);
        var openStatuses = DashboardRules.OpenProjectStatuses;
        var deadlines = await db.Projects.AsNoTracking()
            .Where(p => openStatuses.Contains(p.Status) && p.DueDate >= today && p.DueDate <= until)
            .OrderBy(p => p.DueDate).ThenBy(p => p.Name)
            .Select(p => new DigestDeadline(p.Name, p.DueDate!.Value))
            .ToListAsync(ct);

        var expiring = await db.Quotations.AsNoTracking()
            .Where(q => q.Status == QuotationStatuses.Sent && q.ExpiryDate >= today && q.ExpiryDate <= until)
            .OrderBy(q => q.ExpiryDate)
            .Select(q => new DigestQuotation(q.Number, q.ExpiryDate!.Value))
            .ToListAsync(ct);

        // same receivables filter as GET /api/admin/dashboard (BR-73, project not CANCELLED)
        var receivables = await DashboardQueries.OutstandingTotalAsync(db, ct);

        return new DigestFacts(overdueCount, overdue, todayCount, todayItems, tomorrowCount, tomorrowItems, deadlines, expiring, receivables);
    }
}
