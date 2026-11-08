using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Files;
using FreelanceFlow.Api.Features.Images;
using FreelanceFlow.Api.Infrastructure.Security;
using FreelanceFlow.Api.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Portal;

/// <summary>Spec §7: the project's "Hình ảnh" section and the image files themselves (preview/thumb only).</summary>
public static class PortalImagesEndpoints
{
    public static void MapPortalImagesEndpoints(this IEndpointRouteBuilder app)
    {
        var portal = app.MapGroup("/api/portal").RequireAuthorization(PortalPrincipal.Policy);
        portal.MapGet("projects/{id:guid}/images", ListProjectImages);
        portal.MapGet("images/{id:guid}/{variant}", ServeImage);
    }

    /// <summary>Task images (grouped by deliverable on the page) and progress images, newest first. Request photos stay with their request.</summary>
    private static async Task<IResult> ListProjectImages(
        Guid id, [FromServices] PortalPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        if (!await PortalScope.OwnProjects(db, principal).AnyAsync(p => p.Id == id, ct)) return Results.NotFound();

        // Below this point `id` is proven to belong to the client.
        var rows = await (from f in PortalImages.VisibleTo(db, principal)
                          where f.ProjectId == id
                                && (f.OwnerType == ImageOwnerTypes.Task || f.OwnerType == ImageOwnerTypes.ProgressUpdate)
                          join t in db.Tasks on f.OwnerId equals (Guid?)t.Id into ft
                          from t in ft.DefaultIfEmpty()
                          join d in db.Deliverables on t.DeliverableId equals (Guid?)d.Id into td
                          from d in td.DefaultIfEmpty()
                          orderby f.CreatedAt descending, f.Id descending
                          select new
                          {
                              f.Id,
                              f.OwnerType,
                              DeliverableId = d == null ? (Guid?)null : d.Id,
                              DeliverableName = d == null ? null : d.Name,
                              f.Width,
                              f.Height,
                              f.CreatedAt,
                          })
                         .ToListAsync(ct);

        return Results.Ok(rows.Select(r =>
            PortalImages.ToDto(r.Id, r.OwnerType!, r.DeliverableId, r.DeliverableName, r.Width, r.Height, r.CreatedAt)).ToList());
    }

    private static async Task<IResult> ServeImage(
        Guid id, string variant, [FromServices] PortalPrincipal principal, AppDbContext db, IFileStorage storage,
        HttpContext http, CancellationToken ct)
    {
        if (!ImageVariants.ClientVisible.Contains(variant)) return Results.NotFound(); // never the original (spec §4)
        var image = await PortalImages.VisibleTo(db, principal).FirstOrDefaultAsync(f => f.Id == id, ct);
        return image is null ? Results.NotFound() : await ImageDelivery.ServeAsync(image, variant, storage, http, ct);
    }
}
