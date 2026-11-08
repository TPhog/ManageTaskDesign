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

    /// <summary>BR-54: requests a client may send from the portal per Vietnamese calendar day.</summary>
    public const int DailyClientLimit = 20;

    /// <summary>BR-21/BR-53: project statuses that still accept new requests from the portal.</summary>
    public static readonly string[] OpenProjectStatuses = ["DRAFT", "QUOTED", "IN_PROGRESS", "ON_HOLD"];

    /// <summary>
    /// REQ-01: the client sends a request from the portal (created_by CLIENT => source PORTAL, ck_requests_portal).
    /// <paramref name="sentToday"/> = portal requests this client already sent since <see cref="DayStartUtc"/>.
    /// </summary>
    public static ClientRequest CreateByClient(
        Guid projectId, string projectStatus, Guid? deliverableId, string content, int sentToday)
    {
        var text = content.Trim();
        if (text.Length is < 1 or > MaxContent) throw Invalid($"Nội dung dài 1–{MaxContent} ký tự");
        if (!OpenProjectStatuses.Contains(projectStatus))
            throw new DomainException("BUSINESS_RULE", "Dự án đã hoàn thành hoặc đã huỷ — không gửi yêu cầu mới được");
        if (sentToday >= DailyClientLimit)
            throw new DomainException("RATE_LIMITED", $"Mỗi ngày bạn gửi được tối đa {DailyClientLimit} yêu cầu");
        return new ClientRequest
        {
            ProjectId = projectId,
            DeliverableId = deliverableId,
            Content = text,
            Source = RequestSources.Portal,
            CreatedBy = "CLIENT",
            Status = RequestStatuses.New,
        };
    }

    /// <summary>BR-06/BR-54: 00:00 of <paramref name="todayVn"/> in Asia/Ho_Chi_Minh (UTC+7, no DST) as a UTC instant (Npgsql rejects non-zero offsets).</summary>
    public static DateTimeOffset DayStartUtc(DateOnly todayVn) =>
        new DateTimeOffset(todayVn.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();

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
