using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Files;
using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Domain.Requests;
using FreelanceFlow.Api.Domain.Revisions;
using FreelanceFlow.Api.Domain.Tasks;
using FreelanceFlow.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Portal;

public static class PortalProjectsEndpoints
{
    public static void MapPortalProjectsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/portal/projects").RequireAuthorization(PortalPrincipal.Policy);

        group.MapGet("", ListProjects);
        group.MapGet("{id:guid}", GetProject);
        group.MapGet("{id:guid}/timeline", GetTimeline);
        group.MapGet("{id:guid}/revisions", GetRevisions);
    }

    private static async Task<IResult> ListProjects(
        [FromServices] PortalPrincipal principal,
        AppDbContext db,
        IClock clock)
    {
        var today = clock.TodayVn;
        var items = await PortalScope.OwnProjects(db, principal)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PortalProjectListItemDto(
                p.Id,
                p.Name,
                p.DesignType,
                p.Status,
                p.DueDate,
                new PortalDeliverableSummaryDto(
                    db.Deliverables.Count(d => d.ProjectId == p.Id && d.Status != "CANCELLED"),
                    db.Deliverables.Count(d => d.ProjectId == p.Id && d.Status == "DELIVERED"),
                    db.Deliverables.Count(d => d.ProjectId == p.Id && d.Status == "APPROVED"),
                    db.Deliverables.Count(d => d.ProjectId == p.Id && d.Status == "IN_REVIEW"),
                    // Plan 12 BR-48: same rule as ClientReviewStates.AwaitingClient, in SQL
                    db.Deliverables.Count(d => d.ProjectId == p.Id && d.Status == "IN_REVIEW" && d.ClientDecision == null
                        && !db.ClientRequests.Any(r => r.DeliverableId == d.Id && r.Status == RequestStatuses.New)
                        && !(from i in db.RevisionItems
                             join r in db.RevisionRounds on i.RoundId equals r.Id
                             where r.DeliverableId == d.Id
                                   && (i.WorkStatus == RevisionWorkStatuses.Todo || i.WorkStatus == RevisionWorkStatuses.InProgress)
                             select i.Id).Any())),
                db.Quotations.Any(q => q.ProjectId == p.Id && q.Status == "SENT" &&
                                       (q.ExpiryDate == null || q.ExpiryDate >= today)),
                // M7b: target of the "Có báo giá mới" chip — the newest open quotation
                db.Quotations
                    .Where(q => q.ProjectId == p.Id && q.Status == "SENT" &&
                                (q.ExpiryDate == null || q.ExpiryDate >= today))
                    .OrderByDescending(q => q.SentAt)
                    .Select(q => (Guid?)q.Id)
                    .FirstOrDefault()))
            .ToListAsync();

        return Results.Ok(items);
    }

    private static async Task<IResult> GetProject(
        Guid id,
        [FromServices] PortalPrincipal principal,
        AppDbContext db)
    {
        var project = await PortalScope.OwnProjects(db, principal)
            .Where(p => p.Id == id)
            .Select(p => new
            {
                p.Id, p.Name, p.DesignType, p.Status, p.StatusReason, p.StatusReasonPublic,
                p.StartDate, p.DueDate,
            })
            .FirstOrDefaultAsync();
        if (project is null) return Results.NotFound();

        // Below this point `id` is proven to belong to the client.
        var deliverables = await db.Deliverables.AsNoTracking()
            .Where(d => d.ProjectId == id)
            .OrderBy(d => d.SortOrder).ThenBy(d => d.CreatedAt)
            .Select(d => new { d.Id, d.Name, d.Quantity, d.Status, d.IncludedRounds, d.DueDate, d.ClientDecision, d.LinksJson })
            .ToListAsync();

        // used_rounds (docs/04 §4.1): the same single query as the admin side
        var usedRoundsMap = await RevisionQuota.UsedRounds(db.RevisionRounds.Where(r => r.ProjectId == id), db.RevisionItems)
            .ToDictionaryAsync(x => x.DeliverableId, x => x.UsedRounds);

        // Plan 06b: the same task-progress query as the admin side; only the percentage is exposed (TSK-05)
        var taskCounts = await TaskProgress.PerDeliverable(db.Tasks.Where(t => t.ProjectId == id))
            .ToDictionaryAsync(x => x.DeliverableId);

        // Plan 12 BR-48: deliverables with a NEW request or an open revision item of their rounds
        var openChange = (await PortalReview.OpenChangeDeliverableIds(db, id).ToListAsync()).ToHashSet();

        var deliverableDtos = deliverables.Select(d =>
        {
            var used = usedRoundsMap.GetValueOrDefault(d.Id);
            var tasks = taskCounts.GetValueOrDefault(d.Id);
            var hasOpenChange = openChange.Contains(d.Id);
            var state = ClientReviewStates.Of(d.Status, d.ClientDecision, hasOpenChange);
            return new PortalDeliverableDto(d.Id, d.Name, d.Quantity, d.Status, d.IncludedRounds,
                used, Math.Max(0, d.IncludedRounds - used), d.DueDate,
                TaskProgress.Percent(tasks?.Total ?? 0, tasks?.Done ?? 0), d.ClientDecision, state, hasOpenChange,
                ExternalLinks.Parse(d.LinksJson).Where(l => l.VisibleToClient).Select(l => new PortalLinkDto(l.Title, l.Url)).ToList());
        }).ToList();

        var summary = new PortalDeliverableSummaryDto(
            deliverables.Count(d => d.Status != "CANCELLED"),
            deliverables.Count(d => d.Status == "DELIVERED"),
            deliverables.Count(d => d.Status == "APPROVED"),
            deliverables.Count(d => d.Status == "IN_REVIEW"),
            deliverableDtos.Count(x => x.ReviewState == ClientReviewStates.AwaitingClient));

        // BRF-04: only the newest CONFIRMED brief is visible to the client
        var brief = await db.ProjectBriefs.AsNoTracking()
            .Where(b => b.ProjectId == id && b.Status == "CONFIRMED")
            .OrderByDescending(b => b.Version)
            .Select(b => new PortalBriefDto(b.Version, b.Content, b.ConfirmedAt))
            .FirstOrDefaultAsync();

        return Results.Ok(new PortalProjectDetailDto(
            project.Id,
            project.Name,
            project.DesignType,
            project.Status,
            project.StatusReasonPublic ? project.StatusReason : null,
            project.StartDate,
            project.DueDate,
            summary,
            deliverableDtos,
            brief));
    }

    /// <summary>
    /// POR-02 / REV-07: rounds and items per deliverable with the quota (docs/02 §5.1), then project-level rounds.
    /// Client-safe fields only — description, type, billing, work status (docs/05 §4).
    /// </summary>
    private static async Task<IResult> GetRevisions(
        Guid id,
        [FromServices] PortalPrincipal principal,
        AppDbContext db)
    {
        if (!await PortalScope.OwnProjects(db, principal).AnyAsync(p => p.Id == id)) return Results.NotFound();

        // Below this point `id` is proven to belong to the client.
        var rounds = await db.RevisionRounds.AsNoTracking()
            .Where(r => r.ProjectId == id)
            .OrderBy(r => r.RoundNo).ThenBy(r => r.CreatedAt)
            .Select(r => new { r.Id, r.DeliverableId, r.RoundNo, r.ReceivedAt })
            .ToListAsync();
        var roundIds = rounds.Select(r => r.Id).ToList();
        var items = (await db.RevisionItems.AsNoTracking()
                .Where(i => roundIds.Contains(i.RoundId))
                .OrderBy(i => i.SortOrder).ThenBy(i => i.CreatedAt)
                .Select(i => new { i.Id, i.RoundId, i.Description, i.Type, i.Billing, i.WorkStatus })
                .ToListAsync())
            .ToLookup(i => i.RoundId, i => new PortalRevisionItemDto(i.Id, i.Description, i.Type, i.Billing, i.WorkStatus));

        // used_rounds (docs/04 §4.1): the same single query as the admin side and GetProject
        var usedRoundsMap = await RevisionQuota.UsedRounds(db.RevisionRounds.Where(r => r.ProjectId == id), db.RevisionItems)
            .ToDictionaryAsync(x => x.DeliverableId, x => x.UsedRounds);

        var deliverables = await db.Deliverables.AsNoTracking()
            .Where(d => d.ProjectId == id)
            .OrderBy(d => d.SortOrder).ThenBy(d => d.CreatedAt)
            .Select(d => new { d.Id, d.Name, d.Status, d.IncludedRounds })
            .ToListAsync();

        var roundDtos = rounds
            .Select(r => (r.DeliverableId, Dto: new PortalRoundDto(r.Id, r.RoundNo, r.ReceivedAt, items[r.Id].ToList())))
            .ToList();
        var byDeliverable = roundDtos.Where(x => x.DeliverableId != null).ToLookup(x => x.DeliverableId!.Value, x => x.Dto);

        var deliverableDtos = deliverables
            .Where(d => d.Status != "CANCELLED" || byDeliverable[d.Id].Any())
            .Select(d =>
            {
                var used = usedRoundsMap.GetValueOrDefault(d.Id);
                return new PortalDeliverableRevisionsDto(d.Id, d.Name, d.IncludedRounds, used,
                    RevisionQuota.RemainingRounds(d.IncludedRounds, used), byDeliverable[d.Id].ToList());
            })
            .ToList();
        var projectRounds = roundDtos.Where(x => x.DeliverableId == null).Select(x => x.Dto).ToList();

        return Results.Ok(new PortalRevisionsDto(deliverableDtos, projectRounds));
    }

    private static async Task<IResult> GetTimeline(
        Guid id,
        [FromServices] PortalPrincipal principal,
        AppDbContext db,
        int page = 1,
        int pageSize = 20)
    {
        if (!await PortalScope.OwnProjects(db, principal).AnyAsync(p => p.Id == id)) return Results.NotFound();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // BR-95: only actions written with visible_to_client = true and a client_message
        var query = db.ActivityLogs.AsNoTracking()
            .Where(a => a.ProjectId == id && a.VisibleToClient && a.ClientMessage != null);

        var total = await query.CountAsync();
        var rows = await query
            .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.OccurredAt,
                Message = a.ClientMessage!,
                Kind = a.Action == "project.progress_update" ? "MESSAGE" : "EVENT",
                a.EntityType,
                a.EntityId,
            })
            .ToListAsync();

        // Plan 08: images of progress updates (files.owner_id = the update's activity entity id)
        var updateIds = rows.Where(r => r.EntityType == "progress_update").Select(r => r.EntityId).ToList();
        var byUpdate = new Dictionary<Guid, List<PortalImageDto>>();
        if (updateIds.Count > 0)
        {
            var images = await PortalImages.VisibleTo(db, principal)
                .Where(f => f.ProjectId == id && f.OwnerType == ImageOwnerTypes.ProgressUpdate && updateIds.Contains(f.OwnerId!.Value))
                .OrderBy(f => f.CreatedAt).ThenBy(f => f.Id)
                .Select(f => new { OwnerId = f.OwnerId!.Value, f.Id, f.Width, f.Height, f.CreatedAt })
                .ToListAsync();
            byUpdate = images.GroupBy(x => x.OwnerId).ToDictionary(g => g.Key, g => g
                .Select(x => PortalImages.ToDto(x.Id, ImageOwnerTypes.ProgressUpdate, null, null, x.Width, x.Height, x.CreatedAt))
                .ToList());
        }

        var items = rows.Select(r => new PortalTimelineItemDto(r.OccurredAt, r.Message, r.Kind,
                r.EntityType == "progress_update" && byUpdate.TryGetValue(r.EntityId, out var list) ? list : []))
            .ToList();

        return Results.Ok(new PortalPagedDto<PortalTimelineItemDto>(items, page, pageSize, total));
    }
}
