using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Files;
using FreelanceFlow.Api.Features.Images;
using FreelanceFlow.Api.Infrastructure;
using FreelanceFlow.Api.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Storage;

/// <summary>Settings › Dung lượng (spec §7): usage bar + filterable image list for cleanup.</summary>
public static class StorageEndpoints
{
    private static readonly string[] Sorts = ["size", "oldest", "newest"];

    public static void MapStorageEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/storage").RequireAuthorization();
        admin.MapGet("usage", async (StorageAlertService storage, CancellationToken ct) => Results.Ok(await storage.UsageAsync(ct)));
        admin.MapGet("images", ListImages);
        admin.MapGet("projects", ListProjects);
        admin.MapGet("original-cleanup", PreviewOriginalCleanup);
        admin.MapPost("original-cleanup", RunOriginalCleanup);
    }

    private static async Task<IResult> ListImages(
        AppDbContext db, IClock clock, Guid? projectId = null, bool completedOnly = false, int olderThanMonths = 0,
        string sort = "size", int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (olderThanMonths is < 0 or > 120) errors["olderThanMonths"] = ["Từ 0 đến 120 tháng"];
        if (!Sorts.Contains(sort)) errors["sort"] = ["size | oldest | newest"];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        page = Math.Clamp(page, 1, 100_000);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = from f in db.Files.AsNoTracking()
                    join p in db.Projects on f.ProjectId equals (Guid?)p.Id
                    join c in db.Clients on p.ClientId equals c.Id
                    where f.DeletedAt == null && f.OwnerType != null
                    select new { File = f, ProjectName = p.Name, ProjectStatus = p.Status, ClientName = c.Name };
        if (projectId is { } pid) query = query.Where(x => x.File.ProjectId == pid);
        if (completedOnly) query = query.Where(x => x.ProjectStatus == "COMPLETED");
        if (olderThanMonths > 0)
        {
            var cutoff = clock.UtcNow.AddMonths(-olderThanMonths);
            query = query.Where(x => x.File.CreatedAt < cutoff);
        }

        var total = await query.CountAsync(ct);
        var totalBytes = await query.SumAsync(x => x.File.SizeBytes, ct);
        var ordered = sort switch
        {
            "oldest" => query.OrderBy(x => x.File.CreatedAt).ThenBy(x => x.File.Id),
            "newest" => query.OrderByDescending(x => x.File.CreatedAt).ThenByDescending(x => x.File.Id),
            _ => query.OrderByDescending(x => x.File.SizeBytes).ThenBy(x => x.File.Id),
        };
        var rows = await ordered
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                x.File,
                x.ProjectName,
                x.ProjectStatus,
                x.ClientName,
                TaskTitle = x.File.OwnerType == ImageOwnerTypes.Task
                    ? db.Tasks.Where(t => t.Id == x.File.OwnerId).Select(t => t.Title).FirstOrDefault()
                    : null,
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new StorageImageDto(
            r.File.Id, r.File.ProjectId!.Value, r.ProjectName, r.ProjectStatus, r.ClientName, r.File.OwnerType!, r.File.OwnerId!.Value,
            r.TaskTitle, r.File.OriginalName, r.File.Width ?? 0, r.File.Height ?? 0, r.File.SizeBytes, r.File.VisibleToClient,
            r.File.UploadedBy, r.File.CreatedAt,
            ImageDtos.AdminUrl(r.File.Id, ImageVariants.Thumb), ImageDtos.AdminUrl(r.File.Id, ImageVariants.Preview),
            r.File.OriginalDeletedAt)).ToList();
        return Results.Ok(new StorageImagePageDto(items, page, pageSize, total, totalBytes));
    }

    private static async Task<IResult> ListProjects(AppDbContext db, CancellationToken ct)
    {
        var usage = await db.Files.AsNoTracking()
            .Where(f => f.DeletedAt == null && f.OwnerType != null && f.ProjectId != null)
            .GroupBy(f => f.ProjectId!.Value)
            .Select(g => new
            {
                ProjectId = g.Key,
                ImageCount = g.Count(),
                TotalBytes = g.Sum(f => f.SizeBytes),
                OriginalBytes = g.Sum(f => f.OriginalDeletedAt == null && !f.PreviewIsOriginal ? f.OriginalBytes ?? 0 : 0),
                OriginalsDeleted = g.Count(f => f.OriginalDeletedAt != null),
            })
            .ToListAsync(ct);
        var ids = usage.Select(u => u.ProjectId).ToList();
        var projects = await (from p in db.Projects.AsNoTracking()
                              join c in db.Clients on p.ClientId equals c.Id
                              where ids.Contains(p.Id)
                              select new { p.Id, p.Name, p.Status, ClientName = c.Name })
            .ToDictionaryAsync(p => p.Id, ct);
        var completed = await OriginalCleanup.CompletedAtAsync(db, ids, ct);

        var items = usage.Where(u => projects.ContainsKey(u.ProjectId))
            .Select(u =>
            {
                var p = projects[u.ProjectId];
                DateTimeOffset? completedAt = completed.TryGetValue(u.ProjectId, out var at) ? at : null;
                return new StorageProjectDto(u.ProjectId, p.Name, p.ClientName, p.Status, completedAt,
                    u.ImageCount, u.TotalBytes, u.OriginalBytes, u.OriginalsDeleted);
            })
            .OrderByDescending(x => x.TotalBytes).ThenBy(x => x.ProjectName, StringComparer.Ordinal)
            .ToList();
        return Results.Ok(items);
    }

    private static async Task<IResult> PreviewOriginalCleanup(
        AppDbContext db, IClock clock, int olderThanMonths = 6, CancellationToken ct = default)
    {
        if (olderThanMonths is < OriginalCleanup.MinMonths or > OriginalCleanup.MaxMonths)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["olderThanMonths"] = ["Từ 1 đến 120 tháng"] });
        var cutoff = clock.UtcNow.AddMonths(-olderThanMonths);
        var rows = await OriginalCleanup.CandidatesAsync(db, cutoff, ct);
        return Results.Ok(OriginalCleanup.Summarize(olderThanMonths, cutoff, rows));
    }

    /// <summary>
    /// P10 (BR-89): manual only. Re-reads the candidates and refuses (409) when they no longer match the preview the designer
    /// confirmed; one transaction for the rows + one image.originals_deleted per project; objects go after the commit
    /// (a failed delete is retried by the sweep as a stray variant); thresholds re-evaluated.
    /// </summary>
    private static async Task<IResult> RunOriginalCleanup(
        OriginalCleanupBody body, AppDbContext db, IFileStorage storage, ActivityWriter activity, IClock clock,
        StorageAlertService storageAlerts, ILoggerFactory loggers, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (body.OlderThanMonths is not (>= OriginalCleanup.MinMonths and <= OriginalCleanup.MaxMonths))
            errors["olderThanMonths"] = ["Từ 1 đến 120 tháng"];
        if (body.ExpectedImageCount is not > 0) errors["expectedImageCount"] = ["Xem trước số ảnh trước khi xoá"];
        if (body.ExpectedFreedBytes is not > 0) errors["expectedFreedBytes"] = ["Xem trước dung lượng trước khi xoá"];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var months = body.OlderThanMonths!.Value;
        var now = clock.UtcNow;
        var candidates = await OriginalCleanup.CandidatesAsync(db, now.AddMonths(-months), ct);
        if (candidates.Count != body.ExpectedImageCount || candidates.Sum(c => c.OriginalBytes) != body.ExpectedFreedBytes)
            return Results.Json(new { code = "CONCURRENCY_CONFLICT", detail = "Số ảnh đã thay đổi — xem lại trước khi xoá" },
                statusCode: StatusCodes.Status409Conflict);

        var ids = candidates.Select(c => c.FileId).ToList();
        var files = await db.Files.Where(f => ids.Contains(f.Id)).ToListAsync(ct);
        long freed = 0;
        foreach (var file in files) freed += file.DiscardOriginal(now);
        var byProject = candidates.GroupBy(c => (c.ProjectId, c.ClientId)).ToList();
        foreach (var g in byProject)
            activity.Write("project", g.Key.ProjectId, "image.originals_deleted", clientId: g.Key.ClientId, projectId: g.Key.ProjectId,
                data: new
                {
                    olderThanMonths = months, imageCount = g.Count(), freedBytes = g.Sum(c => c.OriginalBytes),
                    fileIds = g.Select(c => c.FileId).ToArray(),
                });
        await db.SaveChangesAsync(ct);

        try
        {
            var failed = await storage.DeleteManyAsync(files.Select(f => f.VariantKey(ImageVariants.Original)).ToList(), CancellationToken.None);
            if (failed.Count > 0)
                loggers.CreateLogger("FreelanceFlow.Storage")
                    .LogWarning("Could not delete {Count} original(s); the daily sweep retries", failed.Count);
        }
        catch (Exception ex)
        {
            loggers.CreateLogger("FreelanceFlow.Storage")
                .LogWarning("Could not delete originals ({ExceptionType}); the daily sweep retries", ex.GetType().Name);
        }
        await storageAlerts.EvaluateSafelyAsync(CancellationToken.None);
        return Results.Ok(new OriginalCleanupResultDto(byProject.Count, files.Count, freed));
    }
}
