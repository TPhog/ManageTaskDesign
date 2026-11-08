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
    int NotesCount);

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
    int NotesCount);

public record CreateTaskRequest(
    string? Title,
    string? Description,
    string? Status,
    string? Priority,
    DateOnly? StartDate,
    DateOnly? DueDate,
    Guid? DeliverableId);

// Status/Priority null = keep current; every other field is replaced
public record UpdateTaskRequest(
    string? Title,
    string? Description,
    string? Status,
    string? Priority,
    DateOnly? StartDate,
    DateOnly? DueDate,
    Guid? DeliverableId);

public record MoveTaskRequest(string? Status, double? SortOrder);

// Description null = keep the current one (the checkbox toggle sends only text + done)
public record ChecklistItemRequest(string? Text, bool? Done, string? Description);

public record TaskNoteRequest(string? Body);

public record TaskNoteDto(Guid Id, string Body, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
