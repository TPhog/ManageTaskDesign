using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Catalog;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Flow;
using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Domain.Quotations;
using FreelanceFlow.Api.Domain.Tasks;
using FreelanceFlow.Api.Features.Tasks;
using FreelanceFlow.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Projects;

/// <summary>Plan 06b: derived project stage + next steps, and "Bắt đầu dự án" with the service task templates.</summary>
public static class ProjectFlowEndpoints
{
    private const int MaxStartEntries = 50;

    public static void MapProjectFlowEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization();

        admin.MapGet("projects/{id:guid}/flow", GetFlow);
        admin.MapGet("flow/actions", ListActions);
        admin.MapGet("projects/{id:guid}/start-preview", StartPreview);
        admin.MapPost("projects/{id:guid}/start", StartProject);
    }

    private static async Task<IResult> GetFlow(Guid id, AppDbContext db, IClock clock)
    {
        var today = clock.TodayVn;
        var facts = await ProjectFlowReads.LoadFactsAsync(db, [id], today);
        return facts.TryGetValue(id, out var f) ? Results.Ok(ProjectFlow.Evaluate(f, today)) : Results.NotFound();
    }

    /// <summary>Dashboard "Việc cần làm" (plan 07): open, non-archived projects.</summary>
    private static async Task<IResult> ListActions(AppDbContext db, IClock clock)
    {
        var today = clock.TodayVn;
        var projects = await (
                from p in db.Projects.AsNoTracking()
                join c in db.Clients on p.ClientId equals c.Id
                where p.ArchivedAt == null && p.Status != "COMPLETED" && p.Status != "CANCELLED"
                orderby p.DueDate == null, p.DueDate, p.CreatedAt descending
                select new { p.Id, p.Name, ClientName = c.Name })
            .ToListAsync();

        var facts = await ProjectFlowReads.LoadFactsAsync(db, projects.Select(p => p.Id).ToList(), today);

        return Results.Ok(projects.Select(p =>
        {
            var state = ProjectFlow.Evaluate(facts[p.Id], today);
            return new FlowProjectActionsDto(p.Id, p.Name, p.ClientName, state.Stage, state.Actions, state.Warnings);
        }).ToList());
    }

    private static async Task<IResult> StartPreview(Guid id, AppDbContext db)
    {
        var project = await db.Projects.AsNoTracking().Where(p => p.Id == id).Select(p => new { p.DueDate }).FirstOrDefaultAsync();
        if (project is null) return Results.NotFound();

        var rows = await (
                from d in db.Deliverables.AsNoTracking()
                join s in db.Services on d.ServiceId equals s.Id into services
                from s in services.DefaultIfEmpty()
                where d.ProjectId == id && d.Status == "NOT_STARTED"
                orderby d.SortOrder, d.CreatedAt
                select new
                {
                    d.Id,
                    d.Name,
                    ServiceName = s == null ? null : s.Name,
                    Template = s == null ? null : s.TaskTemplateJson,
                    d.DueDate,
                })
            .ToListAsync();

        return Results.Ok(rows.Select(r => new StartPreviewItemDto(
            r.Id, r.Name, r.ServiceName,
            r.Template is null ? [] : Service.ParseTaskTemplate(r.Template),
            r.DueDate ?? project.DueDate)).ToList());   // P14 DF-19: "Hạn chung" defaults to the deliverable's, else the project's
    }

    /// <summary>
    /// One transaction (single SaveChangesAsync): project DRAFT/QUOTED → IN_PROGRESS, every NOT_STARTED deliverable →
    /// IN_PROGRESS, one TODO task per title. Warnings are returned, never blocking (F4). BR-01: the designer pressed it.
    /// </summary>
    private static async Task<IResult> StartProject(
        Guid id, StartProjectRequest? body, AppDbContext db, ActivityWriter activity, IClock clock)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id);
        if (project is null) return Results.NotFound();
        if (body is null)
            return Results.BadRequest(new { code = "VALIDATION_FAILED", detail = "Thiếu nội dung yêu cầu" });
        db.Entry(project).Property<uint>("xmin").OriginalValue = body.RowVersion;

        var hasAcceptedInitial = await db.Quotations.AnyAsync(q =>
            q.ProjectId == id && q.Kind == QuotationKinds.Initial && q.Status == QuotationStatuses.Accepted);
        var fromStatus = project.Status;
        string[] warnings;
        try
        {
            warnings = project.Start(hasAcceptedInitial);   // before the entries: a started project is 409, not a 400
        }
        catch (DomainException ex)
        {
            return Results.Conflict(new { code = ex.Code, detail = ex.Detail });
        }

        var deliverables = await db.Deliverables.Where(d => d.ProjectId == id).ToListAsync();
        var entries = body.Tasks ?? [];
        var errors = Validate(entries, deliverables);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var started = 0;
        foreach (var d in deliverables.Where(d => d.Status == "NOT_STARTED"))
        {
            d.Start();
            started++;
            activity.Write("deliverable", d.Id, "deliverable.status_changed",
                clientId: project.ClientId, projectId: id,
                data: new { from = "NOT_STARTED", to = d.Status, name = d.Name, via = "project.start" });
        }

        var sortOrder = await TasksEndpoints.NextSortOrderAsync(db, id, "TODO");
        var created = 0;
        foreach (var entry in entries)
        {
            foreach (var title in entry.Titles ?? [])
            {
                var task = ProjectTask.Create(id, title, "", "TODO", "MEDIUM", null, entry.DueDate, entry.DeliverableId, sortOrder, clock);
                sortOrder += ProjectTask.SortStep;
                db.Tasks.Add(task);
                created++;
                activity.Write("task", task.Id, "task.created",
                    clientId: project.ClientId, projectId: id,
                    data: new { title = task.Title, status = task.Status, deliverableId = entry.DeliverableId });
            }
        }

        activity.Write("project", id, "project.started",
            clientId: project.ClientId, projectId: id,
            data: new { from = fromStatus, to = project.Status, deliverablesStarted = started, tasksCreated = created },
            visibleToClient: true,
            clientMessage: "Dự án chuyển sang: Đang thực hiện");

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { code = "CONCURRENCY_CONFLICT", detail = "Dữ liệu đã thay đổi, vui lòng tải lại" });
        }

        return Results.Ok(new StartProjectResponse(id, project.Status, started, created, warnings));
    }

    private static Dictionary<string, string[]> Validate(List<StartProjectTasks> entries, List<Deliverable> deliverables)
    {
        var errors = new Dictionary<string, string[]>();
        if (entries.Count > MaxStartEntries)
        {
            errors["tasks"] = [$"Tối đa {MaxStartEntries} hạng mục"];
            return errors;
        }

        var seen = new HashSet<Guid>();
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var deliverable = deliverables.FirstOrDefault(d => d.Id == entry.DeliverableId);
            if (deliverable is null || deliverable.Status == "CANCELLED" || !seen.Add(entry.DeliverableId))
                errors[$"tasks[{i}].deliverableId"] = ["Hạng mục không thuộc dự án này hoặc đã huỷ"];

            var titles = entry.Titles ?? [];
            if (titles.Count > Service.MaxTaskTemplateSteps)
                errors[$"tasks[{i}].titles"] = [$"Tối đa {Service.MaxTaskTemplateSteps} công việc mỗi hạng mục"];
            else if (titles.Any(t => string.IsNullOrWhiteSpace(t) || t.Trim().Length > TaskValidation.TitleMaxLength))
                errors[$"tasks[{i}].titles"] = [$"Tên công việc dài 1–{TaskValidation.TitleMaxLength} ký tự"];
        }
        return errors;
    }
}

public sealed record FlowProjectActionsDto(
    Guid ProjectId, string ProjectName, string ClientName, string Stage,
    IReadOnlyList<FlowAction> Actions, IReadOnlyList<FlowWarning> Warnings);

public sealed record StartPreviewItemDto(
    Guid DeliverableId, string Name, string? ServiceName, IReadOnlyList<string> Titles, DateOnly? SuggestedDueDate);

/// <summary>DueDate (P14 DF-19): one shared due date for every task created from this entry (start date stays empty).</summary>
public sealed record StartProjectTasks(Guid DeliverableId, List<string>? Titles, DateOnly? DueDate = null);

public sealed record StartProjectRequest(uint RowVersion, List<StartProjectTasks>? Tasks);

public sealed record StartProjectResponse(Guid ProjectId, string Status, int StartedDeliverables, int CreatedTasks, string[] Warnings);
