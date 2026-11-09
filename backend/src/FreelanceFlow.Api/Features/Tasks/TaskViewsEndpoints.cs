using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreelanceFlow.Api.Features.Tasks;

/// <summary>
/// P15 D3: saved task list views and the last used view, stored server-side for the single designer. Not logged:
/// no status, money, quota or portal change (same rule as checklist/notes).
/// </summary>
public static class TaskViewsEndpoints
{
    private const string NameIndex = "ux_task_views_name";

    public static void MapTaskViewsEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization();

        admin.MapGet("task-views", ListViews);
        admin.MapPost("task-views", CreateView);
        admin.MapPut("task-views/last", SaveLastView);   // literal before {id:guid}
        admin.MapPut("task-views/{id:guid}", UpdateView);
        admin.MapDelete("task-views/{id:guid}", DeleteView);
    }

    private static async Task<IResult> ListViews(AppDbContext db)
    {
        var views = (await db.TaskViews.AsNoTracking()
                .OrderBy(v => v.SortOrder).ThenBy(v => v.CreatedAt)
                .Select(v => new { v.Id, v.Name, v.ConfigJson, v.SortOrder })
                .ToListAsync())
            .Select(v => new TaskViewDto(v.Id, v.Name, TaskViewConfig.Parse(v.ConfigJson), v.SortOrder))
            .ToList();
        var last = await db.TaskViewStates.AsNoTracking()
            .Where(s => s.Id == TaskViewState.SingletonId)
            .Select(s => s.ConfigJson)
            .FirstOrDefaultAsync();

        return Results.Ok(new TaskViewsResponse(views, last == null ? null : TaskViewConfig.Parse(last)));
    }

    private static async Task<IResult> CreateView(SaveTaskViewRequest body, AppDbContext db)
    {
        var (name, config, errors) = Validate(body);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        if (await NameTakenAsync(db, name, null)) return DuplicateName();
        if (await db.TaskViews.CountAsync() >= TaskView.MaxCount)
            return Results.Conflict(new { code = "CONFLICT", detail = $"Tối đa {TaskView.MaxCount} chế độ xem" });

        var view = new TaskView
        {
            Name = name,
            ConfigJson = config.ToJson(),
            SortOrder = (await db.TaskViews.MaxAsync(v => (int?)v.SortOrder) ?? -1) + 1,
        };
        db.TaskViews.Add(view);
        if (!await TrySaveAsync(db)) return DuplicateName();

        return Results.Created($"/api/admin/task-views/{view.Id}", new TaskViewDto(view.Id, view.Name, config, view.SortOrder));
    }

    private static async Task<IResult> UpdateView(Guid id, SaveTaskViewRequest body, AppDbContext db)
    {
        var view = await db.TaskViews.FindAsync(id);
        if (view == null) return Results.NotFound();

        var (name, config, errors) = Validate(body);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        if (await NameTakenAsync(db, name, id)) return DuplicateName();

        view.Name = name;
        view.ConfigJson = config.ToJson();
        if (!await TrySaveAsync(db)) return DuplicateName();

        return Results.Ok(new TaskViewDto(view.Id, view.Name, config, view.SortOrder));
    }

    private static async Task<IResult> DeleteView(Guid id, AppDbContext db)
    {
        var view = await db.TaskViews.FindAsync(id);
        if (view == null) return Results.NotFound();

        db.TaskViews.Remove(view);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // Upsert of the single task_view_state row in one statement: two tabs saving the first state at once both succeed
    private static async Task<IResult> SaveLastView(SaveLastViewRequest body, AppDbContext db, IClock clock)
    {
        TaskViewConfig config;
        try { config = TaskViewConfig.Validate(body.Config); }
        catch (DomainException ex) { return ConfigProblem(ex); }

        var json = config.ToJson();
        var now = clock.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO task_view_state (id, config, updated_at)
            VALUES ({TaskViewState.SingletonId}, {json}::jsonb, {now})
            ON CONFLICT (id) DO UPDATE SET config = EXCLUDED.config, updated_at = EXCLUDED.updated_at
            """);

        return Results.NoContent();
    }

    private static (string Name, TaskViewConfig Config, Dictionary<string, string[]> Errors) Validate(SaveTaskViewRequest body)
    {
        var errors = new Dictionary<string, string[]>();
        var name = body.Name?.Trim() ?? "";
        if (name.Length == 0) errors["name"] = ["Vui lòng nhập tên chế độ xem"];
        else if (name.Length > TaskView.MaxName) errors["name"] = [$"Tối đa {TaskView.MaxName} ký tự"];

        TaskViewConfig config = null!;
        try { config = TaskViewConfig.Validate(body.Config); }
        catch (DomainException ex) { errors["config"] = [ex.Detail ?? ex.Code]; }
        return (name, config, errors);
    }

    // Fast path for the common case; ux_task_views_name (lower(name)) stays the guarantee — see TrySaveAsync
    private static Task<bool> NameTakenAsync(AppDbContext db, string name, Guid? excludeId)
    {
        var lower = name.ToLowerInvariant();
        return db.TaskViews.AnyAsync(v => v.Name.ToLower() == lower && v.Id != excludeId);
    }

    /// <summary>false when ux_task_views_name refused the name (a concurrent save of the same name won the race).</summary>
    private static async Task<bool> TrySaveAsync(AppDbContext db)
    {
        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
                                          {
                                              SqlState: PostgresErrorCodes.UniqueViolation,
                                              ConstraintName: NameIndex,
                                          })
        {
            return false;
        }
    }

    private static IResult DuplicateName() =>
        Results.Conflict(new { code = "CONFLICT", detail = "Đã có chế độ xem trùng tên" });

    private static IResult ConfigProblem(DomainException ex) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["config"] = [ex.Detail ?? ex.Code] });
}
