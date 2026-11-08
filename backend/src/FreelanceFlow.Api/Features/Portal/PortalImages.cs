using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Files;
using FreelanceFlow.Api.Features.Images;
using FreelanceFlow.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Portal;

/// <summary>
/// 07-security §4.4 + spec §5: the images of the signed-in client's own projects that the client may see —
/// designer images shared with the client (task still exists, or a progress update) and the client's own request photos.
/// </summary>
internal static class PortalImages
{
    public static IQueryable<ProjectFile> VisibleTo(AppDbContext db, PortalPrincipal principal) =>
        from f in db.Files.AsNoTracking()
        join p in PortalScope.OwnProjects(db, principal) on f.ProjectId equals (Guid?)p.Id
        where f.DeletedAt == null && f.OwnerType != null
              && ((f.VisibleToClient
                   && (f.OwnerType == ImageOwnerTypes.ProgressUpdate
                       || (f.OwnerType == ImageOwnerTypes.Task && db.Tasks.Any(t => t.Id == f.OwnerId))))
                  || (f.OwnerType == ImageOwnerTypes.ClientRequest && f.UploadedBy == "CLIENT"))
        select f;

    public static PortalImageDto ToDto(
        Guid id, string source, Guid? deliverableId, string? deliverableName, int? width, int? height, DateTimeOffset createdAt) =>
        new(id, source, deliverableId, deliverableName, width ?? 0, height ?? 0, createdAt,
            ImageDtos.PortalUrl(id, ImageVariants.Thumb), ImageDtos.PortalUrl(id, ImageVariants.Preview));
}
