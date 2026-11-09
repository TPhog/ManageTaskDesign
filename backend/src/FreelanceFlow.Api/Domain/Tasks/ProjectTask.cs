using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.Api.Domain.Tasks;

public class ProjectTask : IAuditable
{
    // Kanban column order (docs/08 §2.2); BR-80: any status may move to any other
    public static readonly string[] Statuses = ["TODO", "IN_PROGRESS", "REVIEW", "DONE", "BLOCKED"];
    public static readonly string[] Priorities = ["LOW", "MEDIUM", "HIGH", "URGENT"];

    /// <summary>Gap used when a task is appended to a column. sortOrder is a double: inserts take the midpoint (docs/10 M6.1).</summary>
    public const double SortStep = 1024;

    /// <summary>Plan 06b F5: the description is Markdown, rendered without raw HTML on the client.</summary>
    public const int MaxDescription = 20_000;

    /// <summary>P14 DF-06: due date before start date (both set) is rejected.</summary>
    public const string DueBeforeStartMessage = "Hạn hoàn thành phải từ ngày bắt đầu trở đi";

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

    // ---- P15 (Lark-style upgrades) ----
    public const int MaxBlockedReason = 500;

    /// <summary>tasks.links — jsonb array of <see cref="ExternalLink"/>; task links are never client-visible (TSK-05).</summary>
    public string LinksJson { get; private set; } = "[]";
    public string? RecurrenceKind { get; private set; }
    public int? RecurrenceWeekdays { get; private set; }
    public int? RecurrenceMonthDay { get; private set; }
    public DateOnly? RecurrenceUntil { get; private set; }
    /// <summary>Set once the next occurrence was created (or the chain ended): DONE → TODO → DONE never spawns twice.</summary>
    public bool RecurrenceSpawned { get; private set; }
    public Guid? RecurrenceSourceId { get; private set; }
    /// <summary>P15 D7: only kept while BLOCKED; cleared by <see cref="Move"/> when the task leaves BLOCKED.</summary>
    public string? BlockedReason { get; private set; }
    public Guid? BlockedByTaskId { get; private set; }

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
        if (description.Length > MaxDescription)
            throw new DomainException("VALIDATION_FAILED", $"Mô tả tối đa {MaxDescription} ký tự");
        if (startDate is { } s && dueDate is { } d && d < s)
            throw new DomainException("VALIDATION_FAILED", DueBeforeStartMessage);

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
        if (status != "BLOCKED")
        {
            BlockedReason = null;
            BlockedByTaskId = null;
        }
        return true;
    }

    // BR-81: overdue = due date before today (Vietnam calendar day) and not DONE
    public bool IsOverdue(DateOnly todayVn) => DueDate is { } due && due < todayVn && Status != "DONE";

    // "Tuần này" filter: weeks start on Monday, so the week ends on Sunday
    public static DateOnly EndOfWeek(DateOnly todayVn) =>
        todayVn.AddDays(6 - ((int)todayVn.DayOfWeek + 6) % 7);

    public TaskChecklistItem AddChecklistItem(string text, string description = "")
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("VALIDATION_FAILED", "Nội dung checklist không được để trống");
        CheckChecklistDescription(description);

        var item = new TaskChecklistItem
        {
            TaskId = Id,
            Text = text.Trim(),
            Description = description,
            SortOrder = ChecklistItems.Count == 0 ? 0 : ChecklistItems.Max(i => i.SortOrder) + 1,
        };
        ChecklistItems.Add(item);
        return item;
    }

    /// <summary><paramref name="description"/> null = keep the current one (the checkbox toggle does not send it).</summary>
    public void UpdateChecklistItem(Guid itemId, string text, bool done, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("VALIDATION_FAILED", "Nội dung checklist không được để trống");
        if (description != null) CheckChecklistDescription(description);

        var item = FindChecklistItem(itemId);
        item.Text = text.Trim();
        item.Done = done;
        if (description != null) item.Description = description;
    }

    private static void CheckChecklistDescription(string description)
    {
        if (description.Length > TaskChecklistItem.MaxDescription)
            throw new DomainException("VALIDATION_FAILED", $"Mô tả mục checklist tối đa {TaskChecklistItem.MaxDescription} ký tự");
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

    // ---------- P15: links, recurrence, blocked-by, reschedule ----------

    public IReadOnlyList<ExternalLink> Links() => ExternalLinks.Parse(LinksJson);

    public void SetLinks(IEnumerable<ExternalLink>? links) =>
        LinksJson = ExternalLinks.Serialize(ExternalLinks.Normalize(links, allowClientVisible: false));

    public RecurrenceRule? Recurrence() => RecurrenceKind == null
        ? null
        : new RecurrenceRule(RecurrenceKind, RecurrenceWeekdays ?? 0, RecurrenceMonthDay, RecurrenceUntil);

    /// <summary>null clears the rule; <see cref="RecurrenceSpawned"/> is kept.</summary>
    public void SetRecurrence(RecurrenceRule? rule)
    {
        RecurrenceKind = rule?.Kind;
        RecurrenceWeekdays = rule?.Weekdays;
        RecurrenceMonthDay = rule?.MonthDay;
        RecurrenceUntil = rule?.Until;
    }

    public void SetBlocked(string? reason, Guid? byTaskId)
    {
        if (Status != "BLOCKED")
            throw new DomainException("VALIDATION_FAILED", "Chỉ ghi lý do khi công việc đang bị chặn");
        var trimmed = reason?.Trim();
        if (trimmed is { Length: > MaxBlockedReason })
            throw new DomainException("VALIDATION_FAILED", $"Lý do bị chặn tối đa {MaxBlockedReason} ký tự");
        BlockedReason = string.IsNullOrEmpty(trimmed) ? null : trimmed;
        BlockedByTaskId = byTaskId;
    }

    /// <summary>
    /// R2 M8: the blocking task was deleted (the FK would only null the link). The task stays BLOCKED - the designer
    /// unblocks it (BR-01) - but without its own reason it would show no cause, so the reason says what happened.
    /// </summary>
    public void BlockerDeleted(string blockerTitle)
    {
        BlockedByTaskId = null;
        BlockedReason ??= $"Việc chặn “{blockerTitle}” đã bị xoá";
    }

    /// <summary>
    /// P15 D4 (BR-01: a consequence of the designer's DONE command). Once per task: base = max(due, today VN); the new
    /// TODO task is due on the rule's next date after base and keeps the start→due gap. Copies title, description,
    /// priority, deliverable, links, rule and the checklist (unchecked); never notes, images, revision item or blocked data.
    /// Returns null (and ends the chain) when the next date is past Until. <paramref name="keepDeliverable"/> false (the
    /// deliverable is DELIVERED/CANCELLED) makes the next occurrence project-level, so a closed deliverable gets no new work.
    /// </summary>
    public ProjectTask? TrySpawnNext(IClock clock, double sortOrder, bool keepDeliverable = true)
    {
        if (Status != "DONE" || RecurrenceSpawned || RecurrenceKind == null) return null;

        var today = clock.TodayVn;
        var baseDate = DueDate is { } d && d > today ? d : today;
        var next = Recurrence()!.NextAfter(baseDate);
        RecurrenceSpawned = true;
        if (next is not { } due) return null;

        DateOnly? start = StartDate != null && DueDate != null
            ? due.AddDays(-(DueDate.Value.DayNumber - StartDate.Value.DayNumber))
            : null;
        var spawned = Create(ProjectId, Title, Description, "TODO", Priority, start, due,
            keepDeliverable ? DeliverableId : null, sortOrder, clock);
        spawned.SetRecurrence(Recurrence());
        spawned.LinksJson = LinksJson;
        spawned.RecurrenceSourceId = Id;
        foreach (var item in ChecklistItems.OrderBy(i => i.SortOrder))
            spawned.AddChecklistItem(item.Text, item.Description);
        return spawned;
    }

    /// <summary>
    /// Calendar drag: moves the due date; a start date moves with it (same gap), or is clamped when there was no due.
    /// A DONE task keeps its dates (INVALID_TRANSITION): its deadline is history.
    /// </summary>
    public void Reschedule(DateOnly newDue)
    {
        if (Status == "DONE")
            throw new DomainException("INVALID_TRANSITION", "Công việc đã xong — không đổi hạn được");
        if (DueDate is { } due && StartDate is { } start)
            StartDate = start.AddDays(newDue.DayNumber - due.DayNumber);
        else if (StartDate is { } onlyStart && newDue < onlyStart)
            StartDate = newDue;
        DueDate = newDue;
    }
}
