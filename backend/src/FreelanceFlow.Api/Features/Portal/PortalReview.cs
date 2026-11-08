using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Requests;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Portal;

/// <summary>Plan 12 BR-48: deliverables with an open change request — the input of ClientReviewStates.Of.</summary>
internal static class PortalReview
{
    public static IQueryable<Guid> OpenChangeDeliverableIds(AppDbContext db, Guid projectId) =>
        db.ClientRequests.AsNoTracking()
            .Where(r => r.ProjectId == projectId && r.Status == RequestStatuses.New && r.DeliverableId != null)
            .Select(r => r.DeliverableId!.Value)
            .Union(
                from i in db.RevisionItems.AsNoTracking()
                join r in db.RevisionRounds on i.RoundId equals r.Id
                where r.ProjectId == projectId && r.DeliverableId != null
                      && (i.WorkStatus == RevisionWorkStatuses.Todo || i.WorkStatus == RevisionWorkStatuses.InProgress)
                select r.DeliverableId!.Value);
}
