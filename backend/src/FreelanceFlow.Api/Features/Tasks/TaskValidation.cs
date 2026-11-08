using FreelanceFlow.Api.Domain.Tasks;

namespace FreelanceFlow.Api.Features.Tasks;

// Request-shape checks → 400 VALIDATION_FAILED with per-field errors (docs/05 §1.2).
// ProjectTask re-checks the same invariants and throws DomainException if a caller skips this.
public static class TaskValidation
{
    public const int TitleMaxLength = 200;
    public const int ChecklistTextMaxLength = 500;
    public static readonly string[] DueFilters = ["overdue", "today", "week"];

    public static Dictionary<string, string[]> TaskFields(string? title, string? status, string? priority)
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

    public static Dictionary<string, string[]> ChecklistText(string? text)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(text))
            errors["text"] = ["Vui lòng nhập nội dung"];
        else if (text.Trim().Length > ChecklistTextMaxLength)
            errors["text"] = [$"Tối đa {ChecklistTextMaxLength} ký tự"];
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
