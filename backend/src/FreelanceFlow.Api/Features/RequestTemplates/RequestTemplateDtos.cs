using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.RequestTemplates;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.RequestTemplates;

public sealed record AdminRequestTemplateDto(
    Guid Id, string Name, string Description, string DesignType, Guid? ServiceId, string? ServiceName, bool Active,
    int SortOrder, IReadOnlyList<TemplateField> Fields, uint RowVersion, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

/// <summary>Portal + public form: never serviceId, designType or active (07-security §4.5).</summary>
public sealed record PublicRequestTemplateDto(Guid Id, string Name, string Description, IReadOnlyList<TemplateField> Fields);

public sealed record RequestTemplateBody(
    string? Name, string? Description, string? DesignType, Guid? ServiceId, bool? Active, List<TemplateField?>? Fields, uint? RowVersion);

public sealed record ReorderTemplatesBody(Guid[]? Ids);

public static class RequestTemplateDtos
{
    public static PublicRequestTemplateDto ToPublic(RequestTemplate t) => new(t.Id, t.Name, t.Description, t.Fields());

    /// <summary>Active templates in display order — the portal list and the public form.</summary>
    public static async Task<List<PublicRequestTemplateDto>> ListPublicAsync(AppDbContext db, CancellationToken ct) =>
        (await db.RequestTemplates.AsNoTracking()
            .Where(t => t.Active)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.CreatedAt)
            .ToListAsync(ct))
        .Select(ToPublic).ToList();
}
