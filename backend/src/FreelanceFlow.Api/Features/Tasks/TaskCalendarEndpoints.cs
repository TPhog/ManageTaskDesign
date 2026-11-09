using System.Globalization;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Tasks;

public record CalendarTaskDto(Guid Id, Guid ProjectId, string ProjectName, string Title, string Status,
    string Priority, DateOnly? StartDate, DateOnly? DueDate, bool IsOverdue);

/// <summary>Kind: DELIVERABLE | PROJECT — read-only deadline markers on the calendar.</summary>
public record CalendarMarkerDto(string Kind, Guid Id, Guid ProjectId, string ProjectName, string Title, DateOnly Date, string Status);

/// <summary>Truncated: more than <see cref="TaskCalendarEndpoints.MaxTasks"/> tasks matched; only the first ones are returned.</summary>
public record CalendarResponse(IReadOnlyList<CalendarTaskDto> Tasks, IReadOnlyList<CalendarMarkerDto> Markers, bool Truncated);

public record RescheduleTaskRequest(DateOnly? DueDate);

/// <summary>P15 D2: the task calendar (month/week) and drag-to-reschedule.</summary>
public static class TaskCalendarEndpoints
{
    public const int MaxRangeDays = 62;
    public const int MaxTasks = 500;

    public static void MapTaskCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization();

        // literal "calendar" never matches tasks/{id:guid}
        admin.MapGet("tasks/calendar", GetCalendar);
        admin.MapPut("tasks/{id:guid}/due-date", Reschedule);
    }

    private static async Task<IResult> GetCalendar(
        AppDbContext db,
        IClock clock,
        string? from = null,
        string? to = null,
        Guid? projectId = null)
    {
        var errors = new Dictionary<string, string[]>();
        if (!TryParseDate(from, out var start)) errors["from"] = ["Ngày bắt đầu không hợp lệ (yyyy-MM-dd)"];
        if (!TryParseDate(to, out var end)) errors["to"] = ["Ngày kết thúc không hợp lệ (yyyy-MM-dd)"];
        if (errors.Count == 0 && end < start) errors["to"] = ["Ngày kết thúc phải từ ngày bắt đầu trở đi"];
        if (errors.Count == 0 && end.DayNumber - start.DayNumber > MaxRangeDays)
            errors["to"] = [$"Khoảng ngày tối đa {MaxRangeDays} ngày"];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var today = clock.TodayVn;

        // One project rule for tasks and markers, like the task list (BR-82): an explicit projectId shows that project
        // whatever its status; otherwise CANCELLED projects are left out.
        var projects = projectId is { } pid
            ? db.Projects.AsNoTracking().Where(p => p.Id == pid)
            : db.Projects.AsNoTracking().Where(p => p.Status != "CANCELLED");

        var query = from t in db.Tasks.AsNoTracking()
                    join p in projects on t.ProjectId equals p.Id
                    select new { Task = t, Project = p };

        // Due inside the range, a start→due span overlapping it, or (no due) a start inside it
        var tasks = await query
            .Where(x =>
                (x.Task.DueDate != null && x.Task.DueDate >= start
                    && (x.Task.DueDate <= end || (x.Task.StartDate != null && x.Task.StartDate <= end)))
                || (x.Task.DueDate == null && x.Task.StartDate != null && x.Task.StartDate >= start && x.Task.StartDate <= end))
            .OrderBy(x => x.Task.DueDate)
            .ThenBy(x => x.Task.SortOrder)
            .ThenBy(x => x.Task.CreatedAt)
            .Take(MaxTasks + 1)
            .Select(x => new CalendarTaskDto(
                x.Task.Id,
                x.Task.ProjectId,
                x.Project.Name,
                x.Task.Title,
                x.Task.Status,
                x.Task.Priority,
                x.Task.StartDate,
                x.Task.DueDate,
                // BR-81 — same rule as ProjectTask.IsOverdue, written out so EF translates it
                x.Task.DueDate != null && x.Task.DueDate < today && x.Task.Status != "DONE"))
            .ToListAsync();
        var truncated = tasks.Count > MaxTasks;
        if (truncated) tasks.RemoveAt(MaxTasks);

        var deliverableMarkers = await (
                from d in db.Deliverables.AsNoTracking()
                join p in projects on d.ProjectId equals p.Id
                where d.DueDate != null && d.DueDate >= start && d.DueDate <= end
                      && d.Status != "CANCELLED" && d.Status != "DELIVERED"
                select new CalendarMarkerDto("DELIVERABLE", d.Id, p.Id, p.Name, d.Name, d.DueDate!.Value, d.Status))
            .ToListAsync();

        // a project's own deadline is only a marker while the project is still running
        var projectMarkers = await projects
            .Where(p => p.DueDate != null && p.DueDate >= start && p.DueDate <= end
                        && p.Status != "CANCELLED" && p.Status != "COMPLETED")
            .Select(p => new CalendarMarkerDto("PROJECT", p.Id, p.Id, p.Name, p.Name, p.DueDate!.Value, p.Status))
            .ToListAsync();

        var markers = deliverableMarkers.Concat(projectMarkers)
            .OrderBy(m => m.Date).ThenBy(m => m.Kind).ThenBy(m => m.Title)
            .ToList();

        return Results.Ok(new CalendarResponse(tasks, markers, truncated));
    }

    /// <summary>
    /// Calendar drag: moves the due date (a start date keeps its gap) and logs task.rescheduled. Same date = no-op, no log.
    /// A DONE task or a task of a CANCELLED project → 409 INVALID_TRANSITION.
    /// </summary>
    private static async Task<IResult> Reschedule(
        Guid id,
        RescheduleTaskRequest body,
        AppDbContext db,
        ActivityWriter activity,
        IClock clock)
    {
        var task = await db.Tasks.FindAsync(id);
        if (task == null) return Results.NotFound();
        if (body.DueDate is not { } newDue)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["dueDate"] = ["Vui lòng chọn hạn hoàn thành"] });

        if (task.DueDate == newDue) return Results.Ok(await TasksEndpoints.LoadDetailAsync(db, id, clock.TodayVn));
        if (await db.Projects.AnyAsync(p => p.Id == task.ProjectId && p.Status == "CANCELLED"))
            return Results.Conflict(new { code = "INVALID_TRANSITION", detail = "Dự án đã huỷ — không đổi hạn được" });

        var fromDue = task.DueDate;
        try { task.Reschedule(newDue); }
        catch (DomainException ex) when (ex.Code == "INVALID_TRANSITION")
        {
            return Results.Conflict(new { code = ex.Code, detail = ex.Detail });
        }
        activity.Write("task", task.Id, "task.rescheduled",
            clientId: await TasksEndpoints.ClientIdOfAsync(db, task.ProjectId), projectId: task.ProjectId,
            data: new { from = fromDue, to = newDue, title = task.Title });
        await db.SaveChangesAsync();

        return Results.Ok(await TasksEndpoints.LoadDetailAsync(db, id, clock.TodayVn));
    }

    private static bool TryParseDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}
