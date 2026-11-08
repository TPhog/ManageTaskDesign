using System.Globalization;
using System.Text.Json;

namespace FreelanceFlow.Api.Domain.RequestTemplates;

/// <summary>
/// BR-131: the server re-checks every answer (never trusts the form) and turns it into the stored snapshot.
/// Unknown keys are ignored; an unanswered optional question is left out.
/// </summary>
public static class TemplateAnswers
{
    /// <summary>Errors key for a malformed answers JSON (the endpoint maps it to "answers").</summary>
    public const string FormErrorKey = "";

    public static TemplateAnswerResult Evaluate(IReadOnlyList<TemplateField> fields, string? answersJson, int imageCount, DateOnly todayVn)
    {
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(answersJson))
        {
            if (answersJson.Length > TemplateLimits.MaxAnswersJson) return Malformed();
            try
            {
                using var doc = JsonDocument.Parse(answersJson);
                root = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                return Malformed();
            }
            if (root.ValueKind != JsonValueKind.Object) return Malformed();
        }

        var errors = new Dictionary<string, string>(StringComparer.Ordinal);
        var answers = new List<TemplateAnswer>();
        foreach (var field in fields)
        {
            string? display = null;
            string? error = null;
            if (field.Type == TemplateFieldTypes.Images)
                display = imageCount > 0 ? $"{imageCount} ảnh đính kèm" : null;
            else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(field.Key, out var value)
                     && value.ValueKind != JsonValueKind.Null)
                (display, error) = Read(field, value, todayVn);

            if (error is not null) errors[field.Key] = error;
            else if (display is null)
            {
                if (field.Required) errors[field.Key] = field.Type == TemplateFieldTypes.Images ? "Đính kèm ít nhất 1 ảnh" : "Bắt buộc";
            }
            else answers.Add(new TemplateAnswer(field.Key, field.Label, field.Type, display));
        }
        return new TemplateAnswerResult(answers, errors);

        static TemplateAnswerResult Malformed() =>
            new([], new Dictionary<string, string> { [FormErrorKey] = "Câu trả lời không hợp lệ — vui lòng tải lại trang" });
    }

    public static string SerializeAnswers(IReadOnlyList<TemplateAnswer> answers) => JsonSerializer.Serialize(answers, TemplateJson.Options);

    public static IReadOnlyList<TemplateAnswer> ParseAnswers(string json) =>
        JsonSerializer.Deserialize<List<TemplateAnswer>>(json, TemplateJson.Options) ?? [];

    private static (string? Display, string? Error) Read(TemplateField f, JsonElement v, DateOnly today) => f.Type switch
    {
        TemplateFieldTypes.ShortText => Text(v, TemplateLimits.MaxShortText),
        TemplateFieldTypes.LongText => Text(v, TemplateLimits.MaxLongText),
        TemplateFieldTypes.SingleChoice => Single(f, v),
        TemplateFieldTypes.MultiChoice => Multi(f, v),
        TemplateFieldTypes.Size => Size(f, v),
        TemplateFieldTypes.Date => Date(v, today),
        TemplateFieldTypes.Number => Number(v),
        _ => (null, "Loại câu hỏi không hợp lệ"),
    };

    private static (string?, string?) Text(JsonElement v, int max)
    {
        if (v.ValueKind != JsonValueKind.String) return (null, "Câu trả lời không hợp lệ");
        var text = v.GetString()!.Replace("\r\n", "\n").Trim();
        if (text.Length == 0) return (null, null);
        return text.Length > max ? (null, $"Tối đa {max} ký tự") : (text, null);
    }

    private static (string?, string?) Single(TemplateField f, JsonElement v)
    {
        if (v.ValueKind != JsonValueKind.String) return (null, "Lựa chọn không hợp lệ");
        var choice = v.GetString()!.Trim();
        if (choice.Length == 0) return (null, null);
        return f.Options.Contains(choice) ? (choice, null) : (null, "Lựa chọn không hợp lệ");
    }

    private static (string?, string?) Multi(TemplateField f, JsonElement v)
    {
        if (v.ValueKind != JsonValueKind.Array) return (null, "Lựa chọn không hợp lệ");
        var picked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in v.EnumerateArray())
        {
            var choice = item.ValueKind == JsonValueKind.String ? item.GetString()!.Trim() : null;
            if (choice is null || !f.Options.Contains(choice)) return (null, "Lựa chọn không hợp lệ");
            picked.Add(choice);
        }
        return picked.Count == 0 ? (null, null) : (string.Join(", ", f.Options.Where(picked.Contains)), null);
    }

    private static (string?, string?) Size(TemplateField f, JsonElement v)
    {
        const string invalid = "Kích thước không hợp lệ";
        if (v.ValueKind != JsonValueKind.Object) return (null, invalid);
        if (v.TryGetProperty("preset", out var preset))
        {
            if (preset.ValueKind != JsonValueKind.String) return (null, invalid);
            var p = preset.GetString()!.Trim();
            if (p.Length == 0) return (null, null);
            return f.Options.Contains(p) ? (p, null) : (null, invalid);
        }
        var hasW = v.TryGetProperty("width", out var w);
        var hasH = v.TryGetProperty("height", out var h);
        if (!hasW && !hasH) return (null, null);
        if (!hasW || !hasH || w.ValueKind != JsonValueKind.Number || h.ValueKind != JsonValueKind.Number
            || !w.TryGetDecimal(out var width) || !h.TryGetDecimal(out var height)
            || width <= 0 || height <= 0 || width > TemplateLimits.MaxSizeValue || height > TemplateLimits.MaxSizeValue)
            return (null, "Nhập chiều rộng và chiều cao lớn hơn 0");
        var unit = v.TryGetProperty("unit", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() : null;
        if (!SizeUnits.All.Contains(unit)) return (null, "Đơn vị không hợp lệ");
        return ($"{Dec(width)} × {Dec(height)} {unit} (tuỳ chỉnh)", null);
    }

    private static (string?, string?) Date(JsonElement v, DateOnly today)
    {
        if (v.ValueKind != JsonValueKind.String) return (null, "Ngày không hợp lệ");
        var raw = v.GetString()!.Trim();
        if (raw.Length == 0) return (null, null);
        if (!DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return (null, "Ngày không hợp lệ");
        if (date < today) return (null, "Chọn ngày từ hôm nay trở đi");
        return (date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), null);
    }

    private static (string?, string?) Number(JsonElement v)
    {
        if (v.ValueKind != JsonValueKind.Number || !v.TryGetInt64(out var n) || n < 0 || n > TemplateLimits.MaxNumber)
            return (null, $"Nhập số nguyên từ 0 đến {Thousands(TemplateLimits.MaxNumber)}");
        return (Thousands(n), null);
    }

    /// <summary>1500000 → "1.500.000" without ICU (the container may run with invariant globalization).</summary>
    private static string Thousands(long n) => n.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');

    /// <summary>29.7 → "29,7"; 60 → "60".</summary>
    private static string Dec(decimal d) => d.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');
}
