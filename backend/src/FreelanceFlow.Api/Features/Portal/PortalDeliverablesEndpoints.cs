using FreelanceFlow.Api.Common;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
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
                           select new { ProjectId = p.Id, ProjectName = p.Name })
            .FirstOrDefaultAsync();
        if (owner is null) return Results.NotFound();

        var deliverable = await db.Deliverables.FirstAsync(d => d.Id == id);
        bool recorded;
        try
        {
            recorded = deliverable.RecordClientApproval(clock);
        }
        catch (DomainException ex)
        {
            return Results.Json(new { code = ex.Code, detail = ex.Detail }, statusCode: StatusCodes.Status409Conflict);
        }

        if (recorded)
        {
            activity.Write("deliverable", deliverable.Id, "deliverable.client_approved",
                clientId: principal.ClientId, projectId: owner.ProjectId, actorType: "CLIENT",
                data: new { name = deliverable.Name },
                visibleToClient: true,
                clientMessage: $"Khách đã duyệt {deliverable.Name}");
            await db.SaveChangesAsync();
            await NotifyDesignerAsync(db, notifier, appSettings.Value, logger,
                principal.ClientId, owner.ProjectId, owner.ProjectName, deliverable.Name);
        }

        return Results.Ok(new PortalDeliverableDecisionDto(
            deliverable.Id, deliverable.Status, deliverable.ClientDecision, deliverable.ClientDecisionAt));
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
