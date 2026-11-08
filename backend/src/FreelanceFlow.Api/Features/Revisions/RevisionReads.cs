using System.Linq.Expressions;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Quotations;
using FreelanceFlow.Api.Domain.Requests;
using FreelanceFlow.Api.Domain.Revisions;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Revisions;

internal static class RevisionReads
{
    /// <summary>Rounds matching <paramref name="filter"/> with items, quotation link (+ effective status) and linked tasks.</summary>
    public static async Task<List<RoundDto>> LoadRoundsAsync(
        AppDbContext db, Expression<Func<RevisionRound, bool>> filter, DateOnly today,
        IReadOnlyDictionary<Guid, BillingSuggestion>? suggestions = null)
    {
        var rounds = await db.RevisionRounds.AsNoTracking().Where(filter)
            .OrderBy(r => r.RoundNo).ThenBy(r => r.CreatedAt)
            .ToListAsync();
        if (rounds.Count == 0) return [];
        var roundIds = rounds.Select(r => r.Id).ToList();

        var items = await db.RevisionItems.AsNoTracking()
            .Where(i => roundIds.Contains(i.RoundId))
            .OrderBy(i => i.SortOrder).ThenBy(i => i.CreatedAt)
            .Select(i => new { Item = i, RowVersion = EF.Property<uint>(i, "xmin") })
            .ToListAsync();

        var lineIds = items.Where(x => x.Item.QuotationItemId != null).Select(x => x.Item.QuotationItemId!.Value).Distinct().ToList();
        var links = (await (from qi in db.QuotationItems.AsNoTracking()
                            join q in db.Quotations.AsNoTracking() on qi.QuotationId equals q.Id
                            where lineIds.Contains(qi.Id)
                            select new { LineId = qi.Id, q.Id, q.Number, q.Version, q.Status, q.ExpiryDate })
                .ToListAsync())
            .ToDictionary(x => x.LineId, x => new LinkedQuotationDto(x.Id, x.Number, x.Version, x.Status,
                new Quotation { Status = x.Status, ExpiryDate = x.ExpiryDate }.EffectiveStatus(today)));

        var itemIds = items.Select(x => x.Item.Id).ToList();
        var tasks = (await db.Tasks.AsNoTracking()
                .Where(t => t.RevisionItemId != null && itemIds.Contains(t.RevisionItemId.Value))
                .OrderBy(t => t.CreatedAt)
                .Select(t => new { ItemId = t.RevisionItemId!.Value, t.Id, t.Title, t.Status })
                .ToListAsync())
            .ToLookup(t => t.ItemId, t => new LinkedTaskDto(t.Id, t.Title, t.Status));

        var serviceIds = items.Where(x => x.Item.ServiceId != null).Select(x => x.Item.ServiceId!.Value).Distinct().ToList();
        var serviceNames = await db.Services.AsNoTracking()
            .Where(s => serviceIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name);

        var deliverableIds = rounds.Where(r => r.DeliverableId != null).Select(r => r.DeliverableId!.Value)
            .Concat(items.Where(x => x.Item.CreatedDeliverableId != null).Select(x => x.Item.CreatedDeliverableId!.Value))
            .Distinct().ToList();
        var names = await db.Deliverables.AsNoTracking()
            .Where(d => deliverableIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name);

        var byRound = items.ToLookup(x => x.Item.RoundId);
        return rounds.Select(r => new RoundDto(
            r.Id, r.ProjectId, r.DeliverableId,
            r.DeliverableId is { } did ? names.GetValueOrDefault(did) : null,
            r.RoundNo, r.ReceivedAt, r.SourceRequestId,
            RevisionQuota.RoundIsCounted(byRound[r.Id].Select(x => x.Item)),
            r.CreatedAt,
            byRound[r.Id].Select(x => ToItemDto(x.Item, x.RowVersion,
                x.Item.QuotationItemId is { } line ? links.GetValueOrDefault(line) : null,
                tasks[x.Item.Id].ToList(),
                suggestions?.GetValueOrDefault(x.Item.Id), serviceNames, names)).ToList())).ToList();
    }

    /// <summary>docs/08 …/revisions: deliverables (sort order) with quota + rounds, then project-level rounds.</summary>
    public static async Task<ProjectRevisionsDto> LoadProjectAsync(AppDbContext db, Guid projectId, DateOnly today)
    {
        var deliverables = await db.Deliverables.AsNoTracking()
            .Where(d => d.ProjectId == projectId)
            .OrderBy(d => d.SortOrder).ThenBy(d => d.CreatedAt)
            .ToListAsync();
        var used = await RevisionQuota.UsedRounds(db.RevisionRounds.Where(r => r.ProjectId == projectId), db.RevisionItems)
            .ToDictionaryAsync(x => x.DeliverableId, x => x.UsedRounds);
        var rounds = await LoadRoundsAsync(db, r => r.ProjectId == projectId, today);
        var byDeliverable = rounds.Where(r => r.DeliverableId != null).ToLookup(r => r.DeliverableId!.Value);

        return new ProjectRevisionsDto(
            projectId,
            deliverables.Select(d =>
            {
                var u = used.GetValueOrDefault(d.Id);
                return new DeliverableRevisionsDto(d.Id, d.Name, d.Status, d.IncludedRounds, u,
                    RevisionQuota.RemainingRounds(d.IncludedRounds, u), d.ExtraRoundPrice, byDeliverable[d.Id].ToList());
            }).ToList(),
            rounds.Where(r => r.DeliverableId == null).ToList());
    }

    public static async Task<RevisionItemDto?> LoadItemAsync(AppDbContext db, Guid itemId, DateOnly today, BillingSuggestion? suggestion = null)
    {
        var roundId = await db.RevisionItems.Where(i => i.Id == itemId).Select(i => (Guid?)i.RoundId).FirstOrDefaultAsync();
        if (roundId is not { } rid) return null;
        var rounds = await LoadRoundsAsync(db, r => r.Id == rid, today,
            suggestion is null ? null : new Dictionary<Guid, BillingSuggestion> { [itemId] = suggestion });
        return rounds.Single().Items.Single(i => i.Id == itemId);
    }

    private static RevisionItemDto ToItemDto(RevisionItem i, uint rowVersion, LinkedQuotationDto? quotation,
        IReadOnlyList<LinkedTaskDto> tasks, BillingSuggestion? suggestion,
        IReadOnlyDictionary<Guid, string> serviceNames, IReadOnlyDictionary<Guid, string> deliverableNames) =>
        new(i.Id, i.RoundId, i.Description, i.Type, i.Billing, i.WorkStatus, i.QuotationItemId, quotation,
            i.ScopeOverrideReason, i.InternalNotes, i.SortOrder, rowVersion, suggestion?.Billing, suggestion?.Reason, tasks,
            i.ServiceId, i.ServiceId is { } sid ? serviceNames.GetValueOrDefault(sid) : null,
            i.CreatedDeliverableId, i.CreatedDeliverableId is { } did ? deliverableNames.GetValueOrDefault(did) : null);
}
