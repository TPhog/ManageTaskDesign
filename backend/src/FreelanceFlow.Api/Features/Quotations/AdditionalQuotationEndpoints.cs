using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Quotations;
using FreelanceFlow.Api.Domain.Requests;
using FreelanceFlow.Api.Domain.Revisions;
using FreelanceFlow.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Quotations;

/// <summary>Service picked for a NEW_DELIVERABLE item (BR-46 step 2) — revision_items has no service column.</summary>
public sealed record NewDeliverableService(Guid RevisionItemId, Guid ServiceId);

public sealed record CreateAdditionalQuotationRequest(string? Title, Guid[]? RevisionItemIds, NewDeliverableService[]? NewDeliverableServices);

public static class AdditionalQuotationEndpoints
{
    private const int MaxItems = 200;

    public static void MapAdditionalQuotationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/admin").RequireAuthorization()
            .MapPost("projects/{projectId:guid}/quotations/from-revisions", CreateFromRevisions);
    }

    /// <summary>REV-04 / BR-46: ADDITIONAL DRAFT from CHARGEABLE items not quoted yet; each item links to its line.</summary>
    private static async Task<IResult> CreateFromRevisions(
        Guid projectId, CreateAdditionalQuotationRequest body, AppDbContext db, ActivityWriter activity)
    {
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return Results.NotFound();

        var ids = body.RevisionItemIds?.Distinct().ToArray() ?? [];
        var errors = new Dictionary<string, string[]>();
        if (ids.Length is 0 or > MaxItems) errors["revisionItemIds"] = [$"Chọn từ 1 đến {MaxItems} mục tính phí"];
        if (body.Title is { Length: > 200 }) errors["title"] = ["Tối đa 200 ký tự"];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        // tracked: the links are written with the xmin check of each item
        var rows = await (from i in db.RevisionItems
                          join r in db.RevisionRounds on i.RoundId equals r.Id
                          where r.ProjectId == projectId && ids.Contains(i.Id)
                          select new { Item = i, Round = r }).ToListAsync();
        if (rows.Count != ids.Length)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["revisionItemIds"] = ["Có mục không thuộc dự án này"],
            });
        var notQuotable = rows.Where(x => x.Item.Billing != RevisionBillings.Chargeable || x.Item.QuotationItemId != null
                                          || x.Item.WorkStatus == RevisionWorkStatuses.Cancelled).ToList();
        if (notQuotable.Count > 0)
            return QuotationReads.BusinessRule("Chỉ tạo báo giá phát sinh từ mục tính phí chưa báo giá (BR-46): "
                + string.Join(", ", notQuotable.Select(x => x.Item.Description)));

        var serviceByItem = (body.NewDeliverableServices ?? [])
            .GroupBy(s => s.RevisionItemId)
            .ToDictionary(g => g.Key, g => g.Last().ServiceId);
        if (serviceByItem.Keys.Any(itemId => !rows.Any(x => x.Item.Id == itemId && x.Item.Type == RevisionItemTypes.NewDeliverable)))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["newDeliverableServices"] = ["Chỉ chọn dịch vụ cho mục \"Hạng mục mới\" có trong danh sách"],
            });
        var serviceIds = serviceByItem.Values.Distinct().ToList();
        var services = await db.Services.AsNoTracking().Where(s => serviceIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id);
        if (services.Count != serviceIds.Count)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["newDeliverableServices"] = ["Dịch vụ không tồn tại"],
            });

        var deliverableIds = rows.Where(x => x.Round.DeliverableId != null).Select(x => x.Round.DeliverableId!.Value).Distinct().ToList();
        var deliverables = await db.Deliverables.AsNoTracking().Where(d => deliverableIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id);

        // display order: deliverable order (project-level rounds last), round number, item order
        var ordered = rows
            .OrderBy(x => x.Round.DeliverableId is { } did ? deliverables[did].SortOrder : int.MaxValue)
            .ThenBy(x => x.Round.DeliverableId)
            .ThenBy(x => x.Round.RoundNo)
            .ThenBy(x => x.Item.SortOrder).ThenBy(x => x.Item.CreatedAt)
            .ToList();
        var plan = AdditionalQuotation.Plan(ordered.Select(x =>
        {
            var d = x.Round.DeliverableId is { } did ? deliverables[did] : null;
            var service = serviceByItem.TryGetValue(x.Item.Id, out var sid) ? services[sid] : null;
            return new ChargeableItem(x.Item.Id, x.Round.Id, x.Round.RoundNo, x.Item.Type, x.Item.Description,
                d?.Name, d?.ExtraRoundPrice ?? 0, service?.Name, service?.BasePrice ?? 0);
        }).ToList(), project.Name);

        var settings = await db.Settings.AsNoTracking().FirstAsync();
        var title = string.IsNullOrWhiteSpace(body.Title) ? $"Báo giá phát sinh — {project.Name}" : body.Title.Trim();
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
                clientId: project.ClientId, projectId: projectId,
                data: new { kind = quotation.Kind, items = quotation.Items.Count, revisionItemIds = ids, total = quotation.Total });
            await db.SaveChangesAsync();

            foreach (var x in ordered)
                x.Item.LinkQuotationLine(quotation.Items[plan.LineIndexByItemId[x.Item.Id]].Id);
            await db.SaveChangesAsync();   // xmin: a concurrent from-revisions on the same items → 409, draft rolled back
            await tx.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return QuotationReads.Concurrency();
        }

        return Results.Created($"/api/admin/quotations/{quotation.Id}", new CreateQuotationResponse(quotation.Id, []));
    }
}
