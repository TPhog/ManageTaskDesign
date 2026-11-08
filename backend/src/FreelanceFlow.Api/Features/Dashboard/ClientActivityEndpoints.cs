using FreelanceFlow.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Dashboard;

/// <summary>P11 BR-95: what the client did lately (requests, portal approvals) plus accepted quotations; hrefs are built here.</summary>
internal static class ClientActivityEndpoints
{
    private const string RequestCreated = "request.created";
    private const string DeliverableApproved = "deliverable.client_approved";
    private const string QuotationAccepted = "quotation.accepted";

    public static void MapClientActivityEndpoints(this IEndpointRouteBuilder admin) => admin.MapGet("dashboard/client-activity", Get);

    private static async Task<IResult> Get(AppDbContext db, CancellationToken ct, int limit = 10)
    {
        var take = Math.Clamp(limit, 1, 30);

        var feed = await db.ActivityLogs.AsNoTracking()
            .Where(a => (a.ActorType == "CLIENT" && (a.Action == RequestCreated || a.Action == DeliverableApproved))
                        || a.Action == QuotationAccepted)
            .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
            .Take(take)
            .Select(a => new { a.Id, a.OccurredAt, a.Action, a.EntityId, a.ClientId, a.ProjectId, a.ClientMessage })
            .ToListAsync(ct);

        var projectIds = feed.Where(f => f.ProjectId != null).Select(f => f.ProjectId!.Value).Distinct().ToList();
        var projects = await db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.ClientId }).ToDictionaryAsync(p => p.Id, ct);
        var clientIds = feed.Select(f => f.ClientId).Concat(projects.Values.Select(p => (Guid?)p.ClientId))
            .Where(id => id != null).Select(id => id!.Value).Distinct().ToList();
        var clients = await db.Clients.AsNoTracking().Where(c => clientIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name }).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var quotationIds = feed.Where(f => f.Action == QuotationAccepted).Select(f => f.EntityId).Distinct().ToList();
        var numbers = await db.Quotations.AsNoTracking().Where(q => quotationIds.Contains(q.Id))
            .Select(q => new { q.Id, q.Number }).ToDictionaryAsync(q => q.Id, q => q.Number, ct);

        var items = feed.Select(f =>
        {
            string? projectName = null;
            Guid? clientId = f.ClientId;
            if (f.ProjectId is { } pid && projects.TryGetValue(pid, out var p))
            {
                projectName = p.Name;
                clientId ??= p.ClientId;
            }
            var clientName = clientId is { } cid && clients.TryGetValue(cid, out var cn) ? cn : null;
            var (kind, text, href) = f.Action switch
            {
                RequestCreated => ("REQUEST_CREATED", f.ClientMessage ?? "Khách gửi yêu cầu mới", $"/admin/requests/{f.EntityId}"),
                DeliverableApproved => ("DELIVERABLE_APPROVED", f.ClientMessage ?? "Khách đã duyệt sản phẩm",
                    f.ProjectId is { } hp ? $"/admin/projects/{hp}" : "/admin/projects"),
                _ => ("QUOTATION_ACCEPTED",
                    f.ClientMessage ?? (numbers.GetValueOrDefault(f.EntityId) is { } no ? $"Khách đã chấp nhận báo giá {no}" : "Khách đã chấp nhận báo giá"),
                    $"/admin/quotations/{f.EntityId}"),
            };
            return new ClientActivityItemDto(f.Id, f.OccurredAt, kind, text, clientName, projectName, href);
        }).ToList();

        return Results.Ok(new ClientActivityDto(items));
    }
}
