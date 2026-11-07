using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.Api.Domain.Requests;

public class RevisionItem : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid RoundId { get; set; }
    public string Description { get; set; } = "";
    public string Type { get; set; } = "";
    public string Billing { get; set; } = "";
    public string WorkStatus { get; set; } = "TODO";
    public Guid? QuotationItemId { get; set; }
    public string? ScopeOverrideReason { get; set; }
    public string InternalNotes { get; set; } = "";
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
