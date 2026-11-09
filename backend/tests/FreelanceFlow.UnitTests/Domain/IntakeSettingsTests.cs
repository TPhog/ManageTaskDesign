using FluentAssertions;
using SettingsEntity = FreelanceFlow.Api.Domain.Settings.Settings;

namespace FreelanceFlow.UnitTests.Domain;

public class IntakeSettingsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Enabling_creates_one_128_bit_slug_that_survives_off_and_on()   // BR-133
    {
        var s = new SettingsEntity();
        s.SetIntakeEnabled(true, Now);
        var first = s.IntakeSlug;

        first.Should().HaveLength(SettingsEntity.IntakeSlugLength);
        SettingsEntity.IsWellFormedIntakeSlug(first).Should().BeTrue();
        s.SetIntakeEnabled(false, Now);
        s.SetIntakeEnabled(true, Now);
        s.IntakeSlug.Should().Be(first);
        s.IntakeEnabled.Should().BeTrue();
    }

    [Fact]
    public void Regenerate_replaces_the_slug()
    {
        var s = new SettingsEntity();
        s.SetIntakeEnabled(true, Now);
        var first = s.IntakeSlug;

        s.RegenerateIntakeSlug(Now);

        s.IntakeSlug.Should().NotBe(first).And.HaveLength(22);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("short")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaa/")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaa")]
    public void Malformed_slugs_are_refused(string? slug) =>
        SettingsEntity.IsWellFormedIntakeSlug(slug).Should().BeFalse();
}
