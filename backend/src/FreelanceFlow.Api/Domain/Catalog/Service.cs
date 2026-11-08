using System.Text.Json;
using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.Api.Domain.Catalog;

public class Service : IAuditable
{
    public const int MaxTaskTemplateSteps = 20;
    public const int MaxTaskTemplateStepLength = 200;

    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public long BasePrice { get; set; }
    public int DefaultIncludedRounds { get; set; }
    public long ExtraRoundPrice { get; set; }
    public bool Active { get; set; } = true;
    /// <summary>services.task_template — jsonb array of step names. Read with <see cref="TaskTemplate"/>, write with <see cref="SetTaskTemplate"/>.</summary>
    public string TaskTemplateJson { get; private set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // NOTE: a method, not a property — a new property would change the EF model.
    public IReadOnlyList<string> TaskTemplate() => ParseTaskTemplate(TaskTemplateJson);

    public static IReadOnlyList<string> ParseTaskTemplate(string json) =>
        JsonSerializer.Deserialize<List<string>>(json) ?? [];

    /// <summary>Plan 06b F3: 0..20 steps, each trimmed to 1..200 characters, kept in the given order.</summary>
    public void SetTaskTemplate(IEnumerable<string?> steps)
    {
        var list = steps.Select(s => (s ?? "").Trim()).ToList();
        if (list.Count > MaxTaskTemplateSteps)
            throw new DomainException("VALIDATION_FAILED", $"Tối đa {MaxTaskTemplateSteps} bước mẫu");
        if (list.Any(s => s.Length is < 1 or > MaxTaskTemplateStepLength))
            throw new DomainException("VALIDATION_FAILED", $"Mỗi bước mẫu dài 1–{MaxTaskTemplateStepLength} ký tự");
        TaskTemplateJson = JsonSerializer.Serialize(list);
    }
}
