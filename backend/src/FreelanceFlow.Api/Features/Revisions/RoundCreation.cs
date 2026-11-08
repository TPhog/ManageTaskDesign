using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Domain.Requests;
using FreelanceFlow.Api.Domain.Revisions;
using FreelanceFlow.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Revisions;

public sealed record CreatedRounds(IReadOnlyList<RevisionRound> Rounds, IReadOnlyDictionary<Guid, BillingSuggestion> Suggestions);

/// <summary>
/// Adds rounds + items + activity to the DbContext; the caller saves once (REQ-04 triage, POST …/rounds).
/// round_no continues per deliverable, project-level rounds number separately (REV-01). Billing null takes the
/// suggestion, and a round counted earlier in the same call already uses up quota for the next one.
/// Throws DomainException: VALIDATION_FAILED (deliverable of another project), BUSINESS_RULE (BR-41).
/// </summary>
internal static class RoundCreation
{
    public static async Task<CreatedRounds> AddAsync(
        AppDbContext db, ActivityWriter activity, Project project, IReadOnlyList<RoundBody> bodies,
        Guid? sourceRequestId, DateOnly today)
    {
        var deliverableIds = bodies.Where(b => b.DeliverableId != null).Select(b => b.DeliverableId!.Value).Distinct().ToList();
        var deliverables = await db.Deliverables.AsNoTracking()
            .Where(d => d.ProjectId == project.Id && deliverableIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id);
        if (deliverables.Count != deliverableIds.Count)
            throw new DomainException("VALIDATION_FAILED", "Hạng mục không thuộc dự án này");

        var used = await RevisionQuota.UsedRounds(db.RevisionRounds.Where(r => r.ProjectId == project.Id), db.RevisionItems)
            .ToDictionaryAsync(x => x.DeliverableId, x => x.UsedRounds);
        var lastNo = await db.RevisionRounds
            .Where(r => r.ProjectId == project.Id && r.DeliverableId != null)
            .GroupBy(r => r.DeliverableId!.Value)
            .Select(g => new { DeliverableId = g.Key, Max = g.Max(r => r.RoundNo) })
            .ToDictionaryAsync(x => x.DeliverableId, x => x.Max);
        var lastProjectNo = await db.RevisionRounds
            .Where(r => r.ProjectId == project.Id && r.DeliverableId == null)
            .MaxAsync(r => (int?)r.RoundNo) ?? 0;

        var rounds = new List<RevisionRound>();
        var suggestions = new Dictionary<Guid, BillingSuggestion>();
        foreach (var body in bodies)
        {
            var d = body.DeliverableId is { } did ? deliverables[did] : null;
            var usedBefore = d is null ? 0 : used.GetValueOrDefault(d.Id);
            var quota = d is null ? null : new DeliverableQuota(d.Name, d.IncludedRounds, usedBefore, d.ExtraRoundPrice);
            var plan = RevisionQuota.PlanRound(quota, body.Items!.Select(i => new PlannedItemInput(i.Type!, i.Billing)).ToList());

            var roundNo = d is null ? ++lastProjectNo : lastNo.GetValueOrDefault(d.Id) + 1;
            if (d is not null) lastNo[d.Id] = roundNo;

            var round = RevisionRound.Create(project.Id, d?.Id, roundNo, body.ReceivedAt ?? today, sourceRequestId);
            db.RevisionRounds.Add(round);
            var roundItems = new List<RevisionItem>();
            for (var index = 0; index < body.Items!.Length; index++)
            {
                var input = body.Items[index];
                var item = round.AddItem(input.Description!, input.Type!, plan.Items[index].Billing, (index + 1) * 10);
                db.RevisionItems.Add(item);
                roundItems.Add(item);
                suggestions[item.Id] = new BillingSuggestion(plan.Items[index].SuggestedBilling, plan.Items[index].SuggestionReason);
            }

            var usedAfter = usedBefore + (d is not null && plan.Counted ? 1 : 0);
            if (d is not null) used[d.Id] = usedAfter;
            foreach (var item in roundItems)
                activity.Write("revision_item", item.Id, "revision_item.created",
                    clientId: project.ClientId, projectId: project.Id,
                    data: new
                    {
                        roundId = round.Id, type = item.Type, billing = item.Billing, deliverableId = d?.Id,
                        usedRoundsBefore = d is null ? (int?)null : usedBefore,
                        usedRoundsAfter = d is null ? (int?)null : usedAfter,
                    });
            activity.Write("revision_round", round.Id, "revision_round.created",
                clientId: project.ClientId, projectId: project.Id,
                data: new
                {
                    deliverableId = d?.Id,
                    roundNo,
                    sourceRequestId,
                    billings = plan.Items.Select(i => i.Billing).ToArray(),
                    usedRoundsBefore = d is null ? (int?)null : usedBefore,
                    usedRoundsAfter = d is null ? (int?)null : usedAfter,
                });
            rounds.Add(round);
        }
        return new CreatedRounds(rounds, suggestions);
    }
}
