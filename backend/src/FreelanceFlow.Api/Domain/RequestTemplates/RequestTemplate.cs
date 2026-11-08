using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Projects;

namespace FreelanceFlow.Api.Domain.RequestTemplates;

/// <summary>Plan 13: a request form the designer edits (Cài đặt › Mẫu yêu cầu). Hard-deleted; snapshots keep old answers.</summary>
public class RequestTemplate : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string DesignType { get; private set; } = ProjectDesignTypes.Other;
    public Guid? ServiceId { get; private set; }
    /// <summary>request_templates.fields (jsonb). Read with <see cref="Fields"/> — a method, so the EF model stays one column.</summary>
    public string FieldsJson { get; private set; } = "[]";
    public bool Active { get; private set; } = true;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public IReadOnlyList<TemplateField> Fields() => TemplateFieldRules.Parse(FieldsJson);

    public static RequestTemplate Create(string? name, string? description, string? designType, Guid? serviceId,
        bool active, IReadOnlyList<TemplateField?>? fields, int sortOrder)
    {
        var template = new RequestTemplate { SortOrder = sortOrder };
        template.Update(name, description, designType, serviceId, active, fields);
        return template;
    }

    public void Update(string? name, string? description, string? designType, Guid? serviceId,
        bool active, IReadOnlyList<TemplateField?>? fields)
    {
        var n = (name ?? "").Trim();
        if (n.Length is < 1 or > TemplateLimits.MaxName) throw Invalid($"Tên mẫu dài 1–{TemplateLimits.MaxName} ký tự");
        var d = (description ?? "").Trim();
        if (d.Length > TemplateLimits.MaxDescription) throw Invalid($"Mô tả tối đa {TemplateLimits.MaxDescription} ký tự");
        if (!ProjectDesignTypes.All.Contains(designType)) throw Invalid("Loại thiết kế không hợp lệ");
        var normalized = TemplateFieldRules.Normalize(fields);
        Name = n;
        Description = d;
        DesignType = designType!;
        ServiceId = serviceId;
        Active = active;
        FieldsJson = TemplateFieldRules.Serialize(normalized);
    }

    private static DomainException Invalid(string detail) => new("VALIDATION_FAILED", detail);
}
