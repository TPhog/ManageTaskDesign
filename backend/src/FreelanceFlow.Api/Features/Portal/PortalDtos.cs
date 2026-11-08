using FreelanceFlow.Api.Domain.Quotations;

namespace FreelanceFlow.Api.Features.Portal;

// Portal DTOs are separate from admin DTOs on purpose (07-security §4.4):
// never add internalNotes, notes, scopeOverrideReason or a private status reason here.

public sealed record PortalSessionRequest(string? Token);

public sealed record PortalDesignerDto(string DisplayName, string? LogoUrl, string? ContactPhone, string? ContactEmail);

public sealed record PortalMeDto(string ClientName, PortalDesignerDto Designer);

public sealed record PortalDeliverableSummaryDto(int Total, int Delivered, int Approved);

public sealed record PortalProjectListItemDto(
    Guid Id,
    string Name,
    string DesignType,
    string Status,
    DateOnly? DueDate,
    PortalDeliverableSummaryDto DeliverableSummary,
    bool HasOpenQuotation,
    Guid? OpenQuotationId);

public sealed record PortalDeliverableDto(
    Guid Id,
    string Name,
    int Quantity,
    string Status,
    int IncludedRounds,
    int UsedRounds,
    int RemainingRounds,
    DateOnly? DueDate);

public sealed record PortalBriefDto(int Version, string Content, DateTimeOffset? ConfirmedAt);

public sealed record PortalProjectDetailDto(
    Guid Id,
    string Name,
    string DesignType,
    string Status,
    string? StatusReason,
    DateOnly? StartDate,
    DateOnly? DueDate,
    PortalDeliverableSummaryDto DeliverableSummary,
    IReadOnlyList<PortalDeliverableDto> Deliverables,
    PortalBriefDto? Brief);

public sealed record PortalTimelineItemDto(DateTimeOffset OccurredAt, string Message);

public sealed record PortalPagedDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

// ---------- quotations (docs/05 §4): rendered from the immutable snapshot only (BR-39) ----------

public sealed record PortalQuotationListItemDto(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    string Number,
    int Version,
    string Kind,
    string Title,
    string Status,
    string EffectiveStatus,
    string PaymentStatus,
    long Total,
    long DepositAmount,
    DateOnly IssueDate,
    DateOnly ExpiryDate);

public sealed record PortalQrOptionDto(string Purpose, string Label, long Amount, string Memo, string Payload);

public sealed record PortalQuotationVersionDto(int Version, string Status, string EffectiveStatus);

/// <summary>
/// The snapshot is portal-safe by construction: it carries no internal notes (Domain/Quotations/QuotationSnapshot).
/// </summary>
public sealed record PortalQuotationDetailDto(
    Guid Id,
    Guid ProjectId,
    string Status,
    string EffectiveStatus,
    string PaymentStatus,
    QuotationSnapshot Snapshot,
    IReadOnlyList<PortalQrOptionDto> QrOptions,
    IReadOnlyList<PortalQuotationVersionDto> Versions);

// ---------- requests (docs/05 §4, REQ-01/06) ----------

public sealed record PortalRequestAttachmentDto(Guid Id, string OriginalName, string ContentType);

public sealed record PortalRequestDto(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    Guid? DeliverableId,
    string? DeliverableName,
    DateTimeOffset CreatedAt,
    string Content,
    string Status,
    string? DeclineReason,
    IReadOnlyList<PortalRequestAttachmentDto> Attachments);
