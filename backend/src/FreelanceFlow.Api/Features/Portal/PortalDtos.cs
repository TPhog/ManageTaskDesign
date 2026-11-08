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
    bool HasOpenQuotation);

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
