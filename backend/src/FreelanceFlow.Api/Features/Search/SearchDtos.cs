namespace FreelanceFlow.Api.Features.Search;

public sealed record SearchResultDto(
    IReadOnlyList<SearchClientDto> Clients,
    IReadOnlyList<SearchProjectDto> Projects,
    IReadOnlyList<SearchQuotationDto> Quotations)
{
    public static readonly SearchResultDto Empty = new([], [], []);
}

public sealed record SearchClientDto(Guid Id, string Name, string? Company, string? Phone, string Status);

public sealed record SearchProjectDto(Guid Id, string Name, string ClientName, string Status, DateOnly? DueDate);

public sealed record SearchQuotationDto(
    Guid Id, Guid ProjectId, string? Number, int Version, string Title,
    string ProjectName, string ClientName, string EffectiveStatus, long Total);
