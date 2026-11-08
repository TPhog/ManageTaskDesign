using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Files;
using FreelanceFlow.Api.Features.Storage;
using FreelanceFlow.Api.Infrastructure;
using FreelanceFlow.Api.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Images;

/// <summary>Designer image endpoints (plan 08): task images, project images, visibility, delete, serve.</summary>
public static class ImagesEndpoints
{
    private const string LogCategory = "FreelanceFlow.Images";

    public static void MapImagesEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization();

        admin.MapPost("tasks/{taskId:guid}/images", UploadTaskImages)
            .WithMetadata(new RequestSizeLimitAttribute(ImageLimits.MaxAdminBodyBytes));
        admin.MapGet("tasks/{taskId:guid}/images", ListTaskImages);
        admin.MapGet("projects/{projectId:guid}/images", ListProjectImages);
        admin.MapPatch("images/{id:guid}", UpdateImage);
        admin.MapDelete("images/{id:guid}", DeleteImage);
        admin.MapPost("images/bulk-delete", BulkDelete);
        admin.MapGet("images/{id:guid}/{variant}", ServeImage);
    }

    private static async Task<IResult> UploadTaskImages(
        Guid taskId, HttpRequest http, AppDbContext db, ImageUploader uploader, ActivityWriter activity,
        StorageAlertService storageAlerts, CancellationToken ct)
    {
        var task = await (from t in db.Tasks.AsNoTracking()
                          join p in db.Projects on t.ProjectId equals p.Id
                          where t.Id == taskId
                          select new { t.Id, t.ProjectId, p.ClientId }).FirstOrDefaultAsync(ct);
        if (task is null) return Results.NotFound();

        var blocked = await ImageForms.EnsureUploadsAllowedAsync(storageAlerts, http, imagesOptional: false, ct);
        if (blocked is not null) return blocked;
        var (form, error) = await ImageForms.ReadAsync(http, ImageLimits.MaxAdminBodyBytes, ct);
        if (error is not null) return error;
        var countErrors = ImageLimits.ValidateCount(form!.Files.Count, ImageLimits.MaxFilesPerAdminRequest, required: true);
        if (countErrors.Count > 0) return Results.ValidationProblem(countErrors);
        var visible = !string.Equals(form["visibleToClient"].ToString(), "false", StringComparison.OrdinalIgnoreCase);

        IReadOnlyList<ProjectFile> images;
        try
        {
            images = await uploader.StageAsync(form.Files,
                new ImageTarget(task.ProjectId, ImageOwnerTypes.Task, task.Id, null, "DESIGNER", visible), ct);
            activity.Write("task", task.Id, "image.uploaded", clientId: task.ClientId, projectId: task.ProjectId,
                data: new { count = images.Count, bytes = images.Sum(i => i.SizeBytes), ownerType = ImageOwnerTypes.Task });
            // P10: owner_id has no FK, so the task row is share-locked for the commit. A DELETE /tasks that got there
            // first leaves no row (404, objects discarded); one that comes later waits and then sees these images.
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var stillThere = await db.Database
                .SqlQuery<Guid>($"SELECT id AS \"Value\" FROM tasks WHERE id = {task.Id} FOR SHARE")
                .ToListAsync(ct);
            if (stillThere.Count == 0)
            {
                await uploader.DiscardAsync();
                return Results.NotFound();
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DomainException ex)
        {
            await uploader.DiscardAsync();
            return ImageResults.FromDomain(ex);
        }
        catch
        {
            await uploader.DiscardAsync();
            throw;
        }

        await storageAlerts.EvaluateSafelyAsync(CancellationToken.None);
        return Results.Json(images.Select(i => ImageDtos.ToAdmin(i) with { DuplicateOf = uploader.DuplicateOf(i.Id) }).ToList(), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> ListTaskImages(Guid taskId, AppDbContext db, CancellationToken ct)
    {
        if (!await db.Tasks.AnyAsync(t => t.Id == taskId, ct)) return Results.NotFound();
        var rows = await db.Files.AsNoTracking()
            .Where(f => f.OwnerType == ImageOwnerTypes.Task && f.OwnerId == taskId && f.DeletedAt == null)
            .OrderBy(f => f.CreatedAt).ThenBy(f => f.Id)
            .ToListAsync(ct);
        return Results.Ok(rows.Select(ImageDtos.ToAdmin).ToList());
    }

    private static async Task<IResult> ListProjectImages(Guid projectId, AppDbContext db, string? ownerType, CancellationToken ct)
    {
        if (ownerType is not null && !ImageOwnerTypes.All.Contains(ownerType))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["ownerType"] = ["Loại chủ sở hữu ảnh không hợp lệ"] });
        if (!await db.Projects.AnyAsync(p => p.Id == projectId, ct)) return Results.NotFound();
        var query = db.Files.AsNoTracking().Where(f => f.ProjectId == projectId && f.OwnerType != null && f.DeletedAt == null);
        if (ownerType is not null) query = query.Where(f => f.OwnerType == ownerType);
        var rows = await query.OrderBy(f => f.CreatedAt).ThenBy(f => f.Id).ToListAsync(ct);
        return Results.Ok(rows.Select(ImageDtos.ToAdmin).ToList());
    }

    private static async Task<IResult> UpdateImage(Guid id, UpdateImageBody body, AppDbContext db, ActivityWriter activity, CancellationToken ct)
    {
        if (body.VisibleToClient is not { } visible)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["visibleToClient"] = ["Bắt buộc"] });
        var image = await db.Files.FirstOrDefaultAsync(f => f.Id == id && f.OwnerType != null && f.DeletedAt == null, ct);
        if (image is null) return Results.NotFound();
        try
        {
            image.SetVisibleToClient(visible);
        }
        catch (DomainException ex)
        {
            return ImageResults.FromDomain(ex);
        }
        var clientId = await db.Projects.Where(p => p.Id == image.ProjectId).Select(p => (Guid?)p.ClientId).FirstOrDefaultAsync(ct);
        activity.Write("file", image.Id, "image.visibility_changed", clientId: clientId, projectId: image.ProjectId,
            data: new { visibleToClient = visible });
        await db.SaveChangesAsync(ct);
        return Results.Ok(ImageDtos.ToAdmin(image));
    }

    private static async Task<IResult> DeleteImage(
        Guid id, AppDbContext db, IFileStorage storage, ActivityWriter activity, IClock clock,
        StorageAlertService storageAlerts, ILoggerFactory loggers, CancellationToken ct)
    {
        var image = await db.Files.FirstOrDefaultAsync(f => f.Id == id && f.OwnerType != null && f.DeletedAt == null, ct);
        if (image is null) return Results.NotFound();
        if (await db.Quotations.AnyAsync(q => q.EvidenceFileId == id, ct))   // BR-87
            return ImageResults.FromDomain(new DomainException("FILE_IN_USE", "Ảnh là chứng từ của báo giá — không xoá được"));

        await ImageDeletion.DeleteAsync(db, storage, activity, clock, loggers.CreateLogger(LogCategory), [image], bulk: false, ct);
        await storageAlerts.EvaluateSafelyAsync(CancellationToken.None);
        return Results.NoContent();
    }

    private static async Task<IResult> BulkDelete(
        BulkDeleteBody body, AppDbContext db, IFileStorage storage, ActivityWriter activity, IClock clock,
        StorageAlertService storageAlerts, ILoggerFactory loggers, CancellationToken ct)
    {
        var ids = body.Ids?.Distinct().ToList() ?? [];
        if (ids.Count is < 1 or > ImageLimits.MaxBulkDelete)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["ids"] = [$"Chọn 1–{ImageLimits.MaxBulkDelete} ảnh"] });

        var evidence = await db.Quotations
            .Where(q => q.EvidenceFileId != null && ids.Contains(q.EvidenceFileId.Value))
            .Select(q => q.EvidenceFileId!.Value).ToListAsync(ct);
        var images = await db.Files
            .Where(f => ids.Contains(f.Id) && f.OwnerType != null && f.DeletedAt == null && !evidence.Contains(f.Id))
            .ToListAsync(ct);
        long freed = 0;
        if (images.Count > 0)
        {
            freed = await ImageDeletion.DeleteAsync(db, storage, activity, clock, loggers.CreateLogger(LogCategory), images, bulk: true, ct);
            await storageAlerts.EvaluateSafelyAsync(CancellationToken.None);
        }
        return Results.Ok(new BulkDeleteResultDto(images.Count, ids.Count - images.Count, freed));
    }

    private static async Task<IResult> ServeImage(
        Guid id, string variant, AppDbContext db, IFileStorage storage, HttpContext http, CancellationToken ct)
    {
        if (!ImageVariants.All.Contains(variant)) return Results.NotFound();
        var image = await db.Files.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id && f.OwnerType != null && f.DeletedAt == null, ct);
        return image is null ? Results.NotFound() : await ImageDelivery.ServeAsync(image, variant, portal: false, storage, http, ct);
    }
}
