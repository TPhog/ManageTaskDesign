using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.Api.Domain.Catalog;

public class Service : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public long BasePrice { get; set; }
    public int DefaultIncludedRounds { get; set; }
    public long ExtraRoundPrice { get; set; }
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
