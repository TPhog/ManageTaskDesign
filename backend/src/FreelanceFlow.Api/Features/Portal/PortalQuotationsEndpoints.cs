using FreelanceFlow.Api.Common;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Quotations;
using FreelanceFlow.Api.Domain.Revisions;
using FreelanceFlow.Api.Features.Quotations;
using FreelanceFlow.Api.Infrastructure;
using FreelanceFlow.Api.Infrastructure.Notifications;
using FreelanceFlow.Api.Infrastructure.Security;
using FreelanceFlow.Api.Infrastructure.VietQr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FreelanceFlow.Api.Features.Portal;

/// <summary>POR-03: the client's quotations. Reads + the client's statements (ADR-012): recorded only, the designer confirms (BR-01).</summary>
public static class PortalQuotationsEndpoints
{
    public static void MapPortalQuotationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/portal/quotations").RequireAuthorization(PortalPrincipal.Policy);

        group.MapGet("", ListQuotations);
        group.MapGet("{id:guid}", GetQuotation);
        group.MapGet("{id:guid}/versions/{version:int}", GetVersion);
        group.MapGet("{id:guid}/qr/{purpose}.png", GetQrPng);
        group.MapPost("{id:guid}/accept", Accept);
        group.MapPost("{id:guid}/payment-notice", PaymentNotice);
    }

    // 07-security §4.4 + QUO-10: quotations of the client's own projects, never DRAFT (POR-06) nor SUPERSEDED.
    // A SUPERSEDED version is reachable only through GetVersion of a chain the client can already see.
    private static IQueryable<Quotation> Visible(AppDbContext db, PortalPrincipal principal) =>
        from q in db.Quotations.AsNoTracking()
        join p in PortalScope.OwnProjects(db, principal) on q.ProjectId equals p.Id
        where q.Status != QuotationStatuses.Draft && q.Status != QuotationStatuses.Superseded && q.Snapshot != null
        select q;

    private static async Task<IResult> ListQuotations(
        [FromServices] PortalPrincipal principal,
        AppDbContext db,
        IClock clock,
        Guid? projectId = null)
    {
        var query = Visible(db, principal);
        if (projectId is { } pid)
        {
            if (!await PortalScope.OwnProjects(db, principal).AnyAsync(p => p.Id == pid)) return Results.NotFound();
            query = query.Where(q => q.ProjectId == pid);
        }

        var rows = await query.OrderByDescending(q => q.SentAt).ThenByDescending(q => q.Id).ToListAsync();
        var today = clock.TodayVn;
        return Results.Ok(rows.Select(q => ToListItem(q, today)).ToList());
    }

    private static async Task<IResult> GetQuotation(
        Guid id,
        [FromServices] PortalPrincipal principal,
        AppDbContext db,
        IClock clock)
    {
        var q = await Visible(db, principal).FirstOrDefaultAsync(x => x.Id == id);
        if (q is null) return Results.NotFound();
        return Results.Ok(ToDetail(q, await ChainAsync(db, q), clock.TodayVn));
    }

    /// <summary>docs/05 §4: another version (e.g. SUPERSEDED) of a chain whose visible member is {id}.</summary>
    private static async Task<IResult> GetVersion(
        Guid id,
        int version,
        [FromServices] PortalPrincipal principal,
        AppDbContext db,
        IClock clock)
    {
        var q = await Visible(db, principal).FirstOrDefaultAsync(x => x.Id == id);
        if (q is null) return Results.NotFound();
        var chain = await ChainAsync(db, q);
        var target = chain.FirstOrDefault(x => x.Version == version);
        return target is null ? Results.NotFound() : Results.Ok(ToDetail(target, chain, clock.TodayVn));
    }

    /// <summary>POR-03 "tải ảnh QR": same BR-75 options as the detail, bank from the snapshot (docs/06 §5).</summary>
    private static async Task<IResult> GetQrPng(
        Guid id,
        string purpose,
        [FromServices] PortalPrincipal principal,
        AppDbContext db,
        IClock clock)
    {
        var q = await Visible(db, principal).FirstOrDefaultAsync(x => x.Id == id);
        if (q is null) return Results.NotFound();
        var snapshot = QuotationSnapshot.FromJson(q.Snapshot!);
        var option = QuotationReads.QrOptions(q, snapshot, clock.TodayVn).FirstOrDefault(o => o.Purpose == purpose);
        if (option is null)
            return QuotationReads.BusinessRule("Không có mã QR này ở trạng thái hiện tại của báo giá (BR-75)");
        var png = VietQrPngBuilder.BuildPng(option.Payload, pixelsPerModule: 10);
        return Results.File(png, "image/png", $"{snapshot.Number.Replace("-", "")}-{purpose}.png");
    }

    /// <summary>Plan 12 BR-76: "Đồng ý báo giá" — client_accepted_at only; status stays SENT until the designer accepts.</summary>
    private static async Task<IResult> Accept(
        Guid id, [FromServices] PortalPrincipal principal, AppDbContext db, ActivityWriter activity, IClock clock,
        INotifier notifier, IOptions<AppSettings> app, ILogger<PortalQuotationsEndpointsLog> logger)
    {
        if (!await Visible(db, principal).AnyAsync(x => x.Id == id)) return Results.NotFound();
        var q = await db.Quotations.FirstAsync(x => x.Id == id);   // tracked: xmin guards a concurrent designer change

        bool recorded;
        try { recorded = q.RecordClientAcceptance(clock.TodayVn, clock.UtcNow); }
        catch (DomainException ex) { return StatementError(ex); }

        if (recorded)
        {
            q.UpdatedAt = clock.UtcNow;
            activity.Write("quotation", q.Id, "quotation.client_accepted",
                clientId: principal.ClientId, projectId: q.ProjectId, actorType: "CLIENT",
                data: new { number = q.Number, version = q.Version },
                visibleToClient: true,
                clientMessage: $"Bạn đã đồng ý báo giá {q.Number} — chờ nhà thiết kế xác nhận");
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return QuotationReads.Concurrency(); }
            await NotifyDesignerAsync(db, notifier, app.Value, logger, principal.ClientId, q,
                "Khách đồng ý báo giá", $"Khách đã bấm \"Đồng ý báo giá\" {q.Number} trên portal", []);
        }
        return await DetailAsync(db, principal, id, clock);
    }

    /// <summary>Plan 12 BR-77: "Tôi đã chuyển khoản" for one BR-75 option — payment_status stays as the designer set it.</summary>
    private static async Task<IResult> PaymentNotice(
        Guid id, PortalPaymentNoticeRequest body, [FromServices] PortalPrincipal principal, AppDbContext db,
        ActivityWriter activity, IClock clock, INotifier notifier, IOptions<AppSettings> app,
        ILogger<PortalQuotationsEndpointsLog> logger)
    {
        if (!await Visible(db, principal).AnyAsync(x => x.Id == id)) return Results.NotFound();
        var errors = new Dictionary<string, string[]>();
        if (body.Purpose is null || !ClientStatements.Purposes.Contains(body.Purpose))
            errors["purpose"] = ["Chọn khoản bạn đã chuyển"];
        if (body.Note?.Trim().Length > ClientStatements.MaxNote)
            errors["note"] = [$"Ghi chú tối đa {ClientStatements.MaxNote} ký tự"];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var q = await db.Quotations.FirstAsync(x => x.Id == id);
        bool recorded;
        try { recorded = q.RecordClientPaymentNotice(body.Purpose!, body.Note, clock.TodayVn, clock.UtcNow); }
        catch (DomainException ex) { return StatementError(ex); }

        if (recorded)
        {
            var notice = q.PaymentNotice()!;
            var what = ClientStatements.PurposeText(notice.Purpose);
            q.UpdatedAt = clock.UtcNow;
            activity.Write("quotation", q.Id, "quotation.client_payment_notice",
                clientId: principal.ClientId, projectId: q.ProjectId, actorType: "CLIENT",
                data: new { number = q.Number, version = q.Version, purpose = notice.Purpose, amount = notice.Amount, hasNote = notice.Note != null },
                visibleToClient: true,
                clientMessage: $"Bạn báo đã chuyển {RevisionQuota.Vnd(notice.Amount)} ({what}) cho báo giá {q.Number} — chờ nhà thiết kế kiểm tra");
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return QuotationReads.Concurrency(); }
            var fields = new List<NotificationField> { new("Khoản", what), new("Số tiền", RevisionQuota.Vnd(notice.Amount)) };
            if (notice.Note is { } note) fields.Add(new("Ghi chú", note.Length > 200 ? note[..200] + "…" : note));
            await NotifyDesignerAsync(db, notifier, app.Value, logger, principal.ClientId, q,
                "Khách báo đã chuyển khoản", $"Khách báo đã chuyển {RevisionQuota.Vnd(notice.Amount)} cho {q.Number} — kiểm tra tài khoản", fields);
        }
        return await DetailAsync(db, principal, id, clock);
    }

    private static IResult StatementError(DomainException ex) =>
        Results.Json(new { code = ex.Code, detail = ex.Detail },
            statusCode: ex.Code == "VALIDATION_FAILED" ? StatusCodes.Status400BadRequest : StatusCodes.Status409Conflict);

    private static async Task<IResult> DetailAsync(AppDbContext db, PortalPrincipal principal, Guid id, IClock clock)
    {
        var fresh = await Visible(db, principal).FirstAsync(x => x.Id == id);
        return Results.Ok(ToDetail(fresh, await ChainAsync(db, fresh), clock.TodayVn));
    }

    /// <summary>Discord alert after commit; never fails the request (pattern: PortalRequestsEndpoints.NotifyDesignerAsync).</summary>
    private static async Task NotifyDesignerAsync(
        AppDbContext db, INotifier notifier, AppSettings app, ILogger logger, Guid clientId, Quotation q,
        string title, string description, List<NotificationField> extra)
    {
        try
        {
            var clientName = await db.Clients.AsNoTracking().Where(c => c.Id == clientId).Select(c => c.Name).FirstOrDefaultAsync();
            var projectName = await db.Projects.AsNoTracking().Where(p => p.Id == q.ProjectId).Select(p => p.Name).FirstOrDefaultAsync();
            var fields = new List<NotificationField>
            {
                new("Khách", clientName ?? "-"), new("Dự án", projectName ?? "-"), new("Báo giá", q.Number ?? "-"),
            };
            fields.AddRange(extra);
            var url = Uri.TryCreate(app.PublicBaseUrl, UriKind.Absolute, out var baseUri)
                ? $"{baseUri.AbsoluteUri.TrimEnd('/')}/admin/quotations/{q.Id}"
                : null;
            await notifier.EnqueueAsync(new Notification(title, description, url, NotificationSeverity.Info, fields));
        }
        catch (Exception ex)
        {
            logger.LogWarning("Could not enqueue the client-statement notification ({ExceptionType})", ex.GetType().Name);
        }
    }

    /// <summary>Every non-DRAFT version sharing the number (BR-33), oldest first; same project as the visible one.</summary>
    private static Task<List<Quotation>> ChainAsync(AppDbContext db, Quotation q) =>
        db.Quotations.AsNoTracking()
            .Where(x => x.Number == q.Number && x.ProjectId == q.ProjectId && x.Status != QuotationStatuses.Draft)
            .OrderBy(x => x.Version)
            .ToListAsync();

    private static PortalQuotationListItemDto ToListItem(Quotation q, DateOnly today)
    {
        var s = QuotationSnapshot.FromJson(q.Snapshot!);
        return new PortalQuotationListItemDto(
            q.Id, q.ProjectId, s.Project.Name, s.Number, s.Version, s.Kind, s.Title,
            q.Status, q.EffectiveStatus(today), q.PaymentStatus,
            s.Total, s.DepositAmount, ClientStatements.Amount(QrPurposes.Remaining, s.Total, s.DepositAmount),
            s.IssueDate, s.ExpiryDate,
            q.ClientAcceptedAt, ToNotice(q.PaymentNotice()));
    }

    private static PortalQuotationDetailDto ToDetail(Quotation q, IReadOnlyList<Quotation> chain, DateOnly today)
    {
        var snapshot = QuotationSnapshot.FromJson(q.Snapshot!);
        var qr = QuotationReads.QrOptions(q, snapshot, today)
            .Select(o => new PortalQrOptionDto(o.Purpose, o.Label, o.Amount, o.Memo, o.Payload))
            .ToList();
        var versions = chain
            .Select(x => new PortalQuotationVersionDto(x.Version, x.Status, x.EffectiveStatus(today)))
            .ToList();
        return new PortalQuotationDetailDto(
            q.Id, q.ProjectId, q.Status, q.EffectiveStatus(today), q.PaymentStatus,
            ClientStatements.Amount(QrPurposes.Remaining, snapshot.Total, snapshot.DepositAmount), snapshot, qr, versions,
            q.ClientAcceptedAt, ToNotice(q.PaymentNotice()));
    }

    private static PortalPaymentNoticeDto? ToNotice(ClientPaymentNotice? n) =>
        n is null ? null : new(n.Purpose, n.Amount, n.At, n.Note, n.Pending);
}

/// <summary>Logger category marker.</summary>
public sealed class PortalQuotationsEndpointsLog;
