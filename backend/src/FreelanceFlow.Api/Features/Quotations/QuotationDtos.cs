using FreelanceFlow.Api.Domain.Quotations;

namespace FreelanceFlow.Api.Features.Quotations;

// ---------- requests (docs/05 §3.10) ----------

/// <summary>Plan 09: typed unit price for a deliverable line (wins over service.base_price).</summary>
public sealed record DeliverablePrice(Guid DeliverableId, long UnitPrice);

/// <summary>D1: deliverableIds = quote only those finished deliverables; omitted = every deliverable except CANCELLED.
/// Plan 09: deliverablePrices = typed unit prices, typically for deliverables without a service.</summary>
public sealed record CreateQuotationRequest(string? Kind, string? Title, bool FromDeliverables, Guid[]? DeliverableIds,
    DeliverablePrice[]? DeliverablePrices = null);

public sealed record QuotationItemRequest(
    Guid? Id, string? Description, string? Details, int Quantity, long UnitPrice, string? SourceType, Guid? SourceId);

public sealed record UpdateQuotationRequest(
    string? Title, int ValidityDays, long DiscountAmount, int VatPercent, int DepositPercent,
    string? Terms, string? NotesToClient, string? InternalNotes, QuotationItemRequest[]? Items, uint RowVersion);

public sealed record RowVersionRequest(uint RowVersion);

public sealed record AcceptQuotationRequest(DateOnly? AcceptedAt, string? Note, Guid? EvidenceFileId, uint RowVersion);

public sealed record RejectQuotationRequest(DateOnly? RejectedAt, string? Reason, uint RowVersion);

public sealed record PaymentStatusRequest(string? To, DateOnly? Date, string? Reason, uint RowVersion);

// ---------- responses ----------

public sealed record CreateQuotationResponse(Guid Id, string[] Warnings);

public sealed record QuotationListItemDto(
    Guid Id, Guid ProjectId, string ProjectName, string ClientName, string Kind, string? Number, int Version,
    string Title, string Status, string EffectiveStatus, string PaymentStatus, long Total, long DepositAmount,
    long Outstanding, DateOnly? IssueDate, DateOnly? ExpiryDate, DateTimeOffset CreatedAt);

public sealed record QuotationItemDto(
    Guid Id, string Description, string Details, int Quantity, long UnitPrice, long Amount,
    string SourceType, Guid? SourceId, int SortOrder);

/// <summary>docs/05 qrOptions plus the ready-to-render EMVCo payload.</summary>
public sealed record QrOptionDto(string Purpose, string Label, long Amount, string Memo, string Payload);

public sealed record QrPayloadDto(string Purpose, long Amount, string Memo, string Payload, SnapshotBank Bank);

public sealed record QuotationVersionDto(Guid Id, int Version, string Status, string EffectiveStatus);

/// <summary>Plan 09 / BR-48: a NEW_DELIVERABLE item on a line of this quotation and the deliverable it became (null until accepted).</summary>
public sealed record QuotationNewDeliverableDto(
    Guid RevisionItemId, Guid QuotationItemId, string LineDescription, Guid? ServiceId, string? ServiceName,
    Guid? CreatedDeliverableId, string? CreatedDeliverableName);

public sealed record QuotationDetailDto(
    Guid Id, Guid ProjectId, string ProjectName, string ClientName,
    string Kind, string? Number, int Version, Guid? PreviousVersionId,
    string Status, string EffectiveStatus, string PaymentStatus, uint RowVersion,
    string Title, int ValidityDays, long DiscountAmount, int VatPercent, int DepositPercent,
    string Terms, string NotesToClient, string InternalNotes,
    long Subtotal, long VatAmount, long Total, long DepositAmount, long Outstanding,
    DateOnly? IssueDate, DateOnly? ExpiryDate, DateTimeOffset? SentAt,
    DateOnly? AcceptedAt, string? AcceptedNote, DateOnly? RejectedAt, string? RejectedReason,
    DateOnly? DepositPaidAt, DateOnly? PaidAt,
    IReadOnlyList<QuotationItemDto> Items,
    QuotationSnapshot? Snapshot,
    IReadOnlyList<QrOptionDto> QrOptions,
    IReadOnlyList<QuotationVersionDto> Versions,
    IReadOnlyList<QuotationNewDeliverableDto> NewDeliverables);
