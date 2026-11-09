using System.Text.RegularExpressions;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.RequestTemplates;

namespace FreelanceFlow.Api.Domain.Intake;

public static class IntakeStatuses
{
    public const string New = "NEW";
    public const string Converted = "CONVERTED";
    public const string Rejected = "REJECTED";
    public static readonly string[] All = [New, Converted, Rejected];
}

/// <summary>
/// Plan 13: what a NEW client sent through the public form /f/{slug} (BR-134). Nothing happens on its own (BR-01):
/// the designer converts it into client + project + brief + deliverable, rejects it or deletes it.
/// </summary>
public partial class IntakeSubmission : IAuditable
{
    public const int MaxContactName = 100;
    public const int MinPhoneDigits = 8;
    public const int MaxPhone = 20;
    public const int MaxEmail = 200;
    public const int MaxBody = 10_000;
    public const int MaxRejectReason = 500;
    /// <summary>BR-137: submissions accepted per Vietnamese calendar day for the whole studio.</summary>
    public const int DailyLimit = 30;
    /// <summary>BR-135: reference photos per submission.</summary>
    public const int MaxImages = 3;

    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid? TemplateId { get; private set; }
    public string? TemplateName { get; private set; }
    public string ContactName { get; private set; } = "";
    public string ContactPhone { get; private set; } = "";
    public string? ContactEmail { get; private set; }
    public string AnswersJson { get; private set; } = "[]";
    public string Note { get; private set; } = "";
    public string Body { get; private set; } = "";
    public string Status { get; private set; } = IntakeStatuses.New;
    public string? RejectReason { get; private set; }
    public Guid? ConvertedClientId { get; private set; }
    public Guid? ConvertedProjectId { get; private set; }
    public DateTimeOffset? ConvertedAt { get; private set; }
    public DateTimeOffset? RejectedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    [GeneratedRegex(@"^[0-9+ .()\-]+$")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    public IReadOnlyList<TemplateAnswer> Answers() => TemplateAnswers.ParseAnswers(AnswersJson);

    /// <summary>Field → message (empty = valid); the endpoint returns these as a ValidationProblem.</summary>
    public static Dictionary<string, string> ValidateContact(string? name, string? phone, string? email)
    {
        var errors = new Dictionary<string, string>();
        var n = (name ?? "").Trim();
        if (n.Length is < 1 or > MaxContactName) errors["contactName"] = $"Nhập tên của bạn (tối đa {MaxContactName} ký tự)";
        var p = (phone ?? "").Trim();
        if (p.Length > MaxPhone || !PhonePattern().IsMatch(p) || PhoneDigits(p).Length < MinPhoneDigits)
            errors["contactPhone"] = "Nhập số điện thoại / Zalo hợp lệ";
        var e = (email ?? "").Trim();
        if (e.Length > 0 && (e.Length > MaxEmail || !EmailPattern().IsMatch(e))) errors["contactEmail"] = "Email không hợp lệ";
        return errors;
    }

    /// <summary>"+84 909.123-456" → "84909123456" (duplicate-client lookup, BR-136).</summary>
    public static string PhoneDigits(string? phone) => new((phone ?? "").Where(char.IsAsciiDigit).ToArray());

    public static IntakeSubmission Create(string? contactName, string? contactPhone, string? contactEmail,
        Guid? templateId, string? templateName, IReadOnlyList<TemplateAnswer> answers, string? note)
    {
        if (ValidateContact(contactName, contactPhone, contactEmail).Count > 0) throw Invalid("Thông tin liên hệ không hợp lệ");
        var text = (note ?? "").Replace("\r\n", "\n").Trim();
        if (text.Length > TemplateLimits.MaxNote) throw Invalid($"Mô tả tối đa {TemplateLimits.MaxNote} ký tự");
        if (templateId is null && text.Length == 0) throw Invalid("Mô tả yêu cầu của bạn");
        if (templateId is null && answers.Count > 0) throw Invalid("Câu trả lời cần một mẫu yêu cầu");
        if (templateId is not null && string.IsNullOrWhiteSpace(templateName)) throw Invalid("Thiếu tên mẫu yêu cầu");
        var body = TemplateRenderer.Render(templateName, answers, text);
        if (body.Length is < 1 or > MaxBody) throw Invalid("Nội dung quá dài — vui lòng rút gọn câu trả lời");
        var email = (contactEmail ?? "").Trim();
        return new IntakeSubmission
        {
            TemplateId = templateId,
            TemplateName = templateName?.Trim(),
            ContactName = contactName!.Trim(),
            ContactPhone = contactPhone!.Trim(),
            ContactEmail = email.Length == 0 ? null : email,
            AnswersJson = TemplateAnswers.SerializeAnswers(answers),
            Note = text,
            Body = body,
        };
    }

    /// <summary>BR-136: the designer pressed "Tạo khách & dự án"; the caller creates the rows in the same SaveChanges.</summary>
    public void Convert(Guid clientId, Guid projectId, DateTimeOffset now)
    {
        EnsureNew(IntakeStatuses.Converted);
        Status = IntakeStatuses.Converted;
        ConvertedClientId = clientId;
        ConvertedProjectId = projectId;
        ConvertedAt = now;
    }

    public void Reject(string? reason, DateTimeOffset now)
    {
        EnsureNew(IntakeStatuses.Rejected);
        var text = reason?.Trim() ?? "";
        if (text.Length > MaxRejectReason) throw Invalid($"Lý do tối đa {MaxRejectReason} ký tự");
        Status = IntakeStatuses.Rejected;
        RejectReason = text.Length == 0 ? null : text;
        RejectedAt = now;
    }

    /// <summary>A converted submission is the origin of a project and stays.</summary>
    public void EnsureDeletable()
    {
        if (Status == IntakeStatuses.Converted)
            throw new DomainException("INVALID_TRANSITION", "Hồ sơ đã chuyển thành dự án — không xoá được");
    }

    private void EnsureNew(string to)
    {
        if (Status != IntakeStatuses.New) throw new DomainException("INVALID_TRANSITION", $"{Status} → {to}");
    }

    private static DomainException Invalid(string detail) => new("VALIDATION_FAILED", detail);
}
