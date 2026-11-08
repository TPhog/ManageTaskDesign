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

    public const int MaxContent = 5000;
    public const int MaxDeclineReason = 2000;

    /// <summary>REQ-02: the designer records a request received over Zalo/email/phone on the client's behalf.</summary>
    public static ClientRequest CreateByDesigner(Guid projectId, Guid? deliverableId, string content, string source)
    {
        var text = content.Trim();
        if (text.Length is < 1 or > MaxContent) throw Invalid($"Nội dung dài 1–{MaxContent} ký tự");
        if (!RequestSources.Designer.Contains(source)) throw Invalid("Nguồn yêu cầu không hợp lệ");
        return new ClientRequest
        {
            ProjectId = projectId,
            DeliverableId = deliverableId,
            Content = text,
            Source = source,
            CreatedBy = "DESIGNER",
            Status = RequestStatuses.New,
        };
    }

    /// <summary>BR-50/51: only a NEW request is triaged; the caller adds its rounds in the same SaveChanges.</summary>
    public void MarkTriaged(DateTimeOffset now)
    {
        EnsureNew(RequestStatuses.Triaged);
        Status = RequestStatuses.Triaged;
        TriagedAt = now;
    }

    /// <summary>BR-50/52: NEW → DECLINED with a reason the client sees.</summary>
    public void Decline(string? reason)
    {
        EnsureNew(RequestStatuses.Declined);
        var text = reason?.Trim() ?? "";
        if (text.Length is < 1 or > MaxDeclineReason)
            throw Invalid($"Bắt buộc lý do từ chối (tối đa {MaxDeclineReason} ký tự)");
        Status = RequestStatuses.Declined;
        DeclineReason = text;
    }

    private void EnsureNew(string to)
    {
        if (Status != RequestStatuses.New)
            throw new DomainException("INVALID_TRANSITION", $"{Status} → {to} (BR-50)");
    }

    private static DomainException Invalid(string detail) => new("VALIDATION_FAILED", detail);
}
