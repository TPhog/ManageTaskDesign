using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.Api.Domain.Settings;

public class Settings
{
    public short Id { get; set; } = 1;
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

    public Bank? Bank { get; set; }
}
