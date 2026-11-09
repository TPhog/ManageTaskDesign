namespace FreelanceFlow.Api.Domain.Tasks;

/// <summary>The minimum of a task the list group-by needs (P15 D3).</summary>
public sealed record TaskGroupRow(Guid Id, Guid ProjectId, string ProjectName, string ClientName,
    Guid? DeliverableId, string? DeliverableName, string Priority, string Status, DateOnly? DueDate,
    double SortOrder, DateTimeOffset CreatedAt);

public sealed record TaskGroup(string Key, string Label, int Count);

/// <summary>
/// P15 D3: the single source of truth for group keys, labels and order of GET /api/admin/tasks?groupBy=. The whole
/// filtered set is ordered here before paging, so a page never splits a group out of order.
/// </summary>
public static class TaskGrouping
{
    public static readonly string[] GroupBys = ["project", "deliverable", "due", "priority", "status"];

    public const string NoDeliverableKey = "none";

    private static readonly string[] DueKeys = ["overdue", "today", "week", "later", "none", "done"];
    private static readonly string[] PriorityOrder = ["URGENT", "HIGH", "MEDIUM", "LOW"];

    private static readonly Dictionary<string, string> DueLabels = new()
    {
        ["overdue"] = "Quá hạn", ["today"] = "Hôm nay", ["week"] = "Tuần này",
        ["later"] = "Sau", ["none"] = "Chưa có hạn", ["done"] = "Đã xong",
    };
    private static readonly Dictionary<string, string> PriorityLabels = new()
    {
        ["URGENT"] = "Khẩn cấp", ["HIGH"] = "Cao", ["MEDIUM"] = "Trung bình", ["LOW"] = "Thấp",
    };
    private static readonly Dictionary<string, string> StatusLabels = new()
    {
        ["TODO"] = "Cần làm", ["IN_PROGRESS"] = "Đang làm", ["REVIEW"] = "Chờ duyệt", ["DONE"] = "Xong", ["BLOCKED"] = "Bị chặn",
    };

    public static string KeyOf(string groupBy, TaskGroupRow row, DateOnly today, DateOnly weekEnd) => groupBy switch
    {
        "project" => row.ProjectId.ToString(),
        "deliverable" => row.DeliverableId?.ToString() ?? NoDeliverableKey,
        "due" => row.Status == "DONE" ? "done"
            : row.DueDate is not { } due ? "none"
            : due < today ? "overdue"
            : due == today ? "today"
            : due <= weekEnd ? "week"
            : "later",
        "priority" => row.Priority,
        "status" => row.Status,
        _ => throw new ArgumentOutOfRangeException(nameof(groupBy), groupBy, null),
    };

    public static (List<Guid> OrderedIds, List<TaskGroup> Groups) Order(
        string groupBy, IEnumerable<TaskGroupRow> rows, DateOnly today, DateOnly weekEnd)
    {
        var keyed = rows.Select(r => (Row: r, Key: KeyOf(groupBy, r, today, weekEnd))).ToList();

        // Rank of the group, then the list's usual order inside it (due date, nulls last → sort order → created)
        var ordered = keyed
            .OrderBy(x => x, new GroupComparer(groupBy))
            .ThenBy(x => x.Row.DueDate == null)
            .ThenBy(x => x.Row.DueDate)
            .ThenBy(x => x.Row.SortOrder)
            .ThenBy(x => x.Row.CreatedAt)
            .ThenBy(x => x.Row.Id)
            .ToList();

        var groups = new List<TaskGroup>();
        foreach (var x in ordered)
        {
            if (groups.Count > 0 && groups[^1].Key == x.Key)
                groups[^1] = groups[^1] with { Count = groups[^1].Count + 1 };
            else
                groups.Add(new TaskGroup(x.Key, LabelOf(groupBy, x.Row, x.Key), 1));
        }
        return (ordered.Select(x => x.Row.Id).ToList(), groups);
    }

    private static string LabelOf(string groupBy, TaskGroupRow row, string key) => groupBy switch
    {
        "project" => $"{row.ProjectName} · {row.ClientName}",
        "deliverable" => row.DeliverableId == null ? "Không gắn hạng mục" : $"{row.ProjectName} · {row.DeliverableName}",
        "due" => DueLabels[key],
        "priority" => PriorityLabels.GetValueOrDefault(key, key),
        "status" => StatusLabels.GetValueOrDefault(key, key),
        _ => key,
    };

    private sealed class GroupComparer(string groupBy) : IComparer<(TaskGroupRow Row, string Key)>
    {
        public int Compare((TaskGroupRow Row, string Key) a, (TaskGroupRow Row, string Key) b)
        {
            switch (groupBy)
            {
                case "project":
                    return CompareProject(a.Row, b.Row);
                case "deliverable":
                    // One "Không gắn hạng mục" group, after every deliverable group (keys stay unique across projects)
                    var aNone = a.Row.DeliverableId == null;
                    var bNone = b.Row.DeliverableId == null;
                    if (aNone || bNone) return aNone.CompareTo(bNone);
                    var byProject = CompareProject(a.Row, b.Row);
                    if (byProject != 0) return byProject;
                    var byName = string.Compare(a.Row.DeliverableName, b.Row.DeliverableName, StringComparison.InvariantCultureIgnoreCase);
                    return byName != 0 ? byName : a.Row.DeliverableId!.Value.CompareTo(b.Row.DeliverableId!.Value);
                case "due":
                    return Array.IndexOf(DueKeys, a.Key).CompareTo(Array.IndexOf(DueKeys, b.Key));
                case "priority":
                    return Array.IndexOf(PriorityOrder, a.Key).CompareTo(Array.IndexOf(PriorityOrder, b.Key));
                case "status":
                    return Array.IndexOf(ProjectTask.Statuses, a.Key).CompareTo(Array.IndexOf(ProjectTask.Statuses, b.Key));
                default:
                    return 0;
            }
        }

        private static int CompareProject(TaskGroupRow a, TaskGroupRow b)
        {
            var byName = string.Compare(a.ProjectName, b.ProjectName, StringComparison.InvariantCultureIgnoreCase);
            return byName != 0 ? byName : a.ProjectId.CompareTo(b.ProjectId);
        }
    }
}
