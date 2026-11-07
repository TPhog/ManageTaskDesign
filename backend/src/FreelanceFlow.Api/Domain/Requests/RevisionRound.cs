using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.Api.Domain.Requests;

public class RevisionRound : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ProjectId { get; set; }
    public Guid? DeliverableId { get; set; }
    public int RoundNo { get; set; }
    public Guid? SourceRequestId { get; set; }
    public DateOnly ReceivedAt { get; set; }
    public string Notes { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
