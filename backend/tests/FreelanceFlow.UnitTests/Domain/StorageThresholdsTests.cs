using FluentAssertions;
using FreelanceFlow.Api.Domain.Files;

namespace FreelanceFlow.UnitTests.Domain;

public class StorageThresholdsTests
{
    private const long Quota = 10_000;
    private static IReadOnlySet<int> Active(params int[] t) => t.ToHashSet();

    [Fact]
    public void Nothing_is_raised_below_50_percent() =>
        StorageThresholds.Evaluate(4_999, Quota, Active()).Should().BeEmpty();

    [Fact]
    public void Each_threshold_is_raised_once_when_crossed_upwards()
    {
        StorageThresholds.Evaluate(5_000, Quota, Active()).Should().Equal(new ThresholdChange(50, true));
        StorageThresholds.Evaluate(9_000, Quota, Active()).Should().Equal(
            new ThresholdChange(50, true), new ThresholdChange(70, true),
            new ThresholdChange(85, true), new ThresholdChange(90, true));
        StorageThresholds.Evaluate(7_100, Quota, Active(50, 70)).Should().BeEmpty();
    }

    [Theory]
    [InlineData(6_900)]   // 69 % ≥ 68 % → still active (hysteresis 2 points)
    [InlineData(6_800)]   // exactly 68 % → still active
    public void Active_threshold_survives_inside_the_hysteresis_band(long used) =>
        StorageThresholds.Evaluate(used, Quota, Active(50, 70)).Should().BeEmpty();

    [Fact]
    public void Threshold_clears_only_below_threshold_minus_2()
    {
        StorageThresholds.Evaluate(6_799, Quota, Active(50, 70)).Should().Equal(new ThresholdChange(70, false));
        StorageThresholds.Evaluate(8_700, Quota, Active(50, 70, 85, 90)).Should().Equal(new ThresholdChange(90, false));
        StorageThresholds.Evaluate(0, Quota, Active(50, 70)).Should().Equal(
            new ThresholdChange(50, false), new ThresholdChange(70, false));
    }

    [Theory]
    [InlineData(6_999, "OK", false)]
    [InlineData(7_000, "NOTICE", false)]
    [InlineData(8_500, "WARNING", false)]
    [InlineData(8_999, "WARNING", false)]
    [InlineData(9_000, "BLOCKED", true)]
    [InlineData(9_500, "CRITICAL", true)]
    public void Level_and_upload_block_follow_the_current_usage(long used, string level, bool blocked)
    {
        StorageThresholds.Level(used, Quota).Should().Be(level);
        StorageThresholds.UploadsBlocked(used, Quota).Should().Be(blocked);
    }

    [Fact]
    public void Percent_has_one_decimal_and_a_zero_quota_counts_as_full()
    {
        StorageThresholds.Percent(7_050, Quota).Should().Be(70.5);
        StorageThresholds.Percent(1, 3).Should().Be(33.3);
        StorageThresholds.BasisPoints(10_737_418_240, 10_737_418_240).Should().Be(10_000);
        StorageThresholds.UploadsBlocked(0, 0).Should().BeTrue();
    }
}
