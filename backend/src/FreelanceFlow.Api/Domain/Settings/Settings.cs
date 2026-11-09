using System.Security.Cryptography;
using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.Api.Domain.Settings;

public class Settings
{
    public short Id { get; set; } = 1;
    /// <summary>InitialCreate seeds this placeholder as display_name; the setup checklist treats it as "not filled in".</summary>
    public const string SeedDisplayName = "FreelanceFlow Studio";

    public string DisplayName { get; set; } = "";
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public Guid? LogoFileId { get; set; }
    public string? BankBin { get; set; }
    public string? BankAccountNo { get; set; }
    public string? BankAccountName { get; set; }
    public string QuotationPrefix { get; set; } = "BG";
    public int DefaultValidityDays { get; set; } = 7;
    public int DefaultDepositPercent { get; set; } = 50;
    public string DefaultTerms { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }

    // Plan 08 - watermark (spec 4)
    public const int MaxWatermarkText = 100;
    public const int MinWatermarkOpacity = 10;
    public const int MaxWatermarkOpacity = 40;

    public bool WatermarkEnabled { get; set; } = true;
    /// <summary>null = default "{DisplayName} · Bản xem trước".</summary>
    public string? WatermarkText { get; set; }
    public int WatermarkOpacity { get; set; } = 18;

    public string EffectiveWatermarkText =>
        WatermarkText is { Length: > 0 } custom
            ? custom
            : $"{(string.IsNullOrWhiteSpace(DisplayName) ? "FreelanceFlow" : DisplayName.Trim())} · Bản xem trước";

    public void SetWatermark(bool enabled, string? text, int opacityPercent, DateTimeOffset now)
    {
        var trimmed = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        if (trimmed is { Length: > MaxWatermarkText })
            throw new DomainException("VALIDATION_FAILED", $"Nội dung watermark tối đa {MaxWatermarkText} ký tự");
        if (opacityPercent is < MinWatermarkOpacity or > MaxWatermarkOpacity)
            throw new DomainException("VALIDATION_FAILED", $"Độ mờ watermark từ {MinWatermarkOpacity} đến {MaxWatermarkOpacity}%");
        WatermarkEnabled = enabled;
        WatermarkText = trimmed;
        WatermarkOpacity = opacityPercent;
        UpdatedAt = now;
    }

    // Plan 11 - daily Discord digest (config only; runtime state lives in dashboard_state)
    public const int MinDigestHour = 0;
    public const int MaxDigestHour = 23;

    public bool DigestEnabled { get; set; } = true;
    public int DigestHourVn { get; set; } = 8;

    public void SetDigest(bool enabled, int hourVn, DateTimeOffset now)
    {
        if (hourVn is < MinDigestHour or > MaxDigestHour)
            throw new DomainException("VALIDATION_FAILED", $"Giờ nhắc việc từ {MinDigestHour} đến {MaxDigestHour}");
        DigestEnabled = enabled;
        DigestHourVn = hourVn;
        UpdatedAt = now;
    }

    // Plan 12 (CF-13): shown to the client after a request — "Nhà thiết kế thường phản hồi {PortalResponseTime}"
    public const string DefaultPortalResponseTime = "trong 24 giờ";
    public const int MaxPortalResponseTime = 60;

    public string PortalResponseTime { get; set; } = DefaultPortalResponseTime;

    /// <summary>null = keep; blank = back to the default.</summary>
    public void SetPortalResponseTime(string? text)
    {
        if (text is null) return;
        var trimmed = text.Trim();
        if (trimmed.Length > MaxPortalResponseTime)
            throw new DomainException("VALIDATION_FAILED", $"Thời gian phản hồi tối đa {MaxPortalResponseTime} ký tự");
        PortalResponseTime = trimmed.Length == 0 ? DefaultPortalResponseTime : trimmed;
    }

    // Plan 13 - public intake form /f/{slug} (BR-133). The slug is shared publicly, so it is stored as is.
    public const int IntakeSlugLength = 22;

    public bool IntakeEnabled { get; set; }
    public string? IntakeSlug { get; set; }

    /// <summary>Turning the form on for the first time creates the slug; off/on keeps the same link.</summary>
    public void SetIntakeEnabled(bool enabled, DateTimeOffset now)
    {
        if (enabled && IntakeSlug is null) IntakeSlug = NewIntakeSlug();
        IntakeEnabled = enabled;
        UpdatedAt = now;
    }

    /// <summary>"Tạo link mới": the old link answers 404 at once (and its form tokens stop validating).</summary>
    public void RegenerateIntakeSlug(DateTimeOffset now)
    {
        IntakeSlug = NewIntakeSlug();
        UpdatedAt = now;
    }

    public static bool IsWellFormedIntakeSlug(string? slug) =>
        slug is { Length: IntakeSlugLength } && slug.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    /// <summary>128 random bits, base64url without padding (22 characters).</summary>
    private static string NewIntakeSlug() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public Bank? Bank { get; set; }
}
