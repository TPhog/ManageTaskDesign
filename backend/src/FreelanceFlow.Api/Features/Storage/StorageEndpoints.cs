using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Files;
using FreelanceFlow.Api.Features.Images;
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
}
