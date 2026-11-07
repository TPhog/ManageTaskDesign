using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.Api.Domain.Tasks;

public class ProjectTask : IAuditable
{
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
}
