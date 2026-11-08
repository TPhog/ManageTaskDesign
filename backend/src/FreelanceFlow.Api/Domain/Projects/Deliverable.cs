using FreelanceFlow.Api.Domain.Catalog;
using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.Api.Domain.Projects;

/// <summary>deliverables.client_decision (plan 06b F2): what the client said on the portal, before the designer confirms.</summary>
public static class ClientDecisions
{
    public const string Approved = "APPROVED";
}

public class Deliverable : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ProjectId { get; set; }
    public Guid? ServiceId { get; set; }
    public string Name { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public int IncludedRounds { get; set; }
    public long ExtraRoundPrice { get; set; }
    public string Status { get; set; } = "NOT_STARTED";
    public DateOnly? DueDate { get; set; }
    public int SortOrder { get; set; }
    /// <summary>Only set while IN_REVIEW (portal "Duyệt"); cleared whenever the deliverable leaves IN_REVIEW.</summary>
    public string? ClientDecision { get; set; }
    public DateTimeOffset? ClientDecisionAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Same limit as the deliverable form (zod max 200).</summary>
    public const int MaxName = 200;

    /// <summary>
    /// BR-48: a deliverable born from an accepted NEW_DELIVERABLE quotation line — NOT_STARTED (BR-01), quota copied
    /// from the picked service (none: 0 rounds, 0 ₫), named after the line the client accepted.
    /// </summary>
    public static Deliverable CreateFromAcceptedLine(Guid projectId, string lineDescription, int quantity, Service? service, int sortOrder)
    {
        var name = lineDescription.Trim();
        if (name.Length == 0) throw new DomainException("VALIDATION_FAILED", "Tên hạng mục trống");
        if (name.Length > MaxName) name = name[..(MaxName - 1)] + "…";
        return new Deliverable
        {
            ProjectId = projectId,
            ServiceId = service?.Id,
            Name = name,
            Quantity = Math.Max(1, quantity),
            IncludedRounds = service?.DefaultIncludedRounds ?? 0,
            ExtraRoundPrice = service?.ExtraRoundPrice ?? 0,
            Status = "NOT_STARTED",
            SortOrder = sortOrder,
        };
    }

    // BR-44: reducing below usedRounds is allowed but must warn; returns warning string or null
    public string? ValidateIncludedRoundsChange(int newIncludedRounds, int usedRounds)
    {
        if (newIncludedRounds < usedRounds)
            return $"Số vòng mới ({newIncludedRounds}) thấp hơn số vòng đã dùng ({usedRounds}). Các item cũ không thay đổi (BR-44).";
        return null;
    }

    // Returns warning if transition is invalid
    public string? ValidateStatusTransition(string to)
    {
        // Status values: NOT_STARTED, IN_PROGRESS, IN_REVIEW, APPROVED, DELIVERED, CANCELLED
        var allowed = (Status, to) switch
        {
            ("NOT_STARTED", "IN_PROGRESS") => true,
            ("NOT_STARTED", "CANCELLED") => true,
            ("IN_PROGRESS", "IN_REVIEW") => true,
            ("IN_PROGRESS", "CANCELLED") => true,
            ("IN_REVIEW", "APPROVED") => true,
            ("IN_REVIEW", "IN_PROGRESS") => true,
            ("IN_REVIEW", "CANCELLED") => true,
            ("APPROVED", "DELIVERED") => true,
            ("APPROVED", "IN_PROGRESS") => true,
            ("DELIVERED", "IN_PROGRESS") => true,
            _ => false,
        };

        return allowed ? null : $"Không thể chuyển từ {Status} sang {to}";
    }

    /// <summary>
    /// The one place a deliverable changes status (BR-01: always a designer command). Throws INVALID_TRANSITION
    /// outside the table of <see cref="ValidateStatusTransition"/>; leaving IN_REVIEW drops the client's decision.
    /// </summary>
    public void ChangeStatus(string to)
    {
        var error = ValidateStatusTransition(to);
        if (error != null) throw new DomainException("INVALID_TRANSITION", error);
        if (Status == "IN_REVIEW")
        {
            ClientDecision = null;
            ClientDecisionAt = null;
        }
        Status = to;
    }

    /// <summary>"Bắt đầu dự án": NOT_STARTED → IN_PROGRESS only.</summary>
    public void Start()
    {
        if (Status != "NOT_STARTED")
            throw new DomainException("INVALID_TRANSITION", $"Không thể chuyển từ {Status} sang IN_PROGRESS");
        ChangeStatus("IN_PROGRESS");
    }

    /// <summary>"Gửi duyệt": IN_PROGRESS → IN_REVIEW.</summary>
    public void SubmitForReview() => ChangeStatus("IN_REVIEW");

    /// <summary>
    /// Portal "Duyệt": records the client's opinion only — the status stays IN_REVIEW until the designer confirms (BR-01).
    /// Returns false when the approval was already recorded (nothing changes).
    /// </summary>
    public bool RecordClientApproval(IClock clock)
    {
        if (Status != "IN_REVIEW")
            throw new DomainException("INVALID_TRANSITION", "Hạng mục không ở trạng thái chờ duyệt");
        if (ClientDecision == ClientDecisions.Approved) return false;
        ClientDecision = ClientDecisions.Approved;
        ClientDecisionAt = clock.UtcNow;
        return true;
    }

    /// <summary>Designer confirms: IN_REVIEW → APPROVED, with or without a portal decision.</summary>
    public void ConfirmApproval() => ChangeStatus("APPROVED");
}
