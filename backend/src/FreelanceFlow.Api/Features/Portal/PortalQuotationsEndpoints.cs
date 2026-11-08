using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Quotations;
using FreelanceFlow.Api.Features.Quotations;
using FreelanceFlow.Api.Infrastructure.Security;
using FreelanceFlow.Api.Infrastructure.VietQr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Portal;

/// <summary>POR-03: the client's quotations. Read-only — the client never accepts/rejects in the app (ADR-004).</summary>
public static class PortalQuotationsEndpoints
{
    public static void MapPortalQuotationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/portal/quotations").RequireAuthorization(PortalPrincipal.Policy);

        group.MapGet("", ListQuotations);
        group.MapGet("{id:guid}", GetQuotation);
        group.MapGet("{id:guid}/versions/{version:int}", GetVersion);
        group.MapGet("{id:guid}/qr/{purpose}.png", GetQrPng);
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
            s.Total, s.DepositAmount, s.IssueDate, s.ExpiryDate);
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
            q.Id, q.ProjectId, q.Status, q.EffectiveStatus(today), q.PaymentStatus, snapshot, qr, versions);
    }
}
