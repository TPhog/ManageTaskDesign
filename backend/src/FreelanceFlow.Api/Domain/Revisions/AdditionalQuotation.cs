using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Quotations;
using FreelanceFlow.Api.Domain.Requests;

namespace FreelanceFlow.Api.Domain.Revisions;

/// <summary>What kind of line BR-46 builds; the preview endpoint shows it to the designer.</summary>
public static class AdditionalLineKinds
{
    public const string Round = "ROUND";
    public const string NewDeliverable = "NEW_DELIVERABLE";
    public const string ScopeChange = "SCOPE_CHANGE";
}

/// <summary>
/// One CHARGEABLE item picked for an ADDITIONAL quotation, with what BR-46 needs to price it.
/// UnitPrice = the price the designer typed for this item's line (plan 09), null = use the suggestion.
/// </summary>
public sealed record ChargeableItem(
    Guid ItemId, Guid RoundId, int RoundNo, string Type, string Description,
    string? DeliverableName, long ExtraRoundPrice, string? ServiceName, long ServicePrice, long? UnitPrice = null);

/// <summary>Lines for Quotation.CreateDraft (same order), per revision item the index of its line, and each line's kind.</summary>
public sealed record AdditionalQuotationPlan(
    IReadOnlyList<QuotationItemInput> Lines, IReadOnlyDictionary<Guid, int> LineIndexByItemId, IReadOnlyList<string> LineKinds);

public static class AdditionalQuotation
{
    private const int MaxLineDescription = 500;

    /// <summary>
    /// BR-46, items in display order: MINOR/MAJOR items of one round → one line "Vòng sửa thứ {n} — {deliverable}";
    /// each NEW_DELIVERABLE → one line (service name when a service was picked, else its description); each
    /// SCOPE_CHANGE → one line. Price of a line: typed price → service.base_price (new deliverable with a service) →
    /// suggestion (extra_round_price of the round's deliverable; project-level round and scope change: 0) → 0.
    /// A round line takes one typed price: two different prices on items of the same round → VALIDATION_FAILED.
    /// Every line: quantity 1, source_type REVISION, source_id = round id (docs/04 quotation_items.source_id).
    /// </summary>
    public static AdditionalQuotationPlan Plan(IReadOnlyList<ChargeableItem> items, string projectName)
    {
        var lines = new List<QuotationItemInput>();
        var kinds = new List<string>();
        var index = new Dictionary<Guid, int>();
        var roundLine = new Dictionary<Guid, int>();

        foreach (var item in items)
        {
            if (item.Type is RevisionItemTypes.Minor or RevisionItemTypes.Major)
            {
                if (!roundLine.TryGetValue(item.RoundId, out var at))
                {
                    at = lines.Count;
                    roundLine[item.RoundId] = at;
                    var roundItems = items.Where(i => i.RoundId == item.RoundId
                        && i.Type is RevisionItemTypes.Minor or RevisionItemTypes.Major).ToList();
                    var typed = roundItems.Where(i => i.UnitPrice is not null).Select(i => i.UnitPrice!.Value).Distinct().ToList();
                    if (typed.Count > 1)
                        throw new DomainException("VALIDATION_FAILED",
                            $"Các mục của vòng sửa thứ {item.RoundNo} nằm chung một dòng — chỉ nhập một đơn giá");
                    var suggested = item.DeliverableName is null ? 0 : item.ExtraRoundPrice;
                    lines.Add(Line($"Vòng sửa thứ {item.RoundNo} — {item.DeliverableName ?? projectName}",
                        string.Join("\n", roundItems.Select(i => "• " + i.Description)),
                        typed.Count == 1 ? typed[0] : suggested, item.RoundId));
                    kinds.Add(AdditionalLineKinds.Round);
                }
                index[item.ItemId] = at;
                continue;
            }

            index[item.ItemId] = lines.Count;
            if (item.Type == RevisionItemTypes.NewDeliverable)
            {
                lines.Add(item.ServiceName is not null
                    ? Line(item.ServiceName, item.Description, item.UnitPrice ?? item.ServicePrice, item.RoundId)
                    : Line(item.Description, "", item.UnitPrice ?? 0, item.RoundId));
                kinds.Add(AdditionalLineKinds.NewDeliverable);
            }
            else
            {
                lines.Add(Line(item.Description, "", item.UnitPrice ?? 0, item.RoundId));
                kinds.Add(AdditionalLineKinds.ScopeChange);
            }
        }
        return new AdditionalQuotationPlan(lines, index, kinds);
    }

    private static QuotationItemInput Line(string description, string details, long unitPrice, Guid roundId) =>
        new(null, Truncate(description), details, 1, unitPrice, QuotationItemSources.Revision, roundId);

    private static string Truncate(string text) =>
        text.Length <= MaxLineDescription ? text : text[..(MaxLineDescription - 1)] + "…";
}
