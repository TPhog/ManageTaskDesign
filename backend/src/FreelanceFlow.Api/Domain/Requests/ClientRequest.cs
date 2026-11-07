using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.Api.Domain.Requests;

public class ClientRequest : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ProjectId { get; set; }
    public Guid? DeliverableId { get; set; }
    public string Content { get; set; } = "";
    public string Source { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public string Status { get; set; } = "NEW";
    public string? DeclineReason { get; set; }
    public DateTimeOffset? TriagedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
