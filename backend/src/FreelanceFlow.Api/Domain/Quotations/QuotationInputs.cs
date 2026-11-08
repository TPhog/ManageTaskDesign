namespace FreelanceFlow.Api.Domain.Quotations;

/// <summary>One line as the designer submits it. Id = existing row to update in place; null = new row.</summary>
public sealed record QuotationItemInput(
    Guid? Id, string Description, string Details, int Quantity, long UnitPrice, string SourceType, Guid? SourceId);

/// <summary>Full replacement of a DRAFT's editable header + lines (PUT /api/admin/quotations/{id}).</summary>
public sealed record QuotationDraftEdit(
    string Title, int ValidityDays, long DiscountAmount, int VatPercent, int DepositPercent,
    string Terms, string NotesToClient, string InternalNotes, IReadOnlyList<QuotationItemInput> Items);

/// <summary>Rows the caller must add to / remove from the DbContext after <see cref="Quotation.EditDraft"/>.</summary>
public sealed record ItemChanges(IReadOnlyList<QuotationItem> Added, IReadOnlyList<QuotationItem> Removed);

/// <summary>A payable amount offered as VietQR (BR-75). Purpose is one of <see cref="QrPurposes"/>.</summary>
public sealed record PaymentOption(string Purpose, string Label, long Amount);
