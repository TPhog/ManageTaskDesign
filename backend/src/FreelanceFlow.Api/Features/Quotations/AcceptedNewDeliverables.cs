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

internal static class AcceptedNewDeliverables
{
    /// <summary>
    /// BR-48, inside the accept command (the caller's single SaveChangesAsync): every live NEW_DELIVERABLE item linked to a
    /// line of <paramref name="q"/> and not converted yet → one NOT_STARTED deliverable named after the accepted line, with
    /// the item's service (quota copied). A project that has already started also gets one TODO task per template step,
    /// because "Bắt đầu dự án" will not run again; otherwise start-preview proposes the template as for any deliverable.
    /// </summary>
    public static async Task<IReadOnlyList<Deliverable>> CreateAsync(AppDbContext db, ActivityWriter activity, IClock clock, Quotation q)
    {
        var lines = await db.QuotationItems.AsNoTracking().Where(i => i.QuotationId == q.Id).ToDictionaryAsync(i => i.Id);
        var lineIds = lines.Keys.ToList();
        var items = await db.RevisionItems   // tracked: created_deliverable_id is written with the item's xmin check
            .Where(r => r.QuotationItemId != null && lineIds.Contains(r.QuotationItemId.Value)
                        && r.Type == RevisionItemTypes.NewDeliverable && r.CreatedDeliverableId == null
                        && r.WorkStatus != RevisionWorkStatuses.Cancelled)
            .ToListAsync();
        if (items.Count == 0) return [];

        var project = await db.Projects.AsNoTracking().FirstAsync(p => p.Id == q.ProjectId);
        var serviceIds = items.Where(i => i.ServiceId != null).Select(i => i.ServiceId!.Value).Distinct().ToList();
        var services = await db.Services.AsNoTracking().Where(s => serviceIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id);
        var sortOrder = (await db.Deliverables.Where(d => d.ProjectId == project.Id).MaxAsync(d => (int?)d.SortOrder) ?? 0) + 10;
        var started = project.Status is not ("DRAFT" or "QUOTED");
        var taskSort = started ? await TasksEndpoints.NextSortOrderAsync(db, project.Id, "TODO") : 0;

        var created = new List<Deliverable>();
        foreach (var item in items.OrderBy(i => lines[i.QuotationItemId!.Value].SortOrder).ThenBy(i => i.SortOrder))
        {
            var line = lines[item.QuotationItemId!.Value];
            var service = item.ServiceId is { } sid ? services.GetValueOrDefault(sid) : null;
            var deliverable = Deliverable.CreateFromAcceptedLine(project.Id, line.Description, line.Quantity, service, sortOrder);
            sortOrder += 10;
            db.Deliverables.Add(deliverable);
            item.MarkDeliverableCreated(deliverable.Id);
            created.Add(deliverable);
            activity.Write("deliverable", deliverable.Id, "deliverable.created",
                clientId: project.ClientId, projectId: project.Id,
                data: new { name = deliverable.Name, serviceId = deliverable.ServiceId, via = "quotation.accepted", quotationId = q.Id, revisionItemId = item.Id });

            if (!started || service is null) continue;
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
        return created;
    }
}
