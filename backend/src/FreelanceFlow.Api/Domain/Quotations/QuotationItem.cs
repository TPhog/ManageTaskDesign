namespace FreelanceFlow.Api.Domain.Quotations;

public class QuotationItem
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid QuotationId { get; set; }
    public string Description { get; set; } = "";
    public string Details { get; set; } = "";
    public int Quantity { get; set; }
    public long UnitPrice { get; set; }
    public long Amount { get; set; }
    public string SourceType { get; set; } = "MANUAL";
    public Guid? SourceId { get; set; }
    public Guid? CopiedFromItemId { get; set; }
    public int SortOrder { get; set; }
}
