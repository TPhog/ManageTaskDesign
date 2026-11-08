using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Domain.Quotations;
using FreelanceFlow.Api.Domain.Requests;
using FreelanceFlow.Api.Domain.Tasks;
using FreelanceFlow.Api.Features.Tasks;
using FreelanceFlow.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Quotations;

/// <summary>What accept did with the quotation's NEW_DELIVERABLE items: the deliverables created, or why none were.</summary>
internal sealed record AcceptedNewDeliverablesResult(IReadOnlyList<Deliverable> Created, string? SkippedReason, IReadOnlyList<Guid> SkippedItemIds);

internal static class AcceptedNewDeliverables
{
    public const string SkippedProjectCancelled = "PROJECT_CANCELLED";

    /// <summary>
    /// BR-48, inside the accept command (the caller's single SaveChangesAsync): every live NEW_DELIVERABLE item linked to a
    /// line of <paramref name="q"/> and not converted yet → one deliverable named after the item, with the item's service
    /// (quota copied); the item becomes DONE. By project status (controller decision, plan 09):
    /// IN_PROGRESS / ON_HOLD → the deliverable starts (IN_PROGRESS) with one TODO task per template step — the accept click
    /// does what "Bắt đầu dự án" did for the others (BR-01: a designer command); DRAFT / QUOTED → NOT_STARTED, start-preview
    /// proposes the template; COMPLETED → NOT_STARTED, no tasks (the designer reopens the project); CANCELLED → nothing is
    /// created, the caller logs the skip.
    /// </summary>
    public static async Task<AcceptedNewDeliverablesResult> CreateAsync(
        AppDbContext db, ActivityWriter activity, IClock clock, Quotation q, Project project)
    {
        var lines = await db.QuotationItems.AsNoTracking().Where(i => i.QuotationId == q.Id).ToDictionaryAsync(i => i.Id);
        var lineIds = lines.Keys.ToList();
        var items = await db.RevisionItems   // tracked: created_deliverable_id is written with the item's xmin check
            .Where(r => r.QuotationItemId != null && lineIds.Contains(r.QuotationItemId.Value)
                        && r.Type == RevisionItemTypes.NewDeliverable && r.CreatedDeliverableId == null
                        && r.WorkStatus != RevisionWorkStatuses.Cancelled)
            .ToListAsync();
        if (items.Count == 0) return new([], null, []);
        if (project.Status == "CANCELLED")
            return new([], SkippedProjectCancelled, items.Select(i => i.Id).ToList());

        var serviceIds = items.Where(i => i.ServiceId != null).Select(i => i.ServiceId!.Value).Distinct().ToList();
        var services = await db.Services.AsNoTracking().Where(s => serviceIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id);
        var sortOrder = (await db.Deliverables.Where(d => d.ProjectId == project.Id).MaxAsync(d => (int?)d.SortOrder) ?? 0) + 10;
        var start = project.Status is "IN_PROGRESS" or "ON_HOLD";
        var taskSort = start ? await TasksEndpoints.NextSortOrderAsync(db, project.Id, "TODO") : 0;

        var created = new List<Deliverable>();
        foreach (var item in items.OrderBy(i => lines[i.QuotationItemId!.Value].SortOrder).ThenBy(i => i.SortOrder))
        {
            var line = lines[item.QuotationItemId!.Value];
            var service = item.ServiceId is { } sid ? services.GetValueOrDefault(sid) : null;
            var deliverable = Deliverable.CreateFromAcceptedLine(project.Id, item.Description, line.Quantity, service, sortOrder);
            if (start) deliverable.Start();
            sortOrder += 10;
            db.Deliverables.Add(deliverable);
            var itemFrom = item.MarkDeliverableCreated(deliverable.Id);
            created.Add(deliverable);
            activity.Write("deliverable", deliverable.Id, "deliverable.created",
                clientId: project.ClientId, projectId: project.Id,
                data: new
                {
                    name = deliverable.Name, serviceId = deliverable.ServiceId, status = deliverable.Status,
                    via = "quotation.accepted", quotationId = q.Id, revisionItemId = item.Id,
                });
            activity.Write("revision_item", item.Id, "revision_item.work_status_changed",
                clientId: project.ClientId, projectId: project.Id,
                data: new { from = itemFrom, to = item.WorkStatus, createdDeliverableId = deliverable.Id, via = "quotation.accepted" });

            if (!start || service is null) continue;
            foreach (var title in service.TaskTemplate())
            {
                var task = ProjectTask.Create(project.Id, title, "", "TODO", "MEDIUM", null, null, deliverable.Id, taskSort, clock);
                taskSort += ProjectTask.SortStep;
                db.Tasks.Add(task);
                activity.Write("task", task.Id, "task.created",
                    clientId: project.ClientId, projectId: project.Id,
                    data: new { title = task.Title, status = task.Status, deliverableId = deliverable.Id, via = "quotation.accepted" });
            }
        }
        return new(created, null, []);
    }
}
