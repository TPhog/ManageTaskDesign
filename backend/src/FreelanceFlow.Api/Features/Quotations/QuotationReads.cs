using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Quotations;
using FreelanceFlow.Api.Domain.Requests;
using FreelanceFlow.Api.Infrastructure.VietQr;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Quotations;

internal static class QuotationReads
{
    /// <summary>docs/05 §1.2 status per domain error code; body { code, detail }.</summary>
    public static IResult FromDomain(DomainException ex)
    {
        var status = ex.Code switch
        {
            "VALIDATION_FAILED" => StatusCodes.Status400BadRequest,
            "INVALID_TRANSITION" or "QUOTATION_LOCKED" or "DUPLICATE" or "CONCURRENCY_CONFLICT" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity,
        };
        return Results.Json(new { code = ex.Code, detail = ex.Detail }, statusCode: status);
    }

    public static IResult Concurrency() =>
        Results.Conflict(new { code = "CONCURRENCY_CONFLICT", detail = "Dữ liệu đã thay đổi, vui lòng tải lại" });

    public static IResult Locked() =>
        Results.Conflict(new { code = "QUOTATION_LOCKED", detail = "Báo giá đã gửi bị khóa — tạo version mới để sửa" });

    /// <summary>
    /// The DB lock triggers raise check_violation (23514) with a "quotation_locked"/"quotation_items_locked" message
    /// when a non-DRAFT row is modified. Ordinary CHECK constraints share the SQLSTATE but carry a constraint name.
    /// </summary>
    public static bool IsLockViolation(DbUpdateException e) =>
        e.InnerException is Npgsql.PostgresException
        {
            SqlState: Npgsql.PostgresErrorCodes.CheckViolation, ConstraintName: null,
        } pg
        && (pg.MessageText.StartsWith("quotation_locked") || pg.MessageText.StartsWith("quotation_items_locked"));

    public static IResult BusinessRule(string detail) =>
        Results.Json(new { code = "BUSINESS_RULE", detail }, statusCode: StatusCodes.Status422UnprocessableEntity);

    public static Task<Guid> ClientIdOfAsync(AppDbContext db, Guid projectId) =>
        db.Projects.Where(p => p.Id == projectId).Select(p => p.ClientId).FirstAsync();

    /// <summary>BR-75 options with memo + payload. Bank comes from the snapshot (docs/06 §5), so DRAFT has none.</summary>
    public static List<QrOptionDto> QrOptions(Quotation q, QuotationSnapshot? snapshot, DateOnly today)
    {
        if (snapshot is null || q.Number is null) return [];
        return q.PaymentOptions(today).Select(o =>
        {
            var memo = VietQrMemo.For(q.Number, o.Purpose);
            var payload = VietQrPayloadBuilder.Build(new VietQrRequest(snapshot.Bank.Bin, snapshot.Bank.AccountNo, o.Amount, memo));
            return new QrOptionDto(o.Purpose, o.Label, o.Amount, memo, payload);
        }).ToList();
    }

    public static QuotationListItemDto ToListItem(Quotation q, string projectName, string clientName, DateOnly today) =>
        new(q.Id, q.ProjectId, projectName, clientName, q.Kind, q.Number, q.Version, q.Title, q.Status,
            q.EffectiveStatus(today), q.PaymentStatus, q.Total, q.DepositAmount, q.OutstandingAmount(),
            q.IssueDate, q.ExpiryDate, q.CreatedAt);

    public static async Task<QuotationDetailDto?> LoadDetailAsync(AppDbContext db, Guid id, DateOnly today)
    {
        var row = await db.Quotations.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { Quotation = x, RowVersion = EF.Property<uint>(x, "xmin") })
            .FirstOrDefaultAsync();
        if (row is null) return null;
        var q = row.Quotation;

        var names = await (from p in db.Projects.AsNoTracking()
                           join c in db.Clients.AsNoTracking() on p.ClientId equals c.Id
                           where p.Id == q.ProjectId
                           select new { ProjectName = p.Name, ClientName = c.Name }).FirstAsync();

        // BR-39: only a DRAFT is shown from its table rows; everything else renders from the snapshot
        List<QuotationItemDto> items = q.Status == QuotationStatuses.Draft
            ? await db.QuotationItems.AsNoTracking()
                .Where(i => i.QuotationId == id)
                .OrderBy(i => i.SortOrder)
                .Select(i => new QuotationItemDto(i.Id, i.Description, i.Details, i.Quantity, i.UnitPrice, i.Amount,
                    i.SourceType, i.SourceId, i.SortOrder))
                .ToListAsync()
            : [];

        List<QuotationVersionDto> versions;
        if (q.Number is null)
        {
            versions = [new QuotationVersionDto(q.Id, q.Version, q.Status, q.EffectiveStatus(today))];
        }
        else
        {
            var chain = await db.Quotations.AsNoTracking()
                .Where(x => x.Number == q.Number)
                .OrderBy(x => x.Version)
                .ToListAsync();
            versions = chain.Select(x => new QuotationVersionDto(x.Id, x.Version, x.Status, x.EffectiveStatus(today))).ToList();
        }

        var snapshot = q.Snapshot is null ? null : QuotationSnapshot.FromJson(q.Snapshot);

        var newDeliverables = await (
                from r in db.RevisionItems.AsNoTracking()
                join line in db.QuotationItems.AsNoTracking() on r.QuotationItemId equals (Guid?)line.Id
                join s in db.Services.AsNoTracking() on r.ServiceId equals (Guid?)s.Id into ss
                from s in ss.DefaultIfEmpty()
                join d in db.Deliverables.AsNoTracking() on r.CreatedDeliverableId equals (Guid?)d.Id into ds
                from d in ds.DefaultIfEmpty()
                where line.QuotationId == id && r.Type == RevisionItemTypes.NewDeliverable
                      && r.WorkStatus != RevisionWorkStatuses.Cancelled
                orderby line.SortOrder, r.SortOrder
                select new QuotationNewDeliverableDto(r.Id, line.Id, line.Description, r.ServiceId,
                    s == null ? null : s.Name, r.CreatedDeliverableId, d == null ? null : d.Name))
            .ToListAsync();

        return new QuotationDetailDto(
            q.Id, q.ProjectId, names.ProjectName, names.ClientName,
            q.Kind, q.Number, q.Version, q.PreviousVersionId,
            q.Status, q.EffectiveStatus(today), q.PaymentStatus, row.RowVersion,
            q.Title, q.ValidityDays, q.DiscountAmount, q.VatPercent, q.DepositPercent,
            q.Terms, q.NotesToClient, q.InternalNotes,
            q.Subtotal, q.VatAmount, q.Total, q.DepositAmount, q.OutstandingAmount(),
            q.IssueDate, q.ExpiryDate, q.SentAt,
            q.AcceptedAt, q.AcceptedNote, q.RejectedAt, q.RejectedReason,
            q.DepositPaidAt, q.PaidAt,
            items, snapshot, QrOptions(q, snapshot, today), versions, newDeliverables);
    }
}
