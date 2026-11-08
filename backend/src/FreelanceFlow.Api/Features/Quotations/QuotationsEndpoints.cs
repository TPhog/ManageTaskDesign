using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Domain.Quotations;
using FreelanceFlow.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Quotations;

public static class QuotationsEndpoints
{
    private static readonly string[] FinishedDeliverableStatuses = ["APPROVED", "DELIVERED"];

    public static void MapQuotationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization();

        group.MapGet("quotations", ListQuotations);
        group.MapGet("quotations/{id:guid}", GetQuotation);
        group.MapPost("projects/{projectId:guid}/quotations", CreateQuotation);
        group.MapPut("quotations/{id:guid}", UpdateQuotation);
        group.MapDelete("quotations/{id:guid}", DeleteQuotation);
    }

    private static async Task<IResult> ListQuotations(
        AppDbContext db,
        IClock clock,
        Guid? projectId = null,
        Guid? clientId = null,
        string? status = null,
        string? effectiveStatus = null,
        string? paymentStatus = null,
        int page = 1,
        int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var today = clock.TodayVn;

        var query = from q in db.Quotations.AsNoTracking()
                    join p in db.Projects.AsNoTracking() on q.ProjectId equals p.Id
                    join c in db.Clients.AsNoTracking() on p.ClientId equals c.Id
                    select new { Quotation = q, ProjectName = p.Name, ClientId = c.Id, ClientName = c.Name };

        if (projectId is { } pid) query = query.Where(x => x.Quotation.ProjectId == pid);
        if (clientId is { } cid) query = query.Where(x => x.ClientId == cid);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Quotation.Status == status);
        if (!string.IsNullOrWhiteSpace(paymentStatus)) query = query.Where(x => x.Quotation.PaymentStatus == paymentStatus);
        // EXPIRED is not stored (ADR-009): filter it as SENT + expiry_date before today (VN)
        query = effectiveStatus switch
        {
            null or "" => query,
            QuotationStatuses.Expired => query.Where(x =>
                x.Quotation.Status == QuotationStatuses.Sent && x.Quotation.ExpiryDate < today),
            QuotationStatuses.Sent => query.Where(x =>
                x.Quotation.Status == QuotationStatuses.Sent && x.Quotation.ExpiryDate >= today),
            _ => query.Where(x => x.Quotation.Status == effectiveStatus),
        };

        var total = await query.CountAsync();
        var rows = await query
            .OrderByDescending(x => x.Quotation.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        var items = rows.Select(x => QuotationReads.ToListItem(x.Quotation, x.ProjectName, x.ClientName, today)).ToList();

        return Results.Ok(new { items, page, pageSize, total });
    }

    private static async Task<IResult> GetQuotation(Guid id, AppDbContext db, IClock clock)
    {
        var detail = await QuotationReads.LoadDetailAsync(db, id, clock.TodayVn);
        return detail is null ? Results.NotFound() : Results.Ok(detail);
    }

    private static async Task<IResult> CreateQuotation(
        Guid projectId,
        CreateQuotationRequest body,
        AppDbContext db,
        ActivityWriter activity,
        IClock clock)
    {
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return Results.NotFound();

        var errors = QuotationValidation.ValidateCreate(body);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var settings = await db.Settings.AsNoTracking().FirstAsync();
        var subset = body.DeliverableIds is { Length: > 0 };
        var lines = new List<QuotationItemInput>();
        var warnings = new List<string>();

        if (body.FromDeliverables)
        {
            List<Deliverable> deliverables;
            if (subset)
            {
                // D1: an own quotation (own number, own VietQR) for selected finished deliverables
                var ids = body.DeliverableIds!.Distinct().ToArray();
                deliverables = await db.Deliverables.AsNoTracking()
                    .Where(d => d.ProjectId == projectId && ids.Contains(d.Id))
                    .ToListAsync();
                if (deliverables.Count != ids.Length)
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["deliverableIds"] = ["Có hạng mục không thuộc dự án này"],
                    });
                var unfinished = deliverables.Where(d => !FinishedDeliverableStatuses.Contains(d.Status)).Select(d => d.Name).ToList();
                if (unfinished.Count > 0)
                    return QuotationReads.BusinessRule(
                        $"Chỉ lập báo giá riêng cho hạng mục đã duyệt hoặc đã bàn giao: {string.Join(", ", unfinished)}");
            }
            else
            {
                deliverables = await db.Deliverables.AsNoTracking()
                    .Where(d => d.ProjectId == projectId && d.Status != "CANCELLED")
                    .ToListAsync();
            }
            deliverables = deliverables.OrderBy(d => d.SortOrder).ThenBy(d => d.CreatedAt).ToList();

            // QUO-01: line = deliverable, quantity × service.base_price (0 without a service; designer edits it)
            var serviceIds = deliverables.Where(d => d.ServiceId != null).Select(d => d.ServiceId!.Value).Distinct().ToList();
            var prices = await db.Services.AsNoTracking()
                .Where(s => serviceIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.BasePrice);
            lines.AddRange(deliverables.Select(d => new QuotationItemInput(
                null, d.Name, "", d.Quantity,
                d.ServiceId is { } sid && prices.TryGetValue(sid, out var price) ? price : 0,
                QuotationItemSources.Deliverable, d.Id)));

            // BR-01: warn, never block, when a deliverable is already on a draft/live quotation of this project
            var deliverableIds = deliverables.Select(d => d.Id).ToList();
            var alreadyQuoted = await (
                from i in db.QuotationItems.AsNoTracking()
                join q in db.Quotations.AsNoTracking() on i.QuotationId equals q.Id
                where q.ProjectId == projectId
                      && i.SourceType == QuotationItemSources.Deliverable
                      && i.SourceId != null && deliverableIds.Contains(i.SourceId.Value)
                      && (q.Status == QuotationStatuses.Draft || q.Status == QuotationStatuses.Sent || q.Status == QuotationStatuses.Accepted)
                select new { DeliverableId = i.SourceId!.Value, q.Number }).ToListAsync();
            foreach (var group in alreadyQuoted.GroupBy(x => x.DeliverableId))
            {
                var name = deliverables.First(d => d.Id == group.Key).Name;
                var where = string.Join(", ", group.Select(x => x.Number ?? "một báo giá nháp").Distinct());
                warnings.Add($"{name} đã có trong {where}");
            }
        }

        var title = string.IsNullOrWhiteSpace(body.Title)
            ? (subset ? $"Báo giá hạng mục đã hoàn thành — {project.Name}" : $"Báo giá {project.Name}")
            : body.Title.Trim();

        Quotation quotation;
        try
        {
            quotation = Quotation.CreateDraft(projectId, QuotationKinds.Initial, title,
                settings.DefaultValidityDays, settings.DefaultDepositPercent, settings.DefaultTerms, lines);
        }
        catch (DomainException ex)
        {
            return QuotationReads.FromDomain(ex);
        }

        db.Quotations.Add(quotation);
        activity.Write("quotation", quotation.Id, "quotation.created",
            clientId: project.ClientId, projectId: projectId,
            data: new { kind = quotation.Kind, items = quotation.Items.Count, deliverableIds = subset ? body.DeliverableIds : null, total = quotation.Total });
        await db.SaveChangesAsync();

        return Results.Created($"/api/admin/quotations/{quotation.Id}", new CreateQuotationResponse(quotation.Id, [.. warnings]));
    }

    private static async Task<IResult> UpdateQuotation(
        Guid id,
        UpdateQuotationRequest body,
        AppDbContext db,
        ActivityWriter activity,
        IClock clock)
    {
        var errors = QuotationValidation.ValidateUpdate(body);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var q = await db.Quotations.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
        if (q is null) return Results.NotFound();
        db.Entry(q).Property<uint>("xmin").OriginalValue = body.RowVersion;

        try
        {
            var changes = q.EditDraft(new QuotationDraftEdit(
                body.Title!.Trim(), body.ValidityDays, body.DiscountAmount, body.VatPercent, body.DepositPercent,
                body.Terms ?? "", body.NotesToClient ?? "", body.InternalNotes ?? "",
                body.Items!.Select(i => new QuotationItemInput(
                    i.Id, i.Description!.Trim(), i.Details ?? "", i.Quantity, i.UnitPrice,
                    i.SourceType ?? QuotationItemSources.Manual, i.SourceId)).ToList()));
            db.QuotationItems.RemoveRange(changes.Removed);
            db.QuotationItems.AddRange(changes.Added);
        }
        catch (DomainException ex)
        {
            return QuotationReads.FromDomain(ex);
        }

        q.UpdatedAt = clock.UtcNow; // always touch the row so the xmin check runs even when only lines changed
        activity.Write("quotation", q.Id, "quotation.updated",
            clientId: await QuotationReads.ClientIdOfAsync(db, q.ProjectId), projectId: q.ProjectId,
            data: new { subtotal = q.Subtotal, discount = q.DiscountAmount, vatPercent = q.VatPercent, total = q.Total, depositPercent = q.DepositPercent, items = q.Items.Count });

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return QuotationReads.Concurrency();
        }

        return Results.Ok(await QuotationReads.LoadDetailAsync(db, id, clock.TodayVn));
    }

    private static async Task<IResult> DeleteQuotation(Guid id, AppDbContext db, ActivityWriter activity)
    {
        var q = await db.Quotations.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
        if (q is null) return Results.NotFound();

        try
        {
            q.EnsureDeletable();
        }
        catch (DomainException ex)
        {
            return QuotationReads.FromDomain(ex);
        }

        // revision_items.quotation_item_id is ON DELETE SET NULL in the DB, so linked revisions unlink themselves (AC-B18)
        db.QuotationItems.RemoveRange(q.Items);
        db.Quotations.Remove(q);
        activity.Write("quotation", q.Id, "quotation.deleted",
            clientId: await QuotationReads.ClientIdOfAsync(db, q.ProjectId), projectId: q.ProjectId,
            data: new { title = q.Title, version = q.Version, total = q.Total });
        await db.SaveChangesAsync();

        return Results.NoContent();
    }
}
