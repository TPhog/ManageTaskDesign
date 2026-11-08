using FluentAssertions;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Settings;

namespace FreelanceFlow.UnitTests.Domain;

public class WatermarkSettingsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Defaults_use_the_studio_name()
    {
        var s = new Settings { DisplayName = " Studio Mây " };
        s.WatermarkEnabled.Should().BeTrue();
        s.WatermarkOpacity.Should().Be(18);
        s.EffectiveWatermarkText.Should().Be("Studio Mây · Bản xem trước");
        new Settings { DisplayName = "" }.EffectiveWatermarkText.Should().Be("FreelanceFlow · Bản xem trước");
    }

    [Fact]
    public void Custom_text_is_trimmed_and_blank_falls_back_to_default()
    {
        var s = new Settings { DisplayName = "Studio Mây" };
        s.SetWatermark(true, "  Chỉ để duyệt  ", 30, Now);
        s.WatermarkText.Should().Be("Chỉ để duyệt");
        s.EffectiveWatermarkText.Should().Be("Chỉ để duyệt");
        s.WatermarkOpacity.Should().Be(30);
        s.UpdatedAt.Should().Be(Now);

        s.SetWatermark(false, "   ", 10, Now);
        s.WatermarkEnabled.Should().BeFalse();
        s.WatermarkText.Should().BeNull();
        s.EffectiveWatermarkText.Should().Be("Studio Mây · Bản xem trước");
    }

    [Theory]
    [InlineData(9)]
    [InlineData(41)]
    public void Opacity_outside_10_to_40_is_refused(int opacity)
    {
        var act = () => new Settings().SetWatermark(true, null, opacity, Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public void Text_over_100_chars_is_refused()
    {
        var act = () => new Settings().SetWatermark(true, new string('x', 101), 18, Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("VALIDATION_FAILED");
    }
}
