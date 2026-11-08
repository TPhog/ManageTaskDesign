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

    public static RevisionRound Create(Guid projectId, Guid? deliverableId, int roundNo, DateOnly receivedAt, Guid? sourceRequestId)
    {
        if (roundNo < 1) throw new DomainException("VALIDATION_FAILED", "Số vòng phải từ 1");
        return new RevisionRound
        {
            ProjectId = projectId,
            DeliverableId = deliverableId,
            RoundNo = roundNo,
            ReceivedAt = receivedAt,
            SourceRequestId = sourceRequestId,
        };
    }

    /// <summary>REV-02 + BR-41 (docs/04 §3): a project-level round never holds IN_QUOTA items.</summary>
    public RevisionItem AddItem(string description, string type, string billing, int sortOrder)
    {
        if (DeliverableId is null && billing == RevisionBillings.InQuota)
            throw RevisionItem.ProjectLevelInQuota();
        var item = new RevisionItem { RoundId = Id, SortOrder = sortOrder, WorkStatus = RevisionWorkStatuses.Todo };
        item.SetContent(description, type, billing);
        return item;
    }
}
