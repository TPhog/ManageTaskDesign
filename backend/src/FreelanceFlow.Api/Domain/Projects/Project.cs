using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.Api.Domain.Projects;

public class Project : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ClientId { get; set; }
    public string Name { get; set; } = "";
    public string DesignType { get; set; } = "";
    public string Status { get; set; } = "DRAFT";
    public string? StatusReason { get; set; }
    public bool StatusReasonPublic { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public string InternalNotes { get; set; } = "";
    public DateTimeOffset? ArchivedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Validates the requested transition and returns any warnings.
    // Throws DomainException(INVALID_TRANSITION) for disallowed transitions.
    // Also throws for missing required fields (reason on CANCELLED, ON_HOLD, COMPLETED→IN_PROGRESS).
    public string[] ValidateTransition(
        string to,
        string? reason,
        bool hasAcceptedInitialQuotation,
        bool hasIncompleteDeliverables,
        bool hasUnpaidAcceptedQuotations)
    {
        var from = Status;
        var warnings = new List<string>();

        // Validate transition table (BR-20)
        var allowed = (from, to) switch
        {
            ("DRAFT", "QUOTED") => true,
            ("DRAFT", "IN_PROGRESS") => true,
            ("DRAFT", "CANCELLED") => true,
            ("QUOTED", "IN_PROGRESS") => true,
            ("QUOTED", "CANCELLED") => true,
            ("IN_PROGRESS", "ON_HOLD") => true,
            ("IN_PROGRESS", "COMPLETED") => true,
            ("IN_PROGRESS", "CANCELLED") => true,
            ("ON_HOLD", "IN_PROGRESS") => true,
            ("ON_HOLD", "CANCELLED") => true,
            ("COMPLETED", "IN_PROGRESS") => true,
            _ => false,
        };

        if (!allowed)
            throw new DomainException("INVALID_TRANSITION", $"{from} → {to}");

        // Validate required fields
        if (to is "CANCELLED" && string.IsNullOrWhiteSpace(reason))
            throw new DomainException("VALIDATION_FAILED", "Bắt buộc lý do khi hủy dự án");

        if (to is "ON_HOLD" && string.IsNullOrWhiteSpace(reason))
            throw new DomainException("VALIDATION_FAILED", "Bắt buộc lý do tạm dừng (hold_reason)");

        if (to is "IN_PROGRESS" && from is "COMPLETED" && string.IsNullOrWhiteSpace(reason))
            throw new DomainException("VALIDATION_FAILED", "Bắt buộc lý do mở lại dự án");

        // Warnings for DRAFT/QUOTED → IN_PROGRESS
        if (to is "IN_PROGRESS" && from is ("DRAFT" or "QUOTED") && !hasAcceptedInitialQuotation)
            warnings.Add("Chưa có báo giá gốc được khách chấp nhận.");

        // Warnings for IN_PROGRESS → COMPLETED
        if (to is "COMPLETED")
        {
            if (hasIncompleteDeliverables)
                warnings.Add("Còn hạng mục chưa bàn giao (chưa ở trạng thái Đã bàn giao hoặc Đã huỷ).");
            if (hasUnpaidAcceptedQuotations)
                warnings.Add("Còn báo giá đã chấp nhận chưa thu đủ tiền.");
        }

        return [.. warnings];
    }

    // Applies the state change (call only after ValidateTransition + warnings accepted)
    public void ApplyStatusChange(string to, string? reason, bool? reasonPublic)
    {
        Status = to;
        StatusReason = reason;
        StatusReasonPublic = reasonPublic ?? false;
    }

    /// <summary>
    /// "Bắt đầu dự án" (plan 06b): DRAFT/QUOTED → IN_PROGRESS. Warnings never block (F4) and are returned to the caller;
    /// any other status → INVALID_TRANSITION (ON_HOLD / COMPLETED keep using the normal status command).
    /// </summary>
    public string[] Start(bool hasAcceptedInitialQuotation)
    {
        if (Status is not ("DRAFT" or "QUOTED"))
            throw new DomainException("INVALID_TRANSITION", $"{Status} → IN_PROGRESS");
        var warnings = ValidateTransition("IN_PROGRESS", null, hasAcceptedInitialQuotation, false, false);
        ApplyStatusChange("IN_PROGRESS", null, null);
        return warnings;
    }
}
