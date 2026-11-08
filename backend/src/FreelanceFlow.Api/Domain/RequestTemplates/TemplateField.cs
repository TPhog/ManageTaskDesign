using System.Text.Encodings.Web;
using System.Text.Json;

namespace FreelanceFlow.Api.Domain.RequestTemplates;

public static class TemplateFieldTypes
{
    public const string ShortText = "SHORT_TEXT";
    public const string LongText = "LONG_TEXT";
    public const string SingleChoice = "SINGLE_CHOICE";
    public const string MultiChoice = "MULTI_CHOICE";
    public const string Size = "SIZE";
    public const string Date = "DATE";
    public const string Number = "NUMBER";
    public const string Images = "IMAGES";
    public static readonly string[] All = [ShortText, LongText, SingleChoice, MultiChoice, Size, Date, Number, Images];
    /// <summary>Types whose options list is meaningful (SIZE: preset sizes; custom W×H is always offered).</summary>
    public static readonly string[] WithOptions = [SingleChoice, MultiChoice, Size];
}

public static class SizeUnits
{
    public const string Px = "px";
    public const string Mm = "mm";
    public const string Cm = "cm";
    public const string M = "m";
    public static readonly string[] All = [Px, Mm, Cm, M];
}

/// <summary>Plan 13 limits (BR-130/131); mirrored in frontend/src/lib/requestTemplates.ts.</summary>
public static class TemplateLimits
{
    public const int MaxTemplates = 50;
    public const int MaxFields = 30;
    public const int MaxKey = 40;
    public const int MaxLabel = 120;
    public const int MaxHelp = 300;
    public const int MaxOptions = 20;
    public const int MaxOption = 100;
    public const int MaxShortText = 200;
    public const int MaxLongText = 2000;
    public const int MaxNote = 2000;
    public const int MaxName = 100;
    public const int MaxDescription = 500;
    public const int MaxAnswersJson = 64_000;
    public const long MaxNumber = 1_000_000_000_000;
    public const decimal MaxSizeValue = 100_000m;
}

/// <summary>One question of a request template; the array order is the display order.</summary>
public sealed record TemplateField(string Key, string Label, string Type, bool Required, string? Help, IReadOnlyList<string> Options);

/// <summary>Snapshot of one answered question (labels copied, so editing the template later changes nothing).</summary>
public sealed record TemplateAnswer(string Key, string Label, string Type, string Display);

public sealed record TemplateAnswerResult(IReadOnlyList<TemplateAnswer> Answers, IReadOnlyDictionary<string, string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public static class TemplateJson
{
    /// <summary>camelCase like the HTTP API; Vietnamese kept readable in jsonb.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
