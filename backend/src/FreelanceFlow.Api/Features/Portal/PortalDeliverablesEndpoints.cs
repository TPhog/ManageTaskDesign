using FreelanceFlow.Api.Common;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Infrastructure;
using FreelanceFlow.Api.Infrastructure.Notifications;
using FreelanceFlow.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FreelanceFlow.Api.Features.Portal;

/// <summary>Plan 06b F2: the client approves a deliverable under review. Records the opinion only (BR-01).</summary>
public static class PortalDeliverablesEndpoints
{
    public static void MapPortalDeliverablesEndpoints(this IEndpointRouteBuilder app)
    {
        var portal = app.MapGroup("/api/portal").RequireAuthorization(PortalPrincipal.Policy);

        portal.MapPost("deliverables/{id:guid}/approve", Approve);
    }

    private static async Task<IResult> Approve(
        Guid id,
        [FromServices] PortalPrincipal principal,
        AppDbContext db,
        ActivityWriter activity,
        IClock clock,
        INotifier notifier,
        IOptions<AppSettings> appSettings,
        ILogger<PortalDeliverablesEndpointsLog> logger)
    {
        // 07-security §4.4: reachable only through the client's own, non-archived projects — otherwise 404
        var owner = await (from d in db.Deliverables.AsNoTracking()
                           join p in PortalScope.OwnProjects(db, principal) on d.ProjectId equals p.Id
                           where d.Id == id
                           select new { ProjectId = p.Id, ProjectName = p.Name, d.Name, d.Status, d.ClientDecision })
            .FirstOrDefaultAsync();
        if (owner is null) return Results.NotFound();

        // Plan 12 BR-48: no approval while the client's own change request is still being worked on
        var hasOpenChange = await PortalReview.OpenChangeDeliverableIds(db, owner.ProjectId).AnyAsync(x => x == id);
        if (ClientReviewStates.Of(owner.Status, owner.ClientDecision, hasOpenChange) == ClientReviewStates.ChangesInProgress)
            return Results.Json(
                new { code = "CHANGE_REQUEST_OPEN", detail = "Bạn đang có yêu cầu sửa cho sản phẩm này — chờ nhà thiết kế gửi bản mới rồi duyệt" },
                statusCode: StatusCodes.Status409Conflict);

        // Conditional UPDATE (not load-modify-save): it cannot overwrite a concurrent designer status change, and of two
        // simultaneous approvals only one matches the WHERE — so one activity entry and one alert.
        await using var tx = await db.Database.BeginTransactionAsync();
        var now = clock.UtcNow;
        var rows = await db.Deliverables
            .Where(d => d.Id == id && d.Status == "IN_REVIEW" && d.ClientDecision == null)
            .ExecuteUpdateAsync(u => u
                .SetProperty(d => d.ClientDecision, ClientDecisions.Approved)
                .SetProperty(d => d.ClientDecisionAt, now));

        if (rows == 0)
        {
            var current = await db.Deliverables.AsNoTracking().Where(d => d.Id == id)
                .Select(d => new PortalDeliverableDecisionDto(d.Id, d.Status, d.ClientDecision, d.ClientDecisionAt))
                .FirstOrDefaultAsync();
            if (current is null) return Results.NotFound();
            if (current.Status == "IN_REVIEW" && current.ClientDecision == ClientDecisions.Approved)
                return Results.Ok(current);   // already approved: idempotent, nothing new to log or announce
            return Results.Json(
                new { code = "INVALID_TRANSITION", detail = "Hạng mục không ở trạng thái chờ duyệt" },
                statusCode: StatusCodes.Status409Conflict);
        }

        activity.Write("deliverable", id, "deliverable.client_approved",
            clientId: principal.ClientId, projectId: owner.ProjectId, actorType: "CLIENT",
            data: new { name = owner.Name },
            visibleToClient: true,
            clientMessage: $"Khách đã duyệt {owner.Name}");
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        await NotifyDesignerAsync(db, notifier, appSettings.Value, logger,
            principal.ClientId, owner.ProjectId, owner.ProjectName, owner.Name);

        return Results.Ok(new PortalDeliverableDecisionDto(id, "IN_REVIEW", ClientDecisions.Approved, now));
    }

    /// <summary>Discord alert for the designer; runs after commit and must never fail the request.</summary>
    private static async Task NotifyDesignerAsync(
        AppDbContext db, INotifier notifier, AppSettings app, ILogger logger,
        Guid clientId, Guid projectId, string projectName, string deliverableName)
    {
        try
        {
            var clientName = await db.Clients.AsNoTracking().Where(c => c.Id == clientId).Select(c => c.Name).FirstOrDefaultAsync();
            var fields = new List<NotificationField>
            {
                new("Khách", clientName ?? "-"),
                new("Dự án", projectName),
                new("Hạng mục", deliverableName),
            };
            var url = Uri.TryCreate(app.PublicBaseUrl, UriKind.Absolute, out var baseUri)
                ? $"{baseUri.AbsoluteUri.TrimEnd('/')}/admin/projects/{projectId}?tab=deliverables"
                : null;
            await notifier.EnqueueAsync(new Notification("Khách đã duyệt hạng mục", $"Khách đã duyệt {deliverableName}", url, NotificationSeverity.Info, fields));
        }
        catch (Exception ex)
        {
            logger.LogWarning("Could not enqueue the client-approval notification ({ExceptionType})", ex.GetType().Name);
        }
    }
}

/// <summary>Logger category marker.</summary>
public sealed class PortalDeliverablesEndpointsLog;
