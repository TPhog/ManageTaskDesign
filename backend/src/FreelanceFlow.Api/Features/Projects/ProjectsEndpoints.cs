using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Domain.Revisions;
using FreelanceFlow.Api.Domain.Tasks;
using FreelanceFlow.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Projects;

public static class ProjectsEndpoints
{
    public static void MapProjectsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/projects").RequireAuthorization();

        group.MapGet("", ListProjects);
        group.MapPost("", CreateProject);
        group.MapGet("{id:guid}", GetProject);
        group.MapPut("{id:guid}", UpdateProject);
        group.MapPost("{id:guid}/status", ChangeStatus);
        group.MapGet("{id:guid}/activity", GetActivity);
    }

    private static async Task<IResult> ListProjects(
        AppDbContext db,
        IClock clock,
        string? status = null,
        Guid? clientId = null,
        string? q = null,
        string? dueBefore = null,
        int page = 1,
        int pageSize = 20,
        string? sort = null)
    {
        pageSize = Math.Min(pageSize, 100);
        var query = db.Projects.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(p => p.Status == status.ToUpperInvariant());

        if (clientId.HasValue)
            query = query.Where(p => p.ClientId == clientId.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var lower = q.ToLowerInvariant();
            query = query.Where(p => p.Name.ToLower().Contains(lower));
        }

        if (!string.IsNullOrWhiteSpace(dueBefore) && DateOnly.TryParse(dueBefore, out var dueDateFilter))
            query = query.Where(p => p.DueDate != null && p.DueDate <= dueDateFilter);

        // Sort
        query = sort switch
        {
            "dueDate:asc" => query.OrderBy(p => p.DueDate),
            "dueDate:desc" => query.OrderByDescending(p => p.DueDate),
            "name:asc" => query.OrderBy(p => p.Name),
            "createdAt:desc" => query.OrderByDescending(p => p.CreatedAt),
            _ => query.OrderByDescending(p => p.CreatedAt),
        };

        var total = await query.CountAsync();
        var today = clock.TodayVn;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.ClientId,
                p.Name,
                p.DesignType,
                p.Status,
                p.StartDate,
                p.DueDate,
                p.CreatedAt,
                Client = db.Clients
                    .Where(c => c.Id == p.ClientId)
                    .Select(c => new { c.Id, c.Name, c.Company })
                    .FirstOrDefault(),
                DeliverableCount = db.Deliverables.Count(d => d.ProjectId == p.Id),
            })
            .ToListAsync();

        return Results.Ok(new { items, page, pageSize, total });
    }

    private static async Task<IResult> CreateProject(
        CreateProjectRequest body,
        AppDbContext db,
        ActivityWriter activity,
        IClock clock)
    {
        var clientExists = await db.Clients.AnyAsync(c => c.Id == body.ClientId && c.Status == "ACTIVE");
        if (!clientExists)
            return Results.NotFound(new { code = "NOT_FOUND", detail = "Khách hàng không tồn tại hoặc đã lưu trữ" });

        var project = new Project
        {
            ClientId = body.ClientId,
            Name = body.Name,
            DesignType = body.DesignType,
            StartDate = body.StartDate,
            DueDate = body.DueDate,
            InternalNotes = body.InternalNotes ?? "",
        };

        // Auto-create brief v1 (BR-25/26)
        var brief = new ProjectBrief
        {
            ProjectId = project.Id,
            Version = 1,
            Status = "DRAFT",
            Content = "",
        };

        db.Projects.Add(project);
        db.ProjectBriefs.Add(brief);

        activity.Write("project", project.Id, "project.created",
            clientId: body.ClientId, projectId: project.Id,
            data: new { name = body.Name, clientId = body.ClientId });

        await db.SaveChangesAsync();

        return Results.Created($"/api/admin/projects/{project.Id}",
            new { project.Id, project.ClientId, project.Name, project.DesignType, project.Status, project.CreatedAt });
    }

    private static async Task<IResult> GetProject(Guid id, AppDbContext db, IClock clock)
    {
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (project == null) return Results.NotFound();

        var client = await db.Clients.AsNoTracking()
            .Where(c => c.Id == project.ClientId)
            .Select(c => new { c.Id, c.Name, c.Company })
            .FirstOrDefaultAsync();

        // Deliverables with used_rounds calculation (BR-5.1)
        var deliverables = await db.Deliverables.AsNoTracking()
            .Where(d => d.ProjectId == id)
            .OrderBy(d => d.SortOrder).ThenBy(d => d.CreatedAt)
            .ToListAsync();

        // used_rounds (docs/04 §4.1): the single query lives in Domain/Revisions
        var usedRoundsMap = await RevisionQuota.UsedRounds(db.RevisionRounds.Where(r => r.ProjectId == id), db.RevisionItems)
            .ToDictionaryAsync(x => x.DeliverableId, x => x.UsedRounds);

        // Plan 06b F3: % of the deliverable's tasks that are DONE (the single query lives in Domain/Tasks)
        var taskCounts = await TaskProgress.PerDeliverable(db.Tasks.Where(t => t.ProjectId == id))
            .ToDictionaryAsync(x => x.DeliverableId);

        var deliverableDtos = deliverables.Select(d =>
        {
            var used = usedRoundsMap.GetValueOrDefault(d.Id);
            var tasks = taskCounts.GetValueOrDefault(d.Id);
            var taskTotal = tasks?.Total ?? 0;
            var taskDone = tasks?.Done ?? 0;
            return new
            {
                d.Id,
                d.Name,
                d.Quantity,
                d.ServiceId,
                d.Status,
                d.IncludedRounds,
                UsedRounds = used,
                RemainingRounds = Math.Max(0, d.IncludedRounds - used),
                d.ExtraRoundPrice,
                d.DueDate,
                d.SortOrder,
                TaskTotal = taskTotal,
                TaskDone = taskDone,
                ProgressPercent = TaskProgress.Percent(taskTotal, taskDone),
                d.ClientDecision,
                d.ClientDecisionAt,
            };
        });

        // Money aggregates
        var moneyStats = await db.Quotations
            .Where(q => q.ProjectId == id && q.Status == "ACCEPTED")
            .GroupBy(_ => 1)
            .Select(g => new
            {
                AcceptedTotal = g.Sum(q => q.Total),
                Outstanding = g.Sum(q =>
                    q.PaymentStatus == "PAID" ? 0L :
                    q.PaymentStatus == "DEPOSIT_PAID" ? q.Total - q.DepositAmount :
                    q.Total),
            })
            .FirstOrDefaultAsync();

        var pendingQuotationTotal = await db.Quotations
            .Where(q => q.ProjectId == id && q.Status == "SENT")
            .SumAsync(q => (long?)q.Total) ?? 0L;

        // Counts
        var newRequests = await db.ClientRequests.CountAsync(r => r.ProjectId == id && r.Status == "NEW");
        var openTasks = await db.Tasks.CountAsync(t => t.ProjectId == id && t.Status != "DONE");
        var overdueTasks = await db.Tasks
            .CountAsync(t => t.ProjectId == id && t.Status != "DONE" &&
                             t.DueDate != null && t.DueDate < clock.TodayVn);

        var brief = await db.ProjectBriefs.AsNoTracking()
            .Where(b => b.ProjectId == id)
            .OrderByDescending(b => b.Version)
            .Select(b => new { b.Id, b.Version, b.Status, b.Content, b.ConfirmedAt, b.CreatedAt, b.UpdatedAt })
            .FirstOrDefaultAsync();

        var rowVersion = await db.Projects
            .Where(p => p.Id == id)
            .Select(p => EF.Property<uint>(p, "xmin"))
            .FirstOrDefaultAsync();

        return Results.Ok(new
        {
            project.Id,
            project.ClientId,
            project.Name,
            project.DesignType,
            project.Status,
            project.StatusReason,
            project.StatusReasonPublic,
            project.StartDate,
            project.DueDate,
            project.InternalNotes,
            project.CreatedAt,
            project.UpdatedAt,
            RowVersion = rowVersion,
            Client = client,
            Deliverables = deliverableDtos,
            Brief = brief,
            Money = new
            {
                AcceptedTotal = moneyStats?.AcceptedTotal ?? 0,
                Outstanding = moneyStats?.Outstanding ?? 0,
                PendingQuotationTotal = pendingQuotationTotal,
            },
            Counts = new { NewRequests = newRequests, OpenTasks = openTasks, OverdueTasks = overdueTasks },
        });
    }

    private static async Task<IResult> UpdateProject(
        Guid id,
        UpdateProjectRequest body,
        AppDbContext db,
        ActivityWriter activity)
    {
        var project = await db.Projects.FindAsync(id);
        if (project == null) return Results.NotFound();

        project.Name = body.Name;
        project.DesignType = body.DesignType;
        project.StartDate = body.StartDate;
        project.DueDate = body.DueDate;
        project.InternalNotes = body.InternalNotes ?? "";

        activity.Write("project", project.Id, "project.updated",
            clientId: project.ClientId, projectId: project.Id,
            data: new { name = body.Name });

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { code = "CONCURRENCY_CONFLICT", detail = "Dữ liệu đã thay đổi, vui lòng tải lại" });
        }

        return Results.Ok(new
        {
            project.Id, project.Name, project.DesignType, project.Status,
            project.StartDate, project.DueDate, project.UpdatedAt,
        });
    }

    private static async Task<IResult> ChangeStatus(
        Guid id,
        ChangeStatusRequest body,
        AppDbContext db,
        ActivityWriter activity,
        IClock clock)
    {
        var project = await db.Projects.FindAsync(id);
        if (project == null) return Results.NotFound();

        // Gather context data for warning checks
        var hasAcceptedInitial = await db.Quotations
            .AnyAsync(q => q.ProjectId == id && q.Kind == "INITIAL" && q.Status == "ACCEPTED");

        var hasIncompleteDeliverables = body.To == "COMPLETED" && await db.Deliverables
            .AnyAsync(d => d.ProjectId == id &&
                           d.Status != "DELIVERED" && d.Status != "CANCELLED");

        var hasUnpaidAccepted = body.To == "COMPLETED" && await db.Quotations
            .AnyAsync(q => q.ProjectId == id && q.Status == "ACCEPTED" && q.PaymentStatus != "PAID");

        string[] warnings;
        try
        {
            warnings = project.ValidateTransition(
                body.To,
                body.Reason,
                hasAcceptedInitial,
                hasIncompleteDeliverables,
                hasUnpaidAccepted);
        }
        catch (DomainException ex) when (ex.Code == "INVALID_TRANSITION")
        {
            return Results.Conflict(new { code = ex.Code, detail = ex.Detail });
        }
        catch (DomainException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["reason"] = [ex.Detail ?? ex.Code]
            });
        }

        // If there are warnings and designer hasn't confirmed, return 422 with warnings
        if (warnings.Length > 0 && body.ConfirmWarnings != true)
        {
            return Results.UnprocessableEntity(new
            {
                code = "BUSINESS_RULE",
                detail = "Có cảnh báo cần xác nhận",
                warnings,
            });
        }

        var fromStatus = project.Status;
        project.ApplyStatusChange(body.To, body.Reason, body.ReasonPublic);

        var clientMessage = body.To switch
        {
            "IN_PROGRESS" => "Dự án chuyển sang: Đang thực hiện",
            "ON_HOLD" => body.ReasonPublic == true ? $"Dự án tạm dừng: {body.Reason}" : null,
            "COMPLETED" => "Dự án chuyển sang: Hoàn thành",
            "CANCELLED" => null,
            _ => null,
        };

        activity.Write("project", project.Id, "project.status_changed",
            clientId: project.ClientId, projectId: project.Id,
            data: new { from = fromStatus, to = body.To, reason = body.Reason },
            visibleToClient: clientMessage != null,
            clientMessage: clientMessage);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { code = "CONCURRENCY_CONFLICT", detail = "Dữ liệu đã thay đổi, vui lòng tải lại" });
        }

        return Results.Ok(new
        {
            project.Id,
            project.Status,
            project.StatusReason,
            project.StatusReasonPublic,
            project.UpdatedAt,
        });
    }

    private static async Task<IResult> GetActivity(
        Guid id,
        AppDbContext db,
        int page = 1,
        int pageSize = 20,
        bool visibleOnly = false)
    {
        pageSize = Math.Min(pageSize, 100);
        var exists = await db.Projects.AnyAsync(p => p.Id == id);
        if (!exists) return Results.NotFound();

        var query = db.ActivityLogs.Where(a => a.ProjectId == id);
        if (visibleOnly) query = query.Where(a => a.VisibleToClient && a.ClientMessage != null);
        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.OccurredAt,
                a.ActorType,
                a.EntityType,
                a.EntityId,
                a.Action,
                a.Data,
                a.VisibleToClient,
                a.ClientMessage,
            })
            .ToListAsync();

        return Results.Ok(new { items, page, pageSize, total });
    }
}

record CreateProjectRequest(
    Guid ClientId,
    string Name,
    string DesignType,
    DateOnly? StartDate,
    DateOnly? DueDate,
    string? InternalNotes);

record UpdateProjectRequest(
    string Name,
    string DesignType,
    DateOnly? StartDate,
    DateOnly? DueDate,
    string? InternalNotes,
    uint? RowVersion);

record ChangeStatusRequest(
    string To,
    string? Reason,
    bool? ReasonPublic,
    bool? ConfirmWarnings,
    uint? RowVersion);
