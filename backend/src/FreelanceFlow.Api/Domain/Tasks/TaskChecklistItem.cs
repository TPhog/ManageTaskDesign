namespace FreelanceFlow.Api.Domain.Tasks;

public class TaskChecklistItem
{
    public const int MaxDescription = 5000;

    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TaskId { get; set; }
    public string Text { get; set; } = "";
    public bool Done { get; set; }
    public int SortOrder { get; set; }
    /// <summary>Plan 06b F5: free text under a subtask.</summary>
    public string Description { get; set; } = "";

    public ProjectTask Task { get; set; } = null!;
}
