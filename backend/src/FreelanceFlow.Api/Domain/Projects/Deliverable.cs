using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.Api.Domain.Projects;

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
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

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
}
