using FreelanceFlow.Api.Domain.Quotations;
using FreelanceFlow.Api.Domain.Requests;

namespace FreelanceFlow.Api.Domain.Revisions;

/// <summary>One CHARGEABLE item picked for an ADDITIONAL quotation, with what BR-46 needs to price it.</summary>
public sealed record ChargeableItem(
    Guid ItemId, Guid RoundId, int RoundNo, string Type, string Description,
    string? DeliverableName, long ExtraRoundPrice, string? ServiceName, long ServicePrice);

/// <summary>Lines for Quotation.CreateDraft (same order) and, per revision item, the index of its line.</summary>
public sealed record AdditionalQuotationPlan(IReadOnlyList<QuotationItemInput> Lines, IReadOnlyDictionary<Guid, int> LineIndexByItemId);

public static class AdditionalQuotation
{
    private const int MaxLineDescription = 500;

    /// <summary>
    /// BR-46, items in display order: MINOR/MAJOR items of one round → one line "Vòng sửa thứ {n} — {deliverable}" at
    /// extra_round_price (project-level round: project name, 0); each NEW_DELIVERABLE → one line (service name +
    /// base_price when a service was picked, else its description at 0); each SCOPE_CHANGE → one line at 0.
    /// Every line: quantity 1, source_type REVISION, source_id = round id (docs/04 quotation_items.source_id).
    /// </summary>
    public static AdditionalQuotationPlan Plan(IReadOnlyList<ChargeableItem> items, string projectName)
    {
        var lines = new List<QuotationItemInput>();
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
                        && i.Type is RevisionItemTypes.Minor or RevisionItemTypes.Major);
                    lines.Add(Line($"Vòng sửa thứ {item.RoundNo} — {item.DeliverableName ?? projectName}",
                        string.Join("\n", roundItems.Select(i => "• " + i.Description)),
                        item.DeliverableName is null ? 0 : item.ExtraRoundPrice, item.RoundId));
                }
                index[item.ItemId] = at;
                continue;
            }

            index[item.ItemId] = lines.Count;
            lines.Add(item.Type == RevisionItemTypes.NewDeliverable && item.ServiceName is not null
                ? Line(item.ServiceName, item.Description, item.ServicePrice, item.RoundId)
                : Line(item.Description, "", 0, item.RoundId));
        }
        return new AdditionalQuotationPlan(lines, index);
    }

    private static QuotationItemInput Line(string description, string details, long unitPrice, Guid roundId) =>
        new(null, Truncate(description), details, 1, unitPrice, QuotationItemSources.Revision, roundId);

    private static string Truncate(string text) =>
        text.Length <= MaxLineDescription ? text : text[..(MaxLineDescription - 1)] + "…";
}
