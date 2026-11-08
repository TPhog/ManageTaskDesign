using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Catalog;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Domain.Quotations;
using FreelanceFlow.Api.Domain.Requests;
using FreelanceFlow.Api.Domain.Revisions;
using FreelanceFlow.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Quotations;

/// <summary>BR-46 step 2: service and/or typed price for a NEW_DELIVERABLE item (plan 09: both optional; old JSON still binds).</summary>
public sealed record NewDeliverableService(Guid RevisionItemId, Guid? ServiceId, long? UnitPrice = null);

/// <summary>Plan 09: typed price for the line of a non-NEW_DELIVERABLE item (a round line: any item of the round).</summary>
public sealed record AdditionalLinePrice(Guid RevisionItemId, long UnitPrice);

public sealed record CreateAdditionalQuotationRequest(string? Title, Guid[]? RevisionItemIds,
    NewDeliverableService[]? NewDeliverableServices, AdditionalLinePrice[]? LinePrices = null);

public sealed record AdditionalPreviewRequest(Guid[]? RevisionItemIds);

public sealed record AdditionalPreviewLineDto(string Kind, IReadOnlyList<Guid> RevisionItemIds, string Description, string Details, long SuggestedUnitPrice);

public sealed record AdditionalPreviewDto(IReadOnlyList<AdditionalPreviewLineDto> Lines);

public static class AdditionalQuotationEndpoints
{
    private const int MaxItems = 200;
    private const string PriceRange = "Đơn giá từ 0 đến 100.000.000.000 ₫";

    public static void MapAdditionalQuotationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization();
        group.MapPost("projects/{projectId:guid}/quotations/from-revisions", CreateFromRevisions);
        group.MapPost("projects/{projectId:guid}/quotations/from-revisions/preview", Preview);
    }

    private sealed record Row(RevisionItem Item, RevisionRound Round);

    private sealed record Quotable(Project Project, Guid[] Ids, List<Row> Ordered, Dictionary<Guid, Deliverable> Deliverables);

    /// <summary>Shared by preview and create: 404 / 400 / 422 as BR-46 requires; create tracks the rows (links written with xmin).</summary>
    private static async Task<(Quotable? Value, IResult? Error)> LoadQuotableAsync(
        AppDbContext db, Guid projectId, Guid[]? revisionItemIds, string? title, bool track)
    {
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return (null, Results.NotFound());

        var ids = revisionItemIds?.Distinct().ToArray() ?? [];
        var errors = new Dictionary<string, string[]>();
        if (ids.Length is 0 or > MaxItems) errors["revisionItemIds"] = [$"Chọn từ 1 đến {MaxItems} mục tính phí"];
        if (title is { Length: > 200 }) errors["title"] = ["Tối đa 200 ký tự"];
        if (errors.Count > 0) return (null, Results.ValidationProblem(errors));

        var revisionItems = track ? db.RevisionItems : db.RevisionItems.AsNoTracking();
        var revisionRounds = track ? db.RevisionRounds : db.RevisionRounds.AsNoTracking();
        var rows = await (from i in revisionItems
                          join r in revisionRounds on i.RoundId equals r.Id
                          where r.ProjectId == projectId && ids.Contains(i.Id)
                          select new { Item = i, Round = r }).ToListAsync();
        if (rows.Count != ids.Length) return (null, Invalid("revisionItemIds", "Có mục không thuộc dự án này"));
        var notQuotable = rows.Where(x => x.Item.Billing != RevisionBillings.Chargeable || x.Item.QuotationItemId != null
                                          || x.Item.WorkStatus == RevisionWorkStatuses.Cancelled).ToList();
        if (notQuotable.Count > 0)
            return (null, QuotationReads.BusinessRule("Chỉ tạo báo giá phát sinh từ mục tính phí chưa báo giá (BR-46): "
                + string.Join(", ", notQuotable.Take(5).Select(x => Truncate(x.Item.Description, 80)))
                + (notQuotable.Count > 5 ? $" … và {notQuotable.Count - 5} mục khác" : "")));

        var deliverableIds = rows.Where(x => x.Round.DeliverableId != null).Select(x => x.Round.DeliverableId!.Value).Distinct().ToList();
        var deliverables = await db.Deliverables.AsNoTracking().Where(d => deliverableIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id);

        // display order: deliverable order (project-level rounds last), round number, item order
        var ordered = rows
            .OrderBy(x => x.Round.DeliverableId is { } did ? deliverables[did].SortOrder : int.MaxValue)
            .ThenBy(x => x.Round.DeliverableId)
            .ThenBy(x => x.Round.RoundNo)
            .ThenBy(x => x.Item.SortOrder).ThenBy(x => x.Item.CreatedAt)
            .Select(x => new Row(x.Item, x.Round))
            .ToList();
        return (new Quotable(project, ids, ordered, deliverables), null);
    }

    private static ChargeableItem ToChargeable(Row x, Quotable q, Service? service, long? unitPrice)
    {
        var d = x.Round.DeliverableId is { } did ? q.Deliverables[did] : null;
        return new ChargeableItem(x.Item.Id, x.Round.Id, x.Round.RoundNo, x.Item.Type, x.Item.Description,
            d?.Name, d?.ExtraRoundPrice ?? 0, service?.Name, service?.BasePrice ?? 0, unitPrice);
    }

    /// <summary>Plan 09: the lines from-revisions would build with no price typed (suggestions only). Writes nothing.</summary>
    private static async Task<IResult> Preview(Guid projectId, AdditionalPreviewRequest body, AppDbContext db)
    {
        var (q, error) = await LoadQuotableAsync(db, projectId, body.RevisionItemIds, null, track: false);
        if (error is not null) return error;

        var plan = AdditionalQuotation.Plan(q!.Ordered.Select(x => ToChargeable(x, q, null, null)).ToList(), q.Project.Name);
        return Results.Ok(new AdditionalPreviewDto(plan.Lines.Select((line, i) => new AdditionalPreviewLineDto(
            plan.LineKinds[i],
            q.Ordered.Select(x => x.Item.Id).Where(id => plan.LineIndexByItemId[id] == i).ToList(),
            line.Description, line.Details, line.UnitPrice)).ToList()));
    }

    /// <summary>REV-04 / BR-46: ADDITIONAL DRAFT from CHARGEABLE items not quoted yet; each item links to its line.</summary>
    private static async Task<IResult> CreateFromRevisions(
        Guid projectId, CreateAdditionalQuotationRequest body, AppDbContext db, ActivityWriter activity)
    {
        var (q, error) = await LoadQuotableAsync(db, projectId, body.RevisionItemIds, body.Title, track: true);
        if (error is not null) return error;
        var rowsById = q!.Ordered.ToDictionary(x => x.Item.Id);
        bool IsNewDeliverable(Guid id) => rowsById.TryGetValue(id, out var x) && x.Item.Type == RevisionItemTypes.NewDeliverable;

        var picks = (body.NewDeliverableServices ?? [])
            .GroupBy(s => s.RevisionItemId)
            .ToDictionary(g => g.Key, g => g.Last());
        if (picks.Keys.Any(id => !IsNewDeliverable(id)))
            return Invalid("newDeliverableServices", "Chỉ chọn dịch vụ cho mục \"Hạng mục mới\" có trong danh sách");
        if (picks.Values.Any(p => p.UnitPrice is < 0 or > QuotationValidation.MaxUnitPrice))
            return Invalid("newDeliverableServices", PriceRange);
        var linePrices = (body.LinePrices ?? [])
            .GroupBy(p => p.RevisionItemId)
            .ToDictionary(g => g.Key, g => g.Last().UnitPrice);
        if (linePrices.Keys.Any(id => !rowsById.ContainsKey(id) || IsNewDeliverable(id)))
            return Invalid("linePrices", "Chỉ nhập giá cho mục có trong danh sách (Hạng mục mới nhập giá ở newDeliverableServices)");
        if (linePrices.Values.Any(p => p is < 0 or > QuotationValidation.MaxUnitPrice))
            return Invalid("linePrices", PriceRange);

        var serviceIds = picks.Values.Where(p => p.ServiceId != null).Select(p => p.ServiceId!.Value).Distinct().ToList();
        var services = await db.Services.AsNoTracking().Where(s => serviceIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id);
        if (services.Count != serviceIds.Count) return Invalid("newDeliverableServices", "Dịch vụ không tồn tại");

        AdditionalQuotationPlan plan;
        try
        {
            plan = AdditionalQuotation.Plan(q.Ordered.Select(x =>
            {
                var pick = picks.GetValueOrDefault(x.Item.Id);
                var service = pick?.ServiceId is { } sid ? services[sid] : null;
                var unitPrice = pick?.UnitPrice ?? (linePrices.TryGetValue(x.Item.Id, out var typed) ? typed : (long?)null);
                return ToChargeable(x, q, service, unitPrice);
            }).ToList(), q.Project.Name);
        }
        catch (DomainException ex)
        {
            return Invalid("linePrices", ex.Detail ?? ex.Code);   // one round line, two typed prices
        }

        var settings = await db.Settings.AsNoTracking().FirstAsync();
        var title = string.IsNullOrWhiteSpace(body.Title) ? $"Báo giá phát sinh — {q.Project.Name}" : body.Title.Trim();
        Quotation quotation;
        try
        {
            quotation = Quotation.CreateDraft(projectId, QuotationKinds.Additional, title,
                settings.DefaultValidityDays, settings.DefaultDepositPercent, settings.DefaultTerms, plan.Lines);
        }
        catch (DomainException ex)
        {
            return QuotationReads.FromDomain(ex);
        }

        // revision_items → quotation_items FK is not in the EF model: the lines must exist before the links are written
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            db.Quotations.Add(quotation);
            activity.Write("quotation", quotation.Id, "quotation.created",
                clientId: q.Project.ClientId, projectId: projectId,
                data: new
                {
                    kind = quotation.Kind, items = quotation.Items.Count, revisionItemIds = q.Ids, total = quotation.Total,
                    newDeliverableServices = picks.Values.Select(p => new { p.RevisionItemId, p.ServiceId, p.UnitPrice }),
                    linePrices = linePrices.Select(kv => new { revisionItemId = kv.Key, unitPrice = kv.Value }),
                });
            await db.SaveChangesAsync();

            foreach (var x in q.Ordered)
                x.Item.LinkQuotationLine(quotation.Items[plan.LineIndexByItemId[x.Item.Id]].Id,
                    picks.GetValueOrDefault(x.Item.Id)?.ServiceId);
            await db.SaveChangesAsync();   // xmin: a concurrent from-revisions on the same items → 409, draft rolled back
            await tx.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return QuotationReads.Concurrency();
        }

        return Results.Created($"/api/admin/quotations/{quotation.Id}", new CreateQuotationResponse(quotation.Id, []));
    }

    private static IResult Invalid(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max] + "…";
}
