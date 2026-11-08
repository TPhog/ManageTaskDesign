using FluentAssertions;
using FreelanceFlow.Api.Features.Storage;
using FreelanceFlow.Api.Infrastructure.Notifications;

namespace FreelanceFlow.UnitTests.Features;

public class StorageAlertMessagesTests
{
    private const long Quota = 10_737_418_240;

    [Theory]
    [InlineData(70, NotificationSeverity.Warning, "Dung lượng ảnh đã dùng 70%")]
    [InlineData(85, NotificationSeverity.High, "Dung lượng ảnh đã dùng 85%")]
    [InlineData(90, NotificationSeverity.High, "Đã chặn tải ảnh mới — dung lượng 90%")]
    [InlineData(95, NotificationSeverity.Critical, "Khẩn cấp: dung lượng ảnh 95%")]
    public void Each_notified_threshold_has_its_severity_and_title(int threshold, NotificationSeverity severity, string title)
    {
        var n = StorageAlertMessages.For(threshold, Quota * threshold / 100, Quota, "https://app.example.com/");

        n.Title.Should().Be(title);
        n.Severity.Should().Be(severity);
        n.Url.Should().Be("https://app.example.com/admin/settings/storage");
        n.Fields!.Select(f => f.Name).Should().Equal("Đã dùng", "Giới hạn", "Tỷ lệ");
        n.Fields![1].Value.Should().Be("10.00 GB");
        n.Fields![2].Value.Should().Be($"{threshold}.0%");
    }

    [Fact]
    public void Blocking_thresholds_say_uploads_are_stopped() =>
        StorageAlertMessages.For(95, Quota, Quota, null).Description.Should().Contain("đang bị chặn");

    [Fact]
    public void Fifty_percent_is_log_only_and_a_relative_base_url_gives_no_link()
    {
        var act = () => StorageAlertMessages.For(50, 1, 2, null);
        act.Should().Throw<ArgumentOutOfRangeException>();
        StorageAlertMessages.For(70, 7, 10, "").Url.Should().BeNull();
    }

    [Theory]
    [InlineData(10_737_418_240L, "10.00 GB")]
    [InlineData(1_572_864L, "1.5 MB")]
    [InlineData(2_048L, "2 KB")]
    [InlineData(0L, "0 KB")]
    public void Bytes_are_human_readable(long bytes, string text) =>
        StorageAlertMessages.FormatBytes(bytes).Should().Be(text);
}
