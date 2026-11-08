using System.Text.Json;
using System.Text.RegularExpressions;
using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.Api.Domain.RequestTemplates;

/// <summary>BR-130: shape of a template's questions. Throws VALIDATION_FAILED "Câu hỏi {n}: …".</summary>
public static partial class TemplateFieldRules
{
    [GeneratedRegex("^[a-z0-9_]{1,40}$")]
    private static partial Regex KeyPattern();

    public static IReadOnlyList<TemplateField> Normalize(IReadOnlyList<TemplateField?>? fields)
    {
        var list = fields ?? [];
        if (list.Count > TemplateLimits.MaxFields) throw Invalid($"Tối đa {TemplateLimits.MaxFields} câu hỏi mỗi mẫu");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<TemplateField>(list.Count);
        for (var i = 0; i < list.Count; i++)
        {
            var n = i + 1;
            var f = list[i] ?? throw Invalid($"Câu hỏi {n}: trống");
            var key = (f.Key ?? "").Trim();
            if (!KeyPattern().IsMatch(key)) throw Invalid($"Câu hỏi {n}: mã chỉ gồm a-z, 0-9, _ (1–{TemplateLimits.MaxKey} ký tự)");
            if (!keys.Add(key)) throw Invalid($"Câu hỏi {n}: mã \"{key}\" bị trùng");
            var label = (f.Label ?? "").Trim();
            if (label.Length is < 1 or > TemplateLimits.MaxLabel) throw Invalid($"Câu hỏi {n}: tiêu đề dài 1–{TemplateLimits.MaxLabel} ký tự");
            if (!TemplateFieldTypes.All.Contains(f.Type)) throw Invalid($"Câu hỏi {n}: loại câu hỏi không hợp lệ");
            var help = string.IsNullOrWhiteSpace(f.Help) ? null : f.Help.Trim();
            if (help is { Length: > TemplateLimits.MaxHelp }) throw Invalid($"Câu hỏi {n}: gợi ý tối đa {TemplateLimits.MaxHelp} ký tự");

            var options = (f.Options ?? []).Select(o => (o ?? "").Trim()).ToList();
            if (TemplateFieldTypes.WithOptions.Contains(f.Type))
            {
                var min = f.Type == TemplateFieldTypes.Size ? 0 : 1;
                if (options.Count < min || options.Count > TemplateLimits.MaxOptions)
                    throw Invalid($"Câu hỏi {n}: cần {min}–{TemplateLimits.MaxOptions} lựa chọn");
                if (options.Any(o => o.Length is < 1 or > TemplateLimits.MaxOption))
                    throw Invalid($"Câu hỏi {n}: mỗi lựa chọn dài 1–{TemplateLimits.MaxOption} ký tự");
                if (options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Count)
                    throw Invalid($"Câu hỏi {n}: có lựa chọn bị trùng");
            }
            else if (options.Count > 0)
            {
                throw Invalid($"Câu hỏi {n}: loại này không có lựa chọn");
            }
            result.Add(new TemplateField(key, label, f.Type, f.Required, help, options));
        }
        if (result.Count(f => f.Type == TemplateFieldTypes.Images) > 1)
            throw Invalid("Mỗi mẫu chỉ có 1 câu hỏi ảnh đính kèm");
        return result;
    }

    public static string Serialize(IReadOnlyList<TemplateField> fields) => JsonSerializer.Serialize(fields, TemplateJson.Options);

    public static IReadOnlyList<TemplateField> Parse(string json) =>
        JsonSerializer.Deserialize<List<TemplateField>>(json, TemplateJson.Options) ?? [];

    private static DomainException Invalid(string detail) => new("VALIDATION_FAILED", detail);
}
