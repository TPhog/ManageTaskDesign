using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.Api.Domain.Quotations;

public class Quotation : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ProjectId { get; set; }
    public string Kind { get; set; } = "";
    public string? Number { get; set; }
    public int Version { get; set; } = 1;
    public Guid? PreviousVersionId { get; set; }
    public string Title { get; set; } = "";
    public string Status { get; set; } = "DRAFT";
    public DateOnly? IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public int ValidityDays { get; set; }
    public long Subtotal { get; set; }
    public long DiscountAmount { get; set; }
    public int VatPercent { get; set; }
    public long VatAmount { get; set; }
    public long Total { get; set; }
    public int DepositPercent { get; set; }
    public long DepositAmount { get; set; }
    public string Terms { get; set; } = "";
    public string NotesToClient { get; set; } = "";
    public string InternalNotes { get; set; } = "";
    public string? Snapshot { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateOnly? AcceptedAt { get; set; }
    public string? AcceptedNote { get; set; }
    public Guid? EvidenceFileId { get; set; }
    public DateOnly? RejectedAt { get; set; }
    public string? RejectedReason { get; set; }
    public string PaymentStatus { get; set; } = "UNPAID";
    public DateOnly? DepositPaidAt { get; set; }
    public DateOnly? PaidAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<QuotationItem> Items { get; set; } = [];
}
