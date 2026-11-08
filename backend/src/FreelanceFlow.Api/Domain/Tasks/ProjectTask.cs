using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.Api.Domain.Tasks;

public class ProjectTask : IAuditable
{
    // Kanban column order (docs/08 §2.2); BR-80: any status may move to any other
    public static readonly string[] Statuses = ["TODO", "IN_PROGRESS", "REVIEW", "DONE", "BLOCKED"];
    public static readonly string[] Priorities = ["LOW", "MEDIUM", "HIGH", "URGENT"];

    /// <summary>Gap used when a task is appended to a column. sortOrder is a double: inserts take the midpoint (docs/10 M6.1).</summary>
    public const double SortStep = 1024;

    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ProjectId { get; set; }
    public Guid? DeliverableId { get; set; }
    public Guid? RevisionItemId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "TODO";
    public string Priority { get; set; } = "MEDIUM";
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public double SortOrder { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<TaskChecklistItem> ChecklistItems { get; set; } = [];

    public static ProjectTask Create(
        Guid projectId,
        string title,
        string description,
        string status,
        string priority,
        DateOnly? startDate,
        DateOnly? dueDate,
        Guid? deliverableId,
        double sortOrder,
        IClock clock)
    {
        var task = new ProjectTask { ProjectId = projectId };
        task.Edit(title, description, priority, startDate, dueDate, deliverableId);
        task.Move(status, sortOrder, clock);
        return task;
    }

    // The handler checks that deliverableId belongs to the same project (docs/04 §3)
    public void Edit(string title, string description, string priority, DateOnly? startDate, DateOnly? dueDate, Guid? deliverableId)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("VALIDATION_FAILED", "Tiêu đề không được để trống");
        if (!Priorities.Contains(priority))
            throw new DomainException("VALIDATION_FAILED", $"Mức ưu tiên không hợp lệ: {priority}");

        Title = title.Trim();
        Description = description;
        Priority = priority;
        StartDate = startDate;
        DueDate = dueDate;
        DeliverableId = deliverableId;
    }

    // BR-80: free movement between all statuses. Always applies sortOrder; returns true when the status changed.
    public bool Move(string status, double sortOrder, IClock clock)
    {
        if (!Statuses.Contains(status))
            throw new DomainException("VALIDATION_FAILED", $"Trạng thái không hợp lệ: {status}");
        if (!double.IsFinite(sortOrder))
            throw new DomainException("VALIDATION_FAILED", "Thứ tự không hợp lệ");

        SortOrder = sortOrder;
        if (status == Status) return false;

        Status = status;
        CompletedAt = status == "DONE" ? clock.UtcNow : null;
        return true;
    }

    // BR-81: overdue = due date before today (Vietnam calendar day) and not DONE
    public bool IsOverdue(DateOnly todayVn) => DueDate is { } due && due < todayVn && Status != "DONE";

    // "Tuần này" filter: weeks start on Monday, so the week ends on Sunday
    public static DateOnly EndOfWeek(DateOnly todayVn) =>
        todayVn.AddDays(6 - ((int)todayVn.DayOfWeek + 6) % 7);

    public TaskChecklistItem AddChecklistItem(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("VALIDATION_FAILED", "Nội dung checklist không được để trống");

        var item = new TaskChecklistItem
        {
            TaskId = Id,
            Text = text.Trim(),
            SortOrder = ChecklistItems.Count == 0 ? 0 : ChecklistItems.Max(i => i.SortOrder) + 1,
        };
        ChecklistItems.Add(item);
        return item;
    }

    public void UpdateChecklistItem(Guid itemId, string text, bool done)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("VALIDATION_FAILED", "Nội dung checklist không được để trống");

        var item = FindChecklistItem(itemId);
        item.Text = text.Trim();
        item.Done = done;
    }

    public TaskChecklistItem RemoveChecklistItem(Guid itemId)
    {
        var item = FindChecklistItem(itemId);
        ChecklistItems.Remove(item);
        return item;
    }

    private TaskChecklistItem FindChecklistItem(Guid itemId) =>
        ChecklistItems.FirstOrDefault(i => i.Id == itemId)
        ?? throw new DomainException("NOT_FOUND", "Không tìm thấy mục checklist");
}
