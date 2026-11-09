using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Quotations;
using FreelanceFlow.Api.Domain.Revisions;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Dashboard;

/// <summary>
/// P11 BR-95: what the client did lately (requests, portal approvals, P12 quotation statements, P13 new-client intake)
/// plus accepted quotations; texts and hrefs are built here for the designer.
/// </summary>
internal static class ClientActivityEndpoints
{
    private const string RequestCreated = "request.created";
    private const string DeliverableApproved = "deliverable.client_approved";
    private const string QuotationAccepted = "quotation.accepted";
    private const string QuotationClientAccepted = "quotation.client_accepted";
    private const string ClientPaymentNotice = "quotation.client_payment_notice";
    private const string IntakeSubmitted = "intake.submitted";
    private static readonly string[] ClientActions =
        [RequestCreated, DeliverableApproved, QuotationClientAccepted, ClientPaymentNotice, IntakeSubmitted];

    public static void MapClientActivityEndpoints(this IEndpointRouteBuilder admin) => admin.MapGet("dashboard/client-activity", Get);

    private static async Task<IResult> Get(AppDbContext db, CancellationToken ct, int limit = 10)
    {
        var take = Math.Clamp(limit, 1, 30);

        var feed = await db.ActivityLogs.AsNoTracking()
            .Where(a => (a.ActorType == "CLIENT" && ClientActions.Contains(a.Action)) || a.Action == QuotationAccepted)
            .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
            .Take(take)
            .Select(a => new { a.Id, a.OccurredAt, a.Action, a.EntityId, a.ClientId, a.ProjectId, a.ClientMessage, a.Data })
            .ToListAsync(ct);

        var projectIds = feed.Where(f => f.ProjectId != null).Select(f => f.ProjectId!.Value).Distinct().ToList();
        var projects = await db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.ClientId }).ToDictionaryAsync(p => p.Id, ct);
        var clientIds = feed.Select(f => f.ClientId).Concat(projects.Values.Select(p => (Guid?)p.ClientId))
            .Where(id => id != null).Select(id => id!.Value).Distinct().ToList();
        var clients = await db.Clients.AsNoTracking().Where(c => clientIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name }).ToDictionaryAsync(c => c.Id, c => c.Name, ct);

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
                // the stored client_message of the two statements speaks to the client ("Bạn ..."): reword from data
                QuotationClientAccepted => ("QUOTATION_CLIENT_ACCEPTED",
                    $"Khách đồng ý báo giá {QuotationRef(f.Data)} — chờ bạn xác nhận", $"/admin/quotations/{f.EntityId}"),
                ClientPaymentNotice => ("CLIENT_PAYMENT_NOTICE", PaymentNoticeText(f.Data), $"/admin/quotations/{f.EntityId}"),
                IntakeSubmitted => ("INTAKE_SUBMITTED", "Khách mới gửi yêu cầu qua form", "/admin/intake"),
                // production always writes "Báo giá {Number} (v{Version}) đã được chấp nhận" as client_message
                _ => ("QUOTATION_ACCEPTED", f.ClientMessage ?? "Khách đã chấp nhận báo giá", $"/admin/quotations/{f.EntityId}"),
            };
            return new ClientActivityItemDto(f.Id, f.OccurredAt, kind, text, clientName, projectName, href);
        }).ToList();

        return Results.Ok(new ClientActivityDto(items));
    }

    /// <summary>data of quotation.client_*: { number, version, purpose?, amount? } (PortalQuotationsEndpoints).</summary>
    private static string QuotationRef(string data)
    {
        using var doc = JsonDocument.Parse(data);
        var root = doc.RootElement;
        var number = root.TryGetProperty("number", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
        var version = root.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : (int?)null;
        return number is null ? "" : version is null ? number : $"{number} (v{version})";
    }

    private static string PaymentNoticeText(string data)
    {
        using var doc = JsonDocument.Parse(data);
        var root = doc.RootElement;
        var purpose = root.TryGetProperty("purpose", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
        var amount = root.TryGetProperty("amount", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetInt64() : (long?)null;
        var what = amount is null || purpose is null ? "" : $" {RevisionQuota.Vnd(amount.Value)} ({ClientStatements.PurposeText(purpose)})";
        return $"Khách báo đã chuyển{what} cho báo giá {QuotationRef(data)} — kiểm tra & xác nhận";
    }
}
