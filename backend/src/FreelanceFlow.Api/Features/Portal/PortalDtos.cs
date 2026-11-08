using FreelanceFlow.Api.Domain.Quotations;

namespace FreelanceFlow.Api.Features.Portal;

// Portal DTOs are separate from admin DTOs on purpose (07-security §4.4):
// never add internalNotes, notes, scopeOverrideReason or a private status reason here.

public sealed record PortalSessionRequest(string? Token);

/// <summary>ResponseTime (Plan 12 CF-13): "Nhà thiết kế thường phản hồi {ResponseTime}".</summary>
public sealed record PortalDesignerDto(string DisplayName, string? LogoUrl, string? ContactPhone, string? ContactEmail, string ResponseTime);

public sealed record PortalMeDto(string ClientName, PortalDesignerDto Designer);

/// <summary>
/// InReview: deliverables in IN_REVIEW (raw status, QA C-06). AwaitingClient (Plan 12 BR-48): those still waiting for the
/// client — not yet approved by the client and without an open change request (ClientReviewStates.AwaitingClient).
/// </summary>
public sealed record PortalDeliverableSummaryDto(int Total, int Delivered, int Approved, int InReview, int AwaitingClient);

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
    DateOnly? DueDate,
    int? ProgressPercent,     // plan 06b: % of tasks DONE (null = no task); task titles never leave the designer side
    string? ClientDecision,   // "APPROVED" once the client pressed "Duyệt" (still IN_REVIEW until the designer confirms)
    string ReviewState,       // Plan 12 BR-48: ClientReviewStates — the one state the client sees
    bool HasOpenChangeRequest);

/// <summary>POST /api/portal/deliverables/{id}/approve.</summary>
public sealed record PortalDeliverableDecisionDto(Guid Id, string Status, string? ClientDecision, DateTimeOffset? ClientDecisionAt);

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

/// <summary>Kind: "MESSAGE" = the designer's progress update, "EVENT" = an automatic status line (QA C-12).</summary>
/// <summary>Plan 08: preview/thumb only — no original, file name, owner id or task title (TSK-05).</summary>
public sealed record PortalImageDto(
    Guid Id,
    string Source,
    Guid? DeliverableId,
    string? DeliverableName,
    int Width,
    int Height,
    DateTimeOffset CreatedAt,
    string ThumbUrl,
    string PreviewUrl);

public sealed record PortalTimelineItemDto(DateTimeOffset OccurredAt, string Message, string Kind, IReadOnlyList<PortalImageDto> Images);

/// <summary>GET /api/portal/studio — public: lets signed-out / expired / bad-link pages show how to reach the studio.</summary>
public sealed record PortalStudioDto(string DisplayName, string? ContactPhone, string? ContactEmail);

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
    DateOnly ExpiryDate,
    DateTimeOffset? ClientAcceptedAt,
    PortalPaymentNoticeDto? ClientPaymentNotice);

/// <summary>Plan 12 BR-77: the client's "Tôi đã chuyển khoản"; Pending = the designer has not marked that payment yet.</summary>
public sealed record PortalPaymentNoticeDto(string Purpose, long Amount, DateTimeOffset At, string? Note, bool Pending);

/// <summary>POST /api/portal/quotations/{id}/payment-notice. Purpose: deposit | full | remaining (BR-75).</summary>
public sealed record PortalPaymentNoticeRequest(string? Purpose, string? Note);

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
    IReadOnlyList<PortalQuotationVersionDto> Versions,
    DateTimeOffset? ClientAcceptedAt,
    PortalPaymentNoticeDto? ClientPaymentNotice);

// ---------- requests (docs/05 §4, REQ-01/06) ----------

public sealed record PortalRequestAttachmentDto(Guid Id, string OriginalName, string ContentType, string? ThumbUrl, string? PreviewUrl);

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

// ---------- revisions (docs/05 §4, REV-07): never internalNotes, scopeOverrideReason, quotation links, tasks or round notes ----------

public sealed record PortalRevisionItemDto(Guid Id, string Description, string Type, string Billing, string WorkStatus);

public sealed record PortalRoundDto(Guid Id, int RoundNo, DateOnly ReceivedAt, IReadOnlyList<PortalRevisionItemDto> Items);

public sealed record PortalDeliverableRevisionsDto(
    Guid Id,
    string Name,
    int IncludedRounds,
    int UsedRounds,
    int RemainingRounds,
    IReadOnlyList<PortalRoundDto> Rounds);

public sealed record PortalRevisionsDto(
    IReadOnlyList<PortalDeliverableRevisionsDto> Deliverables,
    IReadOnlyList<PortalRoundDto> ProjectRounds);
