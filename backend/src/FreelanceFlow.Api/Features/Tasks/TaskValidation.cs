using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Tasks;

namespace FreelanceFlow.Api.Features.Tasks;

// Request-shape checks → 400 VALIDATION_FAILED with per-field errors (docs/05 §1.2).
// ProjectTask / TaskNote re-check the same invariants and throw DomainException if a caller skips this.
public static class TaskValidation
{
    public const int TitleMaxLength = 200;
    public const int ChecklistTextMaxLength = 500;
    public const int DescriptionMaxLength = ProjectTask.MaxDescription;
    public const int ChecklistDescriptionMaxLength = TaskChecklistItem.MaxDescription;
    public static readonly string[] DueFilters = ["overdue", "today", "week"];

    public static Dictionary<string, string[]> TaskFields(string? title, string? status, string? priority, string? description = null,
        DateOnly? startDate = null, DateOnly? dueDate = null)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(title))
            errors["title"] = ["Vui lòng nhập tiêu đề"];
        else if (title.Trim().Length > TitleMaxLength)
            errors["title"] = [$"Tối đa {TitleMaxLength} ký tự"];
        if (status != null && !ProjectTask.Statuses.Contains(status))
            errors["status"] = ["Trạng thái không hợp lệ"];
        if (priority != null && !ProjectTask.Priorities.Contains(priority))
            errors["priority"] = ["Mức ưu tiên không hợp lệ"];
        if (description is { Length: > DescriptionMaxLength })
            errors["description"] = [$"Mô tả tối đa {DescriptionMaxLength} ký tự"];
        if (startDate is { } s && dueDate is { } d && d < s)
            errors["dueDate"] = [ProjectTask.DueBeforeStartMessage];
        return errors;
    }

    public static Dictionary<string, string[]> Move(string? status, double? sortOrder)
    {
        var errors = new Dictionary<string, string[]>();
        if (status == null || !ProjectTask.Statuses.Contains(status))
            errors["status"] = ["Trạng thái không hợp lệ"];
        if (sortOrder is not { } value || !double.IsFinite(value))
            errors["sortOrder"] = ["Thứ tự không hợp lệ"];
        return errors;
    }

    public static Dictionary<string, string[]> ChecklistText(string? text, string? description = null)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(text))
            errors["text"] = ["Vui lòng nhập nội dung"];
        else if (text.Trim().Length > ChecklistTextMaxLength)
            errors["text"] = [$"Tối đa {ChecklistTextMaxLength} ký tự"];
        if (description is { Length: > ChecklistDescriptionMaxLength })
            errors["description"] = [$"Mô tả tối đa {ChecklistDescriptionMaxLength} ký tự"];
        return errors;
    }

    public static Dictionary<string, string[]> NoteBody(string? body)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(body))
            errors["body"] = ["Vui lòng nhập nội dung ghi chú"];
        else if (body.Trim().Length > TaskNote.MaxBody)
            errors["body"] = [$"Tối đa {TaskNote.MaxBody} ký tự"];
        return errors;
    }

    // ---- P15 ----

    public const string NoRecurrence = "NONE";

    public static bool ClearsRecurrence(RecurrenceDto dto) => dto.Kind is null or NoRecurrence;

    /// <summary>The domain rule for a non-NONE recurrence (call after <see cref="Recurrence"/> passed).</summary>
    public static RecurrenceRule ToRule(RecurrenceDto dto) =>
        RecurrenceRule.Create(dto.Kind!, dto.Weekdays, dto.MonthDay, dto.Until);

    public static IEnumerable<ExternalLink> ToLinks(IEnumerable<ExternalLinkDto> links) =>
        links.Select(l => new ExternalLink(l.Title ?? "", l.Url ?? "", l.VisibleToClient));

    public static Dictionary<string, string[]> Recurrence(RecurrenceDto? dto)
    {
        var errors = new Dictionary<string, string[]>();
        if (dto == null || ClearsRecurrence(dto)) return errors;
        try { ToRule(dto); }
        catch (DomainException ex) { errors["recurrence"] = [ex.Detail ?? ex.Code]; }
        return errors;
    }

    public static Dictionary<string, string[]> Links(IEnumerable<ExternalLinkDto>? links)
    {
        var errors = new Dictionary<string, string[]>();
        if (links == null) return errors;
        try { ExternalLinks.Normalize(ToLinks(links), allowClientVisible: true); }
        catch (DomainException ex) { errors["links"] = [ex.Detail ?? ex.Code]; }
        return errors;
    }

    public static Dictionary<string, string[]> Blocked(BlockedDto? dto)
    {
        var errors = new Dictionary<string, string[]>();
        if (dto?.Reason?.Trim() is { Length: > ProjectTask.MaxBlockedReason })
            errors["blocked.reason"] = [$"Lý do bị chặn tối đa {ProjectTask.MaxBlockedReason} ký tự"];
        return errors;
    }

    public static Dictionary<string, string[]> ListFilters(string? status, string? due)
    {
        var errors = new Dictionary<string, string[]>();
        if (status != null && !ProjectTask.Statuses.Contains(status))
            errors["status"] = ["Trạng thái không hợp lệ"];
        if (due != null && !DueFilters.Contains(due))
            errors["due"] = ["Bộ lọc hạn không hợp lệ (overdue | today | week)"];
        return errors;
    }
}
