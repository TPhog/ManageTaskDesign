using System.Text.Json;
using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.Api.Domain.Tasks;

/// <summary>P15 D3: a saved task list view (task_views). Single designer: no owner column.</summary>
public sealed class TaskView : IAuditable
{
    public const int MaxCount = 20, MaxName = 60;

    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = "";
    public string ConfigJson { get; set; } = "{}";
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>P15 D3: the last view used on the task page — a single row (id = 1) in task_view_state.</summary>
public sealed class TaskViewState
{
    public const short SingletonId = 1;

    public short Id { get; set; } = SingletonId;
    public string ConfigJson { get; set; } = "{}";
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed record TaskViewConfig(string Mode, string GroupBy, string? Due, string? Status, Guid? ClientId,
    string? ClientLabel, Guid? ProjectId, string? ProjectLabel, string? Q, string CalendarUnit)
{
    public static readonly string[] Modes = ["list", "calendar"];
    public static readonly string[] CalendarUnits = ["month", "week"];
    public static readonly string[] DueFilters = ["overdue", "today", "week"];
    public const string NoGrouping = "none";
    public const int MaxQ = 200, MaxLabel = 200;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Checks the enums and lengths, trims text (empty → null); a missing calendarUnit means "month".</summary>
    public static TaskViewConfig Validate(TaskViewConfig? c)
    {
        if (c == null) throw Invalid();
        var q = Clean(c.Q, MaxQ);
        var clientLabel = Clean(c.ClientLabel, MaxLabel);
        var projectLabel = Clean(c.ProjectLabel, MaxLabel);
        var unit = c.CalendarUnit ?? "month";

        if (!Modes.Contains(c.Mode)
            || !(c.GroupBy == NoGrouping || TaskGrouping.GroupBys.Contains(c.GroupBy))
            || (c.Due != null && !DueFilters.Contains(c.Due))
            || (c.Status != null && !ProjectTask.Statuses.Contains(c.Status))
            || !CalendarUnits.Contains(unit))
            throw Invalid();

        return c with { Q = q, ClientLabel = clientLabel, ProjectLabel = projectLabel, CalendarUnit = unit };
    }

    public static TaskViewConfig Parse(string json) =>
        JsonSerializer.Deserialize<TaskViewConfig>(json, Json) ?? throw Invalid();

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    private static string? Clean(string? value, int max)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        if (trimmed.Length > max) throw Invalid();
        return trimmed;
    }

    private static DomainException Invalid() => new("VALIDATION_FAILED", "Cấu hình chế độ xem không hợp lệ");
}
