using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Files;
using FreelanceFlow.Api.Domain.Revisions;
using FreelanceFlow.Api.Features.Images;
using FreelanceFlow.Api.Features.Revisions;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Requests;

internal static class RequestReads
{
    /// <summary>RequestDto for each id, in the given order (unknown ids are skipped).</summary>
    public static async Task<List<RequestDto>> LoadAsync(AppDbContext db, IReadOnlyList<Guid> ids)
    {
        if (ids.Count == 0) return [];
        var rows = await (from r in db.ClientRequests.AsNoTracking()
                          join p in db.Projects.AsNoTracking() on r.ProjectId equals p.Id
                          join c in db.Clients.AsNoTracking() on p.ClientId equals c.Id
                          where ids.Contains(r.Id)
                          select new
                          {
                              Request = r,
                              ProjectName = p.Name,
                              ClientId = c.Id,
                              ClientName = c.Name,
                              RowVersion = EF.Property<uint>(r, "xmin"),
                          })
            .ToListAsync();

        var deliverableIds = rows.Where(x => x.Request.DeliverableId != null).Select(x => x.Request.DeliverableId!.Value).Distinct().ToList();
        var names = await db.Deliverables.AsNoTracking()
            .Where(d => deliverableIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name);
        var attachments = (await db.Files.AsNoTracking()
                .Where(f => f.RequestId != null && ids.Contains(f.RequestId.Value) && f.DeletedAt == null)
                .OrderBy(f => f.CreatedAt)
                .Select(f => new { RequestId = f.RequestId!.Value, f.Id, f.OriginalName, f.ContentType, IsImage = f.OwnerType != null })
                .ToListAsync())
            .ToLookup(f => f.RequestId, f => new RequestAttachmentDto(f.Id, f.OriginalName, f.ContentType,
                f.IsImage ? ImageDtos.AdminUrl(f.Id, ImageVariants.Thumb) : null,
                f.IsImage ? ImageDtos.AdminUrl(f.Id, ImageVariants.Preview) : null,
                f.IsImage ? ImageDtos.AdminUrl(f.Id, ImageVariants.Original) : null));

        var byId = rows.ToDictionary(x => x.Request.Id);
        return ids.Where(byId.ContainsKey).Select(id =>
        {
            var x = byId[id];
            var r = x.Request;
            return new RequestDto(r.Id, r.ProjectId, x.ProjectName, x.ClientId, x.ClientName,
                r.DeliverableId, r.DeliverableId is { } did ? names.GetValueOrDefault(did) : null,
                r.Content, r.Source, r.CreatedBy, r.Status, r.DeclineReason, r.TriagedAt, r.CreatedAt, x.RowVersion,
                attachments[r.Id].ToList());
        }).ToList();
    }

    public static async Task<RequestDetailDto?> LoadDetailAsync(AppDbContext db, Guid id, DateOnly today,
        IReadOnlyDictionary<Guid, BillingSuggestion>? suggestions = null)
    {
        var request = (await LoadAsync(db, [id])).SingleOrDefault();
        if (request is null) return null;
        var rounds = await RevisionReads.LoadRoundsAsync(db, r => r.SourceRequestId == id, today, suggestions);
        return new RequestDetailDto(request, rounds);
    }
}
