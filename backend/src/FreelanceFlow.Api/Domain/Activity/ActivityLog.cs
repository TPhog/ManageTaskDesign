namespace FreelanceFlow.Api.Domain.Activity;

public class ActivityLog
{
    public long Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string ActorType { get; set; } = "";
    public Guid? ClientId { get; set; }
    public Guid? ProjectId { get; set; }
    public string EntityType { get; set; } = "";
    public Guid EntityId { get; set; }
    public string Action { get; set; } = "";
    public string Data { get; set; } = "{}";
    public bool VisibleToClient { get; set; }
    public string? ClientMessage { get; set; }
}
