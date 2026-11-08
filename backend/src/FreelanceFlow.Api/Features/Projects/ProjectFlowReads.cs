using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Flow;
using FreelanceFlow.Api.Domain.Quotations;
using FreelanceFlow.Api.Domain.Requests;
using FreelanceFlow.Api.Domain.Revisions;
using FreelanceFlow.Api.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Projects;

/// <summary>
/// Loads <see cref="FlowFacts"/> for one or many projects with a fixed number of grouped queries (no N+1), so
/// /projects/{id}/flow and /flow/actions share the same reads. Read-only: AsNoTracking projections.
/// </summary>
internal static class ProjectFlowReads
{
    public static async Task<Dictionary<Guid, FlowFacts>> LoadFactsAsync(
        AppDbContext db, IReadOnlyCollection<Guid> projectIds, DateOnly today)
    {
        var ids = projectIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, FlowFacts>();

        var statuses = await db.Projects.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Status);

        var briefConfirmed = (await db.ProjectBriefs.AsNoTracking()
                .Where(b => ids.Contains(b.ProjectId) && b.Status == "CONFIRMED")
                .Select(b => b.ProjectId)
                .Distinct()
                .ToListAsync())
            .ToHashSet();

        var deliverables = await db.Deliverables.AsNoTracking()
            .Where(d => ids.Contains(d.ProjectId))
            .OrderBy(d => d.SortOrder).ThenBy(d => d.CreatedAt)
            .Select(d => new { d.Id, d.ProjectId, d.Name, d.Status, d.ClientDecision, d.IncludedRounds })
            .ToListAsync();

        var taskCounts = await TaskProgress.PerDeliverable(db.Tasks.Where(t => ids.Contains(t.ProjectId)))
            .ToDictionaryAsync(x => x.DeliverableId);

        // used_rounds (docs/04 §4.1): the single query lives in Domain/Revisions
        var usedRounds = await RevisionQuota.UsedRounds(db.RevisionRounds.Where(r => ids.Contains(r.ProjectId)), db.RevisionItems)
            .ToDictionaryAsync(x => x.DeliverableId, x => x.UsedRounds);

        var quotations = await db.Quotations.AsNoTracking()
            .Where(q => ids.Contains(q.ProjectId) && q.Status != QuotationStatuses.Superseded)
            .Select(q => new
            {
                q.ProjectId, q.Id, q.Kind, q.Status, q.Number, q.Version, q.ExpiryDate, q.PaymentStatus,
                q.DepositPercent, q.Total, q.DepositAmount, q.CreatedAt,
                q.ClientAcceptedAt, q.ClientPaymentNoticePurpose,
            })
            .ToListAsync();

        // BR-81: overdue = due date before today (VN) and not DONE
        var overdue = await db.Tasks.AsNoTracking()
            .Where(t => ids.Contains(t.ProjectId) && t.Status != "DONE" && t.DueDate != null && t.DueDate < today)
            .GroupBy(t => t.ProjectId)
            .Select(g => new { ProjectId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProjectId, x => x.Count);

        var newRequests = await db.ClientRequests.AsNoTracking()
            .Where(r => ids.Contains(r.ProjectId) && r.Status == RequestStatuses.New)
            .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
            .Select(r => new { r.ProjectId, r.Id })
            .ToListAsync();

        // BR-46: CHARGEABLE items that are not on a quotation line yet
        var unquoted = await (
                from i in db.RevisionItems.AsNoTracking()
                join r in db.RevisionRounds on i.RoundId equals r.Id
                where ids.Contains(r.ProjectId)
                      && i.Billing == RevisionBillings.Chargeable
                      && i.QuotationItemId == null
                      && i.WorkStatus != RevisionWorkStatuses.Cancelled
                group i by r.ProjectId into g
                select new { ProjectId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProjectId, x => x.Count);

        // D-11: revision items still TODO / IN_PROGRESS, per deliverable
        var openRevisions = await (
                from i in db.RevisionItems.AsNoTracking()
                join r in db.RevisionRounds on i.RoundId equals r.Id
                where ids.Contains(r.ProjectId) && r.DeliverableId != null
                      && (i.WorkStatus == RevisionWorkStatuses.Todo || i.WorkStatus == RevisionWorkStatuses.InProgress)
                group i by r.DeliverableId into g
                select new { DeliverableId = g.Key!.Value, Count = g.Count() })
            .ToDictionaryAsync(x => x.DeliverableId, x => x.Count);

        var deliverablesByProject = deliverables.ToLookup(d => d.ProjectId);
        var quotationsByProject = quotations.ToLookup(q => q.ProjectId);
        var requestsByProject = newRequests.ToLookup(r => r.ProjectId);

        return statuses.ToDictionary(p => p.Key, p => new FlowFacts(
            p.Value,
            briefConfirmed.Contains(p.Key),
            deliverablesByProject[p.Key].Select(d =>
            {
                var tasks = taskCounts.GetValueOrDefault(d.Id);
                return new FlowDeliverable(d.Id, d.Name, d.Status, tasks?.Total ?? 0, tasks?.Done ?? 0,
                    d.ClientDecision, d.IncludedRounds, usedRounds.GetValueOrDefault(d.Id),
                    openRevisions.GetValueOrDefault(d.Id));
            }).ToList(),
            quotationsByProject[p.Key].Select(q => new FlowQuotation(
                q.Id, q.Kind, q.Status, q.Number, q.Version, q.ExpiryDate, q.PaymentStatus,
                q.DepositPercent, q.Total, q.DepositAmount, q.CreatedAt,
                q.ClientAcceptedAt, q.ClientPaymentNoticePurpose)).ToList(),
            overdue.GetValueOrDefault(p.Key),
            requestsByProject[p.Key].Select(r => r.Id).ToList(),
            unquoted.GetValueOrDefault(p.Key)));
    }
}
