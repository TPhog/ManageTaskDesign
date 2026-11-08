namespace FreelanceFlow.Api.Features.Revisions;

// ---------- requests (docs/05 §3.7–3.8) ----------

public sealed record RevisionItemBody(string? Description, string? Type, string? Billing);

/// <summary>One round of a triage or of POST …/rounds. ReceivedAt null → today (VN).</summary>
public sealed record RoundBody(Guid? DeliverableId, DateOnly? ReceivedAt, RevisionItemBody[]? Items);

/// <summary>REQ-02, JSON (attachments arrive with the M3 file upload).</summary>
public sealed record CreateRequestBody(string? Content, string? Source, Guid? DeliverableId);

public sealed record TriageBody(RoundBody[]? Rounds, uint RowVersion);

public sealed record DeclineBody(string? Reason, uint RowVersion);

public sealed record SuggestBillingItem(string? Type, string? Billing);

public sealed record SuggestBillingBody(Guid? DeliverableId, SuggestBillingItem[]? Items);

public sealed record UpdateRevisionItemBody(string? Description, string? Type, string? Billing, string? InternalNotes, uint RowVersion);

public sealed record WorkStatusBody(string? To, string? OverrideReason, uint RowVersion);

public sealed record CreateItemTaskBody(string? Title, string? Priority, DateOnly? DueDate);

public sealed record ProgressUpdateBody(string? Message);

// ---------- responses ----------

public sealed record SuggestionDto(string Billing, string Reason);

public sealed record LinkedQuotationDto(Guid Id, string? Number, int Version, string Status, string EffectiveStatus);

public sealed record LinkedTaskDto(Guid Id, string Title, string Status);

/// <summary>SuggestedBilling/SuggestionReason are filled only in the response that created the item.</summary>
public sealed record RevisionItemDto(
    Guid Id, Guid RoundId, string Description, string Type, string Billing, string WorkStatus,
    Guid? QuotationItemId, LinkedQuotationDto? Quotation, string? ScopeOverrideReason, string InternalNotes,
    int SortOrder, uint RowVersion, string? SuggestedBilling, string? SuggestionReason, IReadOnlyList<LinkedTaskDto> Tasks);

public sealed record RoundDto(
    Guid Id, Guid ProjectId, Guid? DeliverableId, string? DeliverableName, int RoundNo, DateOnly ReceivedAt,
    Guid? SourceRequestId, bool Counted, DateTimeOffset CreatedAt, IReadOnlyList<RevisionItemDto> Items);

public sealed record DeliverableRevisionsDto(
    Guid Id, string Name, string Status, int IncludedRounds, int UsedRounds, int RemainingRounds, long ExtraRoundPrice,
    IReadOnlyList<RoundDto> Rounds);

public sealed record ProjectRevisionsDto(Guid ProjectId, IReadOnlyList<DeliverableRevisionsDto> Deliverables, IReadOnlyList<RoundDto> ProjectRounds);

public sealed record RequestAttachmentDto(Guid Id, string OriginalName, string ContentType);

public sealed record RequestDto(
    Guid Id, Guid ProjectId, string ProjectName, Guid ClientId, string ClientName,
    Guid? DeliverableId, string? DeliverableName, string Content, string Source, string CreatedBy,
    string Status, string? DeclineReason, DateTimeOffset? TriagedAt, DateTimeOffset CreatedAt, uint RowVersion,
    IReadOnlyList<RequestAttachmentDto> Attachments);

public sealed record RequestDetailDto(RequestDto Request, IReadOnlyList<RoundDto> Rounds);

public sealed record NewRequestCountDto(int Count);

public sealed record ProgressUpdateDto(DateTimeOffset OccurredAt, string Message);
