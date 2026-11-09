using System.Globalization;
using FreelanceFlow.Api.Domain.Intake;
using FreelanceFlow.Api.Domain.RequestTemplates;

namespace FreelanceFlow.Api.Features.Intake;

public sealed record IntakeImageDto(Guid Id, int Width, int Height, long SizeBytes, DateTimeOffset CreatedAt,
    string ThumbUrl, string PreviewUrl, string OriginalUrl);

public sealed record IntakeListItemDto(Guid Id, DateTimeOffset CreatedAt, string Status, string ContactName, string ContactPhone,
    string? ContactEmail, string? TemplateName, string Excerpt, int ImageCount);

public sealed record IntakeMatchingClientDto(Guid Id, string Name, string? Phone, string? Email);

public sealed record IntakeDetailDto(
    Guid Id, DateTimeOffset CreatedAt, string Status, string ContactName, string ContactPhone, string? ContactEmail,
    string? TemplateName, string Excerpt, int ImageCount,
    Guid? TemplateId, IReadOnlyList<TemplateAnswer> Answers, string Note, string Body, IReadOnlyList<IntakeImageDto> Images,
    string SuggestedDesignType, Guid? SuggestedServiceId, string? SuggestedServiceName, DateOnly? SuggestedDueDate,
    string? RejectReason, DateTimeOffset? RejectedAt, Guid? ConvertedClientId, Guid? ConvertedProjectId, DateTimeOffset? ConvertedAt,
    IReadOnlyList<IntakeMatchingClientDto> MatchingClients, uint RowVersion);

public sealed record ConvertIntakeBody(Guid? ExistingClientId, string? ClientName, string? ProjectName, string? DesignType,
    DateOnly? DueDate, Guid? ServiceId, string? DeliverableName, uint? RowVersion);

public sealed record ConvertIntakeResultDto(Guid ClientId, Guid ProjectId, Guid? DeliverableId);

public sealed record RejectIntakeBody(string? Reason, uint? RowVersion);

public static class IntakeDtos
{
    /// <summary>Client / project / deliverable names typed in the convert dialog.</summary>
    public const int MaxName = 200;
    private const int ExcerptLength = 160;

    public static string Excerpt(string body)
    {
        var oneLine = string.Join(' ', body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return oneLine.Length > ExcerptLength ? oneLine[..ExcerptLength] + "…" : oneLine;
    }

    /// <summary>The "deadline" answer if any, else the first DATE answer (display "dd/MM/yyyy").</summary>
    public static DateOnly? SuggestedDueDate(IReadOnlyList<TemplateAnswer> answers)
    {
        var date = answers.FirstOrDefault(a => a.Type == TemplateFieldTypes.Date && a.Key == "deadline")
                   ?? answers.FirstOrDefault(a => a.Type == TemplateFieldTypes.Date);
        return date is not null && DateOnly.TryParseExact(date.Display, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d : null;
    }

    /// <summary>"0909 123 456" = "+84 909.123.456": the last 9 digits of a Vietnamese number match.</summary>
    public static bool SamePhone(string? a, string? b)
    {
        var x = IntakeSubmission.PhoneDigits(a);
        var y = IntakeSubmission.PhoneDigits(b);
        return x.Length >= 9 && y.Length >= 9 && x[^9..] == y[^9..];
    }
}
