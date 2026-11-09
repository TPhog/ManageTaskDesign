namespace FreelanceFlow.Api.Features.Tasks;

// GET /api/admin/tasks item (docs/05 §3.9). Designer-only: tasks never reach the portal (TSK-05).
public record TaskListItemDto(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    string ClientName,
    Guid? DeliverableId,
    string? DeliverableName,
    string Title,
    string Status,
    string Priority,
    DateOnly? StartDate,
    DateOnly? DueDate,
    double SortOrder,
    DateTimeOffset? CompletedAt,
    bool IsOverdue,
    int ChecklistTotal,
    int ChecklistDone,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool HasDescription,   // plan 06b: Kanban card icon
    int NotesCount,
    // P15
    bool IsRecurring,
    int LinksCount,
    string? BlockedReason,
    Guid? BlockedByTaskId,
    string? BlockedByTitle,
    bool BlockedByDone,
    string? GroupKey);

public record ChecklistItemDto(Guid Id, string Text, bool Done, int SortOrder, string Description);

public record TaskDetailDto(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    Guid? DeliverableId,
    string? DeliverableName,
    string Title,
    string Description,
    string Status,
    string Priority,
    DateOnly? StartDate,
    DateOnly? DueDate,
    double SortOrder,
    DateTimeOffset? CompletedAt,
    bool IsOverdue,
    IReadOnlyList<ChecklistItemDto> Checklist,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid? RevisionItemId,   // REV-06: task created from a revision item
    int NotesCount,
    // P15
    RecurrenceDto? Recurrence,
    Guid? RecurrenceSourceId,
    IReadOnlyList<ExternalLinkDto> Links,
    BlockedInfoDto? Blocked,          // null unless BLOCKED
    NextOccurrenceDto? NextOccurrence);

public record CreateTaskRequest(
    string? Title,
    string? Description,
    string? Status,
    string? Priority,
    DateOnly? StartDate,
    DateOnly? DueDate,
    Guid? DeliverableId,
    RecurrenceDto? Recurrence = null,
    List<ExternalLinkDto>? Links = null);

// Status/Priority null = keep current; every other field is replaced.
// P15: Recurrence / Links / Blocked null = keep current (kind "NONE" clears the rule, [] clears the links).
public record UpdateTaskRequest(
    string? Title,
    string? Description,
    string? Status,
    string? Priority,
    DateOnly? StartDate,
    DateOnly? DueDate,
    Guid? DeliverableId,
    RecurrenceDto? Recurrence = null,
    List<ExternalLinkDto>? Links = null,
    BlockedDto? Blocked = null);

public record MoveTaskRequest(string? Status, double? SortOrder, BlockedDto? Blocked = null);

// ---- P15 ----
public record ExternalLinkDto(string? Title, string? Url, bool VisibleToClient = false);

/// <summary>Kind "NONE" (or a null object) = not recurring. Weekdays: mask Mon=1 … Sun=64.</summary>
public record RecurrenceDto(string? Kind, int Weekdays = 0, int? MonthDay = null, DateOnly? Until = null);

public record BlockedDto(string? Reason, Guid? ByTaskId);

public record BlockedInfoDto(string? Reason, Guid? ByTaskId, string? ByTaskTitle, bool ByTaskDone);

public record NextOccurrenceDto(Guid Id, DateOnly? DueDate);

// Description null = keep the current one (the checkbox toggle sends only text + done)
public record ChecklistItemRequest(string? Text, bool? Done, string? Description);

public record TaskNoteRequest(string? Body);

public record TaskNoteDto(Guid Id, string Body, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
