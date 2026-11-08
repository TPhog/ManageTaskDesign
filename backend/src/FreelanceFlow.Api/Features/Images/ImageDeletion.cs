using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Files;
using FreelanceFlow.Api.Infrastructure;
using FreelanceFlow.Api.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Images;

public static class ImageDeletion
{
    /// <summary>
    /// Soft-deletes the rows with one activity each in one transaction, then removes their objects. An object that
    /// fails to delete is not counted (the database is the source of truth); the daily sweep removes it later.
    /// Returns the freed bytes.
    /// </summary>
    public static async Task<long> DeleteAsync(
        AppDbContext db, IFileStorage storage, ActivityWriter activity, IClock clock, ILogger logger,
        IReadOnlyList<ProjectFile> images, bool bulk, CancellationToken ct)
    {
        await MarkDeletedAsync(db, activity, clock, images, bulk, ct);
        await db.SaveChangesAsync(ct);
        await DeleteObjectsAsync(storage, logger, images);
        return images.Sum(i => i.SizeBytes);
    }

    /// <summary>Soft delete + one image.deleted activity per image; the caller commits with its own change (P10: task delete).</summary>
    public static async Task MarkDeletedAsync(
        AppDbContext db, ActivityWriter activity, IClock clock, IReadOnlyList<ProjectFile> images, bool bulk, CancellationToken ct)
    {
        if (images.Count == 0) return;
        var now = clock.UtcNow;
        var projectIds = images.Where(i => i.ProjectId != null).Select(i => i.ProjectId!.Value).Distinct().ToList();
        var clients = await db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.ClientId, ct);

        foreach (var image in images)
        {
            image.SoftDelete(now);
            Guid? clientId = image.ProjectId is { } pid && clients.TryGetValue(pid, out var cid) ? cid : null;
            activity.Write("file", image.Id, "image.deleted", clientId: clientId, projectId: image.ProjectId,
                data: new { ownerType = image.OwnerType, ownerId = image.OwnerId, sizeBytes = image.SizeBytes, bulk });
        }
    }

    /// <summary>After the commit: removes every stored object in one call; leftovers are retried by StorageSweeper.</summary>
    public static async Task DeleteObjectsAsync(IFileStorage storage, ILogger logger, IReadOnlyList<ProjectFile> images)
    {
        if (images.Count == 0) return;
        try
        {
            var failed = await storage.DeleteManyAsync(images.SelectMany(i => i.StoredVariantKeys()).ToList(), CancellationToken.None);
            if (failed.Count > 0)
                logger.LogWarning("Could not delete {Count} stored image object(s); the daily sweep retries", failed.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Could not delete stored image objects ({ExceptionType}); the daily sweep retries", ex.GetType().Name);
        }
    }
}
