using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Dashboard;

/// <summary>P11 BR-91: 7-day work schedule. Read-only; a fixed number of projected queries.</summary>
internal static class ScheduleEndpoints
{
    public static void MapScheduleEndpoints(this IEndpointRouteBuilder admin) => admin.MapGet("dashboard/schedule", Get);

    private static int PriorityRank(string p) => p switch { "URGENT" => 0, "HIGH" => 1, "MEDIUM" => 2, _ => 3 };

    private static async Task<IResult> Get(AppDbContext db, IClock clock, CancellationToken ct)
    {
        var today = clock.TodayVn;
        var until = DashboardRules.ScheduleUntil(today);

        // BR-81/82: not DONE, project not CANCELLED — same base as the dashboard counters
        var open = from t in db.Tasks.AsNoTracking()
                   join p in db.Projects.AsNoTracking() on t.ProjectId equals p.Id
                   join c in db.Clients.AsNoTracking() on p.ClientId equals c.Id
                   where p.Status != "CANCELLED" && t.Status != "DONE"
                   select new { t, ProjectName = p.Name, ClientName = c.Name };

        var overdueTotal = await open.CountAsync(x => x.t.DueDate < today, ct);
        var overdue = await open.Where(x => x.t.DueDate < today)
            .OrderBy(x => x.t.DueDate).ThenBy(x => x.t.SortOrder)
            .Take(DashboardRules.MaxScheduleOverdue)
            .Select(x => new ScheduleTaskDto(x.t.Id, x.t.Title, x.t.Status, x.t.Priority, x.t.DueDate!.Value, x.t.SortOrder,
                x.t.ProjectId, x.ProjectName, x.ClientName))
            .ToListAsync(ct);

        var windowTasks = await open.Where(x => x.t.DueDate >= today && x.t.DueDate <= until)
            .Select(x => new ScheduleTaskDto(x.t.Id, x.t.Title, x.t.Status, x.t.Priority, x.t.DueDate!.Value, x.t.SortOrder,
                x.t.ProjectId, x.ProjectName, x.ClientName))
            .ToListAsync(ct);
        var undated = await open.CountAsync(x => x.t.DueDate == null, ct);

        var openStatuses = new[] { "DRAFT", "QUOTED", "IN_PROGRESS", "ON_HOLD" };
        var projectDeadlines = await (
                from p in db.Projects.AsNoTracking()
                join c in db.Clients.AsNoTracking() on p.ClientId equals c.Id
                where openStatuses.Contains(p.Status) && p.DueDate >= today && p.DueDate <= until
                select new ScheduleDeadlineDto("PROJECT", p.Id, p.Id, p.Name, p.Name, c.Name, p.DueDate!.Value))
            .ToListAsync(ct);
        var deliverableDeadlines = await (
                from d in db.Deliverables.AsNoTracking()
                join p in db.Projects.AsNoTracking() on d.ProjectId equals p.Id
                join c in db.Clients.AsNoTracking() on p.ClientId equals c.Id
                where d.DueDate >= today && d.DueDate <= until
                      && d.Status != "DELIVERED" && d.Status != "CANCELLED"
                      && p.Status != "CANCELLED" && p.Status != "COMPLETED"
                select new ScheduleDeadlineDto("DELIVERABLE", d.Id, p.Id, d.Name, p.Name, c.Name, d.DueDate!.Value))
            .ToListAsync(ct);

        var days = DashboardRules.ScheduleDates(today).Select(date =>
        {
            var tasks = windowTasks.Where(t => t.DueDate == date)
                .OrderBy(t => PriorityRank(t.Priority)).ThenBy(t => t.SortOrder).ToList();
            var deadlines = projectDeadlines.Concat(deliverableDeadlines).Where(d => d.Date == date)
                .OrderBy(d => d.Kind).ThenBy(d => d.Name).ToList();
            return new ScheduleDayDto(
                date,
                tasks.Take(DashboardRules.MaxScheduleTasksPerDay).ToList(),
                Math.Max(0, tasks.Count - DashboardRules.MaxScheduleTasksPerDay),
                deadlines);
        }).ToList();

        return Results.Ok(new ScheduleDto(today, undated, new ScheduleOverdueDto(overdueTotal, overdue), days));
    }
}
