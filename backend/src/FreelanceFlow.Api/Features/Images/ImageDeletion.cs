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
    /// fails to delete is not counted (the database is the source of truth) and shows up in the reconciliation log.
    /// Returns the freed bytes.
    /// </summary>
    public static async Task<long> DeleteAsync(
        AppDbContext db, IFileStorage storage, ActivityWriter activity, IClock clock, ILogger logger,
        IReadOnlyList<ProjectFile> images, bool bulk, CancellationToken ct)
    {
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
        await db.SaveChangesAsync(ct);

        foreach (var key in images.SelectMany(i => i.VariantKeys()))
        {
            try
            {
                await storage.DeleteAsync(key, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning("Could not delete a stored image object ({ExceptionType})", ex.GetType().Name);
            }
        }
        return images.Sum(i => i.SizeBytes);
    }
}
