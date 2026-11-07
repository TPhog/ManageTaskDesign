namespace FreelanceFlow.Api.Domain.Tasks;

public class TaskChecklistItem
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TaskId { get; set; }
    public string Text { get; set; } = "";
    public bool Done { get; set; }
    public int SortOrder { get; set; }

    public ProjectTask Task { get; set; } = null!;
}
