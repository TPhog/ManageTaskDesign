using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Dashboard;
using FreelanceFlow.Api.Domain.Quotations;
using FreelanceFlow.Api.Domain.Requests;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Dashboard;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization();
        admin.MapGet("dashboard", GetDashboard);
        admin.MapSetupChecklistEndpoints();
    }

    /// <summary>
    /// docs/05 §3.12 — DSH-01..04 / BR-90. Read-only AsNoTracking queries; money is summed in SQL as long VND.
    /// Each count uses the same filter as the list a tile links to (tasks ?due=, quotations ?effectiveStatus=SENT).
    /// </summary>
    private static async Task<IResult> GetDashboard(AppDbContext db, IClock clock)
    {
        var today = clock.TodayVn;

        // ---- DSH-01: cần xử lý ----
        var newRequests = await db.ClientRequests.AsNoTracking().CountAsync(r => r.Status == RequestStatuses.New);

        // BR-81 overdue / due today (VN), BR-82 tasks of CANCELLED projects stay out — same as GET /api/admin/tasks?due=
        var openTasks = from t in db.Tasks.AsNoTracking()
                        join p in db.Projects.AsNoTracking() on t.ProjectId equals p.Id
                        where p.Status != "CANCELLED" && t.Status != "DONE"
                        select t;
        var overdueTasks = await openTasks.CountAsync(t => t.DueDate < today);
        var dueTodayTasks = await openTasks.CountAsync(t => t.DueDate == today);

        // BR-90: SENT and not expired (EXPIRED is computed on read, ADR-009); "sắp hết hạn" = expiry in [today, today+3]
        var expiringUntil = DashboardRules.ExpiringSoonUntil(today);
        var liveSent = db.Quotations.AsNoTracking()
            .Where(q => q.Status == QuotationStatuses.Sent && q.ExpiryDate >= today);
        var awaitingQuotations = await liveSent.CountAsync();
        var expiringSoonQuotations = await liveSent.CountAsync(q => q.ExpiryDate <= expiringUntil);

        // ---- DSH-02: dự án ----
        var inProgress = await db.Projects.AsNoTracking().CountAsync(p => p.Status == "IN_PROGRESS");
        var onHold = await db.Projects.AsNoTracking().CountAsync(p => p.Status == "ON_HOLD");
        var upcomingUntil = DashboardRules.UpcomingUntil(today);
        var openStatuses = DashboardRules.OpenProjectStatuses;
        var dueIn7Days = await (
                from p in db.Projects.AsNoTracking()
                join c in db.Clients.AsNoTracking() on p.ClientId equals c.Id
                where openStatuses.Contains(p.Status) && p.DueDate >= today && p.DueDate <= upcomingUntil
                orderby p.DueDate, p.Name
                select new DashboardDueProjectDto(p.Id, p.Name, c.Name, p.DueDate!.Value, p.Status))
            .Take(DashboardRules.MaxUpcomingProjects)
            .ToListAsync();

        // ---- DSH-03: tiền ----
        // docs/04 §4.2: BR-73 outstanding of ACCEPTED quotations whose project is not CANCELLED
        var outstanding = await (
                from q in db.Quotations.AsNoTracking()
                join p in db.Projects.AsNoTracking() on q.ProjectId equals p.Id
                where q.Status == QuotationStatuses.Accepted && p.Status != "CANCELLED"
                select q.PaymentStatus == PaymentStatuses.Paid ? 0L
                    : q.PaymentStatus == PaymentStatuses.DepositPaid ? q.Total - q.DepositAmount
                    : q.Total)
            .SumAsync();

        // accepted_at is the date the designer entered (BR-36); one read feeds the month totals and the chart (DSH-04)
        var chartStart = DashboardRules.ChartStart(today);
        var accepted = await db.Quotations.AsNoTracking()
            .Where(q => q.Status == QuotationStatuses.Accepted && q.AcceptedAt >= chartStart)
            .Select(q => new AcceptedAmount(q.AcceptedAt!.Value, q.Kind, q.Total))
            .ToListAsync();

        return Results.Ok(new DashboardDto(
            new DashboardActionRequiredDto(newRequests, overdueTasks, dueTodayTasks, awaitingQuotations, expiringSoonQuotations),
            new DashboardProjectsDto(inProgress, onHold, dueIn7Days),
            new DashboardMoneyDto(
                outstanding,
                DashboardRules.AcceptedInMonth(today, accepted),
                DashboardRules.AcceptedInMonth(today, accepted, QuotationKinds.Additional)),
            DashboardRules.AcceptedByMonth(today, accepted)));
    }
}
