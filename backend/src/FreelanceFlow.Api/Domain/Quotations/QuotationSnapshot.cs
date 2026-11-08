using System.Text.Json;

namespace FreelanceFlow.Api.Domain.Quotations;

/// <summary>
/// quotations.snapshot, schemaVersion 1 (docs/04 §5). Built once at send and the only display source afterwards
/// (BR-39). It carries the bank account (docs/06 §5) and deliberately no internal notes, so it is portal-safe.
/// </summary>
public sealed record QuotationSnapshot(
    int SchemaVersion,
    string Number,
    int Version,
    string Kind,
    string Title,
    DateOnly IssueDate,
    DateOnly ExpiryDate,
    SnapshotDesigner Designer,
    SnapshotClient Client,
    SnapshotProject Project,
    IReadOnlyList<SnapshotItem> Items,
    long Subtotal,
    long DiscountAmount,
    int VatPercent,
    long VatAmount,
    long Total,
    int DepositPercent,
    long DepositAmount,
    string Terms,
    string NotesToClient,
    SnapshotBank Bank)
{
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Call after Number, IssueDate and ExpiryDate are set (see <see cref="Quotation.Send"/>).</summary>
    public static QuotationSnapshot Build(
        Quotation q, SnapshotDesigner designer, SnapshotClient client, SnapshotProject project, SnapshotBank bank) =>
        new(CurrentSchemaVersion,
            q.Number ?? throw new InvalidOperationException("Snapshot needs the quotation number"),
            q.Version,
            q.Kind,
            q.Title,
            q.IssueDate ?? throw new InvalidOperationException("Snapshot needs issue_date"),
            q.ExpiryDate ?? throw new InvalidOperationException("Snapshot needs expiry_date"),
            designer,
            client,
            project,
            q.Items.OrderBy(i => i.SortOrder)
                .Select(i => new SnapshotItem(i.Description, i.Details, i.Quantity, i.UnitPrice, i.Amount))
                .ToList(),
            q.Subtotal, q.DiscountAmount, q.VatPercent, q.VatAmount, q.Total,
            q.DepositPercent, q.DepositAmount,
            q.Terms, q.NotesToClient,
            bank);

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public static QuotationSnapshot FromJson(string json) =>
        JsonSerializer.Deserialize<QuotationSnapshot>(json, Json)
        ?? throw new InvalidOperationException("Empty quotation snapshot");
}

public sealed record SnapshotDesigner(string DisplayName, string? Email, string? Phone);

public sealed record SnapshotClient(string Name, string? Company);

public sealed record SnapshotProject(Guid Id, string Name);

public sealed record SnapshotItem(string Description, string Details, int Quantity, long UnitPrice, long Amount);

public sealed record SnapshotBank(string Bin, string ShortName, string AccountNo, string AccountName);
