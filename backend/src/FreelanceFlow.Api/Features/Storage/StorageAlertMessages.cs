using System.Globalization;
using FreelanceFlow.Api.Domain.Files;
using FreelanceFlow.Api.Infrastructure.Notifications;

namespace FreelanceFlow.Api.Features.Storage;

/// <summary>Spec §2/§9: Discord text per threshold. 50 % is log-only and has no message.</summary>
public static class StorageAlertMessages
{
    private const long Gb = 1L << 30;
    private const long Mb = 1L << 20;

    public static Notification For(int threshold, long usedBytes, long quotaBytes, string? publicBaseUrl)
    {
        var (title, severity) = threshold switch
        {
            70 => ("Dung lượng ảnh đã dùng 70%", NotificationSeverity.Warning),
            85 => ("Dung lượng ảnh đã dùng 85%", NotificationSeverity.High),
            90 => ("Đã chặn tải ảnh mới — dung lượng 90%", NotificationSeverity.High),
            95 => ("Khẩn cấp: dung lượng ảnh 95%", NotificationSeverity.Critical),
            _ => throw new ArgumentOutOfRangeException(nameof(threshold), threshold, "Only 70/85/90/95 notify"),
        };
        var description = threshold >= StorageThresholds.BlockUploadsAt
            ? "Tải ảnh mới đang bị chặn. Xoá bớt ảnh trong Cài đặt › Dung lượng để mở lại."
            : "Nên dọn bớt ảnh (ví dụ ảnh của dự án đã hoàn thành) trong Cài đặt › Dung lượng.";
        var url = Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var baseUri)
            ? $"{baseUri.AbsoluteUri.TrimEnd('/')}/admin/settings/storage"
            : null;
        var percent = StorageThresholds.Percent(usedBytes, quotaBytes).ToString("0.0", CultureInfo.InvariantCulture);
        return new Notification(title, description, url, severity,
        [
            new NotificationField("Đã dùng", FormatBytes(usedBytes)),
            new NotificationField("Giới hạn", FormatBytes(quotaBytes)),
            new NotificationField("Tỷ lệ", $"{percent}%"),
        ]);
    }

    public static string FormatBytes(long bytes) => bytes switch
    {
        >= Gb => $"{(bytes / (double)Gb).ToString("0.00", CultureInfo.InvariantCulture)} GB",
        >= Mb => $"{(bytes / (double)Mb).ToString("0.0", CultureInfo.InvariantCulture)} MB",
        _ => $"{Math.Round(bytes / 1024.0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture)} KB",
    };
}
