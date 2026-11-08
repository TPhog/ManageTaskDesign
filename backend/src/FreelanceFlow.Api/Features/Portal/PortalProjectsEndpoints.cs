using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Revisions;
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
                    db.Deliverables.Count(d => d.ProjectId == p.Id && d.Status == "APPROVED")),
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
            .Select(d => new { d.Id, d.Name, d.Quantity, d.Status, d.IncludedRounds, d.DueDate })
            .ToListAsync();

        // used_rounds (docs/04 §4.1): the same single query as the admin side
        var usedRoundsMap = await RevisionQuota.UsedRounds(db.RevisionRounds.Where(r => r.ProjectId == id), db.RevisionItems)
            .ToDictionaryAsync(x => x.DeliverableId, x => x.UsedRounds);

        var deliverableDtos = deliverables.Select(d =>
        {
            var used = usedRoundsMap.GetValueOrDefault(d.Id);
            return new PortalDeliverableDto(d.Id, d.Name, d.Quantity, d.Status, d.IncludedRounds,
                used, Math.Max(0, d.IncludedRounds - used), d.DueDate);
        }).ToList();

        var summary = new PortalDeliverableSummaryDto(
            deliverables.Count(d => d.Status != "CANCELLED"),
            deliverables.Count(d => d.Status == "DELIVERED"),
            deliverables.Count(d => d.Status == "APPROVED"));

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
        var items = await query
            .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new PortalTimelineItemDto(a.OccurredAt, a.ClientMessage!))
            .ToListAsync();

        return Results.Ok(new PortalPagedDto<PortalTimelineItemDto>(items, page, pageSize, total));
    }
}
