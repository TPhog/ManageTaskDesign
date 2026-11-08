using System.Globalization;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Requests;

namespace FreelanceFlow.Api.Domain.Revisions;

/// <summary>Quota of a deliverable when a round is planned (docs/02 §5.1).</summary>
public sealed record DeliverableQuota(string Name, int IncludedRounds, int UsedRounds, long ExtraRoundPrice);

public sealed record BillingSuggestion(string Billing, string Reason);

/// <summary>Billing null → take the suggestion (docs/05 §3.7.1).</summary>
public sealed record PlannedItemInput(string Type, string? Billing);

public sealed record PlannedItem(string Billing, string SuggestedBilling, string SuggestionReason);

/// <summary>Counted = the round uses exactly one quota round (BR-42).</summary>
public sealed record RoundPlan(IReadOnlyList<PlannedItem> Items, bool Counted);

/// <summary>Init-only (not positional) so EF can compose member access on the projection.</summary>
public sealed record DeliverableRoundUsage
{
    public Guid DeliverableId { get; init; }
    public int UsedRounds { get; init; }
}

public static class RevisionQuota
{
    /// <summary>§5.1: an item makes its round a counted round when IN_QUOTA and not CANCELLED.</summary>
    public static bool CountsTowardQuota(string billing, string workStatus) =>
        billing == RevisionBillings.InQuota && workStatus != RevisionWorkStatuses.Cancelled;

    public static bool RoundIsCounted(IEnumerable<RevisionItem> roundItems) =>
        roundItems.Any(i => CountsTowardQuota(i.Billing, i.WorkStatus));

    public static int RemainingRounds(int includedRounds, int usedRounds) => Math.Max(0, includedRounds - usedRounds);

    /// <summary>
    /// docs/04 §4.1 — the only used_rounds query. Filter <paramref name="rounds"/> first (by project or deliverable);
    /// a deliverable without a counted round is absent (= 0). Pure LINQ: EF translates it, unit tests run it in memory.
    /// </summary>
    public static IQueryable<DeliverableRoundUsage> UsedRounds(IQueryable<RevisionRound> rounds, IQueryable<RevisionItem> items) =>
        rounds
            .Where(r => r.DeliverableId != null && items.Any(i => i.RoundId == r.Id
                && i.Billing == RevisionBillings.InQuota && i.WorkStatus != RevisionWorkStatuses.Cancelled))
            .GroupBy(r => r.DeliverableId!.Value)
            .Select(g => new DeliverableRoundUsage { DeliverableId = g.Key, UsedRounds = g.Count() });

    /// <summary>docs/02 §5.3 suggestBilling. <paramref name="quota"/> null = project-level round.</summary>
    public static BillingSuggestion SuggestBilling(string itemType, DeliverableQuota? quota, bool roundAlreadyCounted)
    {
        if (itemType == RevisionItemTypes.ScopeChange)
            return new(RevisionBillings.Chargeable, "Thay đổi phạm vi luôn tính phí");
        if (itemType == RevisionItemTypes.NewDeliverable)
            return new(RevisionBillings.Chargeable, "Hạng mục mới luôn tính phí");
        if (quota is null)
            return new(RevisionBillings.Chargeable, "Vòng sửa cấp dự án không dùng quota");
        if (roundAlreadyCounted)
            return new(RevisionBillings.InQuota, $"Vòng này đã tính quota của {quota.Name} — không tốn thêm vòng");
        var usage = $"{quota.Name} đã dùng {quota.UsedRounds}/{quota.IncludedRounds} vòng";
        return quota.UsedRounds < quota.IncludedRounds
            ? new(RevisionBillings.InQuota, $"{usage} → Trong quota")
            : new(RevisionBillings.Chargeable, $"{usage} → Tính phí {Vnd(quota.ExtraRoundPrice)}/vòng");
    }

    /// <summary>
    /// Billing of each item of one new round, independent of item order: explicit billing wins, null takes the suggestion.
    /// An explicit IN_QUOTA item makes the round counted up front, so null items then stay IN_QUOTA (BR-42 — one round
    /// costs one quota round). Explicit IN_QUOTA on a project-level round is rejected (BR-41).
    /// </summary>
    public static RoundPlan PlanRound(DeliverableQuota? quota, IReadOnlyList<PlannedItemInput> items)
    {
        foreach (var input in items)
        {
            if (!RevisionItemTypes.All.Contains(input.Type))
                throw new DomainException("VALIDATION_FAILED", "Loại yêu cầu sửa không hợp lệ");
            if (input.Billing is not null && !RevisionBillings.All.Contains(input.Billing))
                throw new DomainException("VALIDATION_FAILED", "Cách tính phí không hợp lệ");
            if (quota is null && input.Billing == RevisionBillings.InQuota)
                throw RevisionItem.ProjectLevelInQuota();
        }
        var counted = items.Any(i => i.Billing == RevisionBillings.InQuota);
        var planned = new List<PlannedItem>(items.Count);
        foreach (var input in items)
        {
            var suggestion = SuggestBilling(input.Type, quota, counted);
            var billing = input.Billing ?? suggestion.Billing;
            if (billing == RevisionBillings.InQuota) counted = true;
            planned.Add(new PlannedItem(billing, suggestion.Billing, suggestion.Reason));
        }
        return new RoundPlan(planned, counted);
    }

    /// <summary>"200.000 ₫" without ICU culture data (container images may run globalization-invariant).</summary>
    public static string Vnd(long amount) =>
        amount.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.') + " ₫";
}
