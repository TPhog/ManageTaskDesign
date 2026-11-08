using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Files;
using FreelanceFlow.Api.Domain.Tasks;
using FreelanceFlow.Api.Features.Images;
using FreelanceFlow.Api.Features.Storage;
using FreelanceFlow.Api.Infrastructure;
using FreelanceFlow.Api.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Tasks;

public static class TasksEndpoints
{
    public static void MapTasksEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization();

        admin.MapGet("tasks", ListTasks);
        admin.MapGet("tasks/{id:guid}", GetTask);
        admin.MapPost("projects/{projectId:guid}/tasks", CreateTask);
        admin.MapPut("tasks/{id:guid}", UpdateTask);
        admin.MapPost("tasks/{id:guid}/move", MoveTask);
        admin.MapDelete("tasks/{id:guid}", DeleteTask);
        admin.MapPost("tasks/{id:guid}/checklist", AddChecklistItem);
        admin.MapPut("tasks/{id:guid}/checklist/{itemId:guid}", UpdateChecklistItem);
        admin.MapDelete("tasks/{id:guid}/checklist/{itemId:guid}", RemoveChecklistItem);
        admin.MapGet("tasks/{id:guid}/notes", ListNotes);
        admin.MapPost("tasks/{id:guid}/notes", AddNote);
        admin.MapPut("tasks/{id:guid}/notes/{noteId:guid}", UpdateNote);
        admin.MapDelete("tasks/{id:guid}/notes/{noteId:guid}", DeleteNote);
    }

    private static async Task<IResult> ListTasks(
        AppDbContext db,
        IClock clock,
        Guid? projectId = null,
        string? status = null,
        string? due = null,
        string? q = null,
        Guid? clientId = null,
        int page = 1,
        int pageSize = 20)
    {
        var errors = TaskValidation.ListFilters(status, due);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        page = Math.Clamp(page, 1, 100_000);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var today = clock.TodayVn;
        var weekEnd = ProjectTask.EndOfWeek(today);

        var query = from t in db.Tasks.AsNoTracking()
                    join p in db.Projects on t.ProjectId equals p.Id
                    join c in db.Clients on p.ClientId equals c.Id
                    select new { Task = t, Project = p, ClientName = c.Name };

        query = projectId is { } pid
            ? query.Where(x => x.Task.ProjectId == pid)
            : query.Where(x => x.Project.Status != "CANCELLED"); // BR-82

        if (status != null)
            query = query.Where(x => x.Task.Status == status);

        if (clientId is { } cid)
            query = query.Where(x => x.Project.ClientId == cid);

        // QA D-06/D-18: free-text search over the task title and its project name
        if (!string.IsNullOrWhiteSpace(q))
        {
            var lower = q.Trim().ToLowerInvariant();
            query = query.Where(x => x.Task.Title.ToLower().Contains(lower) || x.Project.Name.ToLower().Contains(lower));
        }

        // Due buckets use today(VN) and skip DONE (matches partial index ix_tasks_due)
        query = due switch
        {
            "overdue" => query.Where(x => x.Task.DueDate < today && x.Task.Status != "DONE"),
            "today" => query.Where(x => x.Task.DueDate == today && x.Task.Status != "DONE"),
            "week" => query.Where(x => x.Task.DueDate >= today && x.Task.DueDate <= weekEnd && x.Task.Status != "DONE"),
            _ => query,
        };

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(x => x.Task.DueDate == null)
            .ThenBy(x => x.Task.DueDate)
            .ThenBy(x => x.Task.SortOrder)
            .ThenBy(x => x.Task.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new TaskListItemDto(
                x.Task.Id,
                x.Task.ProjectId,
                x.Project.Name,
                x.ClientName,
                x.Task.DeliverableId,
                db.Deliverables.Where(d => d.Id == x.Task.DeliverableId).Select(d => d.Name).FirstOrDefault(),
                x.Task.Title,
                x.Task.Status,
                x.Task.Priority,
                x.Task.StartDate,
                x.Task.DueDate,
                x.Task.SortOrder,
                x.Task.CompletedAt,
                // BR-81 — same rule as ProjectTask.IsOverdue, written out so EF translates it
                x.Task.DueDate != null && x.Task.DueDate < today && x.Task.Status != "DONE",
                db.TaskChecklistItems.Count(i => i.TaskId == x.Task.Id),
                db.TaskChecklistItems.Count(i => i.TaskId == x.Task.Id && i.Done),
                x.Task.CreatedAt,
                x.Task.UpdatedAt,
                x.Task.Description != "",
                db.TaskNotes.Count(n => n.TaskId == x.Task.Id)))
            .ToListAsync();

        return Results.Ok(new { items, page, pageSize, total });
    }

    private static async Task<IResult> GetTask(Guid id, AppDbContext db, IClock clock)
    {
        var dto = await LoadDetailAsync(db, id, clock.TodayVn);
        return dto == null ? Results.NotFound() : Results.Ok(dto);
    }

    private static async Task<IResult> CreateTask(
        Guid projectId,
        CreateTaskRequest body,
        AppDbContext db,
        ActivityWriter activity,
        IClock clock)
    {
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId);
        if (project == null) return Results.NotFound();

        var errors = TaskValidation.TaskFields(body.Title, body.Status, body.Priority, body.Description);
        await CheckDeliverableAsync(db, projectId, body.DeliverableId, errors);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var status = body.Status ?? "TODO";
        var task = ProjectTask.Create(
            projectId,
            body.Title!,
            body.Description ?? "",
            status,
            body.Priority ?? "MEDIUM",
            body.StartDate,
            body.DueDate,
            body.DeliverableId,
            await NextSortOrderAsync(db, projectId, status),
            clock);
        db.Tasks.Add(task);

        activity.Write("task", task.Id, "task.created",
            clientId: project.ClientId, projectId: projectId,
            data: new { title = task.Title, status = task.Status });

        await db.SaveChangesAsync();

        return Results.Created($"/api/admin/tasks/{task.Id}", await LoadDetailAsync(db, task.Id, clock.TodayVn));
    }

    private static async Task<IResult> UpdateTask(
        Guid id,
        UpdateTaskRequest body,
        AppDbContext db,
        ActivityWriter activity,
        IClock clock)
    {
        var task = await db.Tasks.FindAsync(id);
        if (task == null) return Results.NotFound();

        var errors = TaskValidation.TaskFields(body.Title, body.Status, body.Priority, body.Description);
        await CheckDeliverableAsync(db, task.ProjectId, body.DeliverableId, errors);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var fromStatus = task.Status;
        task.Edit(body.Title!, body.Description ?? "", body.Priority ?? task.Priority,
            body.StartDate, body.DueDate, body.DeliverableId);
        // Status changed from the drawer: the card goes to the bottom of its new column
        if (body.Status != null && body.Status != task.Status)
            task.Move(body.Status, await NextSortOrderAsync(db, task.ProjectId, body.Status), clock);

        var clientId = await ClientIdOfAsync(db, task.ProjectId);
        activity.Write("task", task.Id, "task.updated",
            clientId: clientId, projectId: task.ProjectId,
            data: new { title = task.Title });
        if (task.Status != fromStatus)
            activity.Write("task", task.Id, "task.status_changed",
                clientId: clientId, projectId: task.ProjectId,
                data: new { from = fromStatus, to = task.Status, title = task.Title });

        await db.SaveChangesAsync();

        return Results.Ok(await LoadDetailAsync(db, id, clock.TodayVn));
    }

    // Kanban drag & drop: the client computes sortOrder (midpoint between neighbours, docs/10 M6.1)
    private static async Task<IResult> MoveTask(
        Guid id,
        MoveTaskRequest body,
        AppDbContext db,
        ActivityWriter activity,
        IClock clock)
    {
        var task = await db.Tasks.FindAsync(id);
        if (task == null) return Results.NotFound();

        var errors = TaskValidation.Move(body.Status, body.SortOrder);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var fromStatus = task.Status;
        if (task.Move(body.Status!, body.SortOrder!.Value, clock))
            activity.Write("task", task.Id, "task.status_changed",
                clientId: await ClientIdOfAsync(db, task.ProjectId), projectId: task.ProjectId,
                data: new { from = fromStatus, to = task.Status, title = task.Title });

        await db.SaveChangesAsync();

        return Results.Ok(await LoadDetailAsync(db, id, clock.TodayVn));
    }

    // Hard delete — tasks are not financial data (docs/05 §3.9); checklist rows and notes go with the FK cascade.
    // P10 (BR-88): the task's images go too, in the same transaction, except quotation evidence (BR-87), which stays.
    // The task row is locked first, so an image upload committing concurrently (it share-locks the row) is either
    // finished and included here, or finds the task gone.
    private static async Task<IResult> DeleteTask(
        Guid id, AppDbContext db, ActivityWriter activity, IFileStorage storage, IClock clock,
        StorageAlertService storageAlerts, ILoggerFactory loggers)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var locked = await db.Database.SqlQuery<Guid>($"SELECT id AS \"Value\" FROM tasks WHERE id = {id} FOR UPDATE").ToListAsync();
        if (locked.Count == 0) return Results.NotFound();
        var task = await db.Tasks.Include(t => t.ChecklistItems).FirstOrDefaultAsync(t => t.Id == id);
        if (task == null) return Results.NotFound();

        var taskImages = db.Files.Where(f => f.OwnerType == ImageOwnerTypes.Task && f.OwnerId == id && f.DeletedAt == null);
        var images = await taskImages.Where(f => !db.Quotations.Any(q => q.EvidenceFileId == f.Id)).ToListAsync();
        var kept = await taskImages.CountAsync() - images.Count;
        var clientId = await ClientIdOfAsync(db, task.ProjectId);

        db.Tasks.Remove(task);
        await ImageDeletion.MarkDeletedAsync(db, activity, clock, images, bulk: false, CancellationToken.None);
        activity.Write("task", task.Id, "task.deleted", clientId: clientId, projectId: task.ProjectId,
            data: new { title = task.Title, imagesDeleted = images.Count, imagesKept = kept });

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        if (images.Count > 0)
        {
            await ImageDeletion.DeleteObjectsAsync(storage, loggers.CreateLogger("FreelanceFlow.Images"), images);
            await storageAlerts.EvaluateSafelyAsync(CancellationToken.None);
        }
        return Results.NoContent();
    }

    // Checklist edits are not logged: no status, money, quota or portal change (CLAUDE.md activity rule)
    private static async Task<IResult> AddChecklistItem(
        Guid id,
        ChecklistItemRequest body,
        AppDbContext db,
        IClock clock)
    {
        var task = await db.Tasks.Include(t => t.ChecklistItems).FirstOrDefaultAsync(t => t.Id == id);
        if (task == null) return Results.NotFound();

        var errors = TaskValidation.ChecklistText(body.Text, body.Description);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var item = task.AddChecklistItem(body.Text!, body.Description ?? "");
        // Explicit Add: a new child with a preset Guid key found only through the navigation is treated as an UPDATE
        db.TaskChecklistItems.Add(item);
        await db.SaveChangesAsync();

        return Results.Created($"/api/admin/tasks/{id}", await LoadDetailAsync(db, id, clock.TodayVn));
    }

    private static async Task<IResult> UpdateChecklistItem(
        Guid id,
        Guid itemId,
        ChecklistItemRequest body,
        AppDbContext db,
        IClock clock)
    {
        var task = await db.Tasks.Include(t => t.ChecklistItems).FirstOrDefaultAsync(t => t.Id == id);
        if (task == null || task.ChecklistItems.All(i => i.Id != itemId)) return Results.NotFound();

        var errors = TaskValidation.ChecklistText(body.Text, body.Description);
        if (body.Done == null) errors["done"] = ["Bắt buộc"];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        task.UpdateChecklistItem(itemId, body.Text!, body.Done!.Value, body.Description);
        await db.SaveChangesAsync();

        return Results.Ok(await LoadDetailAsync(db, id, clock.TodayVn));
    }

    private static async Task<IResult> RemoveChecklistItem(Guid id, Guid itemId, AppDbContext db, IClock clock)
    {
        var task = await db.Tasks.Include(t => t.ChecklistItems).FirstOrDefaultAsync(t => t.Id == id);
        if (task == null || task.ChecklistItems.All(i => i.Id != itemId)) return Results.NotFound();

        db.TaskChecklistItems.Remove(task.RemoveChecklistItem(itemId));
        await db.SaveChangesAsync();

        return Results.Ok(await LoadDetailAsync(db, id, clock.TodayVn));
    }

    // ---------- notes (plan 06b F5): designer-only, not logged (same rule as the checklist) ----------

    private static async Task<IResult> ListNotes(Guid id, AppDbContext db)
    {
        if (!await db.Tasks.AnyAsync(t => t.Id == id)) return Results.NotFound();

        var notes = await db.TaskNotes.AsNoTracking()
            .Where(n => n.TaskId == id)
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Select(n => new TaskNoteDto(n.Id, n.Body, n.CreatedAt, n.UpdatedAt))
            .ToListAsync();

        return Results.Ok(notes);
    }

    private static async Task<IResult> AddNote(Guid id, TaskNoteRequest body, AppDbContext db)
    {
        if (!await db.Tasks.AnyAsync(t => t.Id == id)) return Results.NotFound();

        var errors = TaskValidation.NoteBody(body.Body);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var note = TaskNote.Create(id, body.Body!);
        db.TaskNotes.Add(note);
        await db.SaveChangesAsync();

        return Results.Created($"/api/admin/tasks/{id}/notes/{note.Id}", ToNoteDto(note));
    }

    private static async Task<IResult> UpdateNote(Guid id, Guid noteId, TaskNoteRequest body, AppDbContext db)
    {
        var note = await db.TaskNotes.FirstOrDefaultAsync(n => n.Id == noteId && n.TaskId == id);
        if (note == null) return Results.NotFound();

        var errors = TaskValidation.NoteBody(body.Body);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        note.Edit(body.Body!);
        await db.SaveChangesAsync();

        return Results.Ok(ToNoteDto(note));
    }

    private static async Task<IResult> DeleteNote(Guid id, Guid noteId, AppDbContext db)
    {
        var note = await db.TaskNotes.FirstOrDefaultAsync(n => n.Id == noteId && n.TaskId == id);
        if (note == null) return Results.NotFound();

        db.TaskNotes.Remove(note);
        await db.SaveChangesAsync();

        return Results.NoContent();
    }

    private static TaskNoteDto ToNoteDto(TaskNote n) => new(n.Id, n.Body, n.CreatedAt, n.UpdatedAt);

    internal static async Task<TaskDetailDto?> LoadDetailAsync(AppDbContext db, Guid id, DateOnly today)
    {
        var task = await db.Tasks.AsNoTracking()
            .Include(t => t.ChecklistItems)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (task == null) return null;

        var projectName = await db.Projects
            .Where(p => p.Id == task.ProjectId)
            .Select(p => p.Name)
            .FirstAsync();
        var deliverableName = task.DeliverableId == null
            ? null
            : await db.Deliverables
                .Where(d => d.Id == task.DeliverableId)
                .Select(d => d.Name)
                .FirstOrDefaultAsync();
        var notesCount = await db.TaskNotes.CountAsync(n => n.TaskId == id);

        return new TaskDetailDto(
            task.Id,
            task.ProjectId,
            projectName,
            task.DeliverableId,
            deliverableName,
            task.Title,
            task.Description,
            task.Status,
            task.Priority,
            task.StartDate,
            task.DueDate,
            task.SortOrder,
            task.CompletedAt,
            task.IsOverdue(today),
            task.ChecklistItems
                .OrderBy(i => i.SortOrder)
                .Select(i => new ChecklistItemDto(i.Id, i.Text, i.Done, i.SortOrder, i.Description))
                .ToList(),
            task.CreatedAt,
            task.UpdatedAt,
            task.RevisionItemId,
            notesCount);
    }

    // Appends to the bottom of the column
    internal static async Task<double> NextSortOrderAsync(AppDbContext db, Guid projectId, string status) =>
        (await db.Tasks
            .Where(t => t.ProjectId == projectId && t.Status == status)
            .MaxAsync(t => (double?)t.SortOrder) ?? 0) + ProjectTask.SortStep;

    // docs/04 §3: deliverable_id of a task must belong to the same project
    private static async Task CheckDeliverableAsync(
        AppDbContext db, Guid projectId, Guid? deliverableId, Dictionary<string, string[]> errors)
    {
        if (deliverableId is { } did &&
            !await db.Deliverables.AnyAsync(d => d.Id == did && d.ProjectId == projectId))
            errors["deliverableId"] = ["Hạng mục không thuộc dự án này"];
    }

    private static Task<Guid> ClientIdOfAsync(AppDbContext db, Guid projectId) =>
        db.Projects.Where(p => p.Id == projectId).Select(p => p.ClientId).FirstAsync();
}
