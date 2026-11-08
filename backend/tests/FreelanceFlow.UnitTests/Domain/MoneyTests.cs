using FluentAssertions;
using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.UnitTests.Domain;

public class MoneyTests
{
    [Theory]
    [InlineData(1_234_567L, 8, 98_765L)]     // AC-B2: 98.765,36 → 98.765
    [InlineData(12_345L, 10, 1_235L)]        // 1.234,5 → 1.235 (half-up; banker's rounding would give 1.234)
    [InlineData(1_001L, 50, 501L)]           // 500,5 → 501
    [InlineData(1L, 50, 1L)]                 // 0,5 → 1
    [InlineData(2_400_000L, 50, 1_200_000L)] // AC-B1 deposit
    [InlineData(999L, 0, 0L)]
    [InlineData(0L, 20, 0L)]
    [InlineData(2_000_000L, 100, 2_000_000L)]
    public void Percent_rounds_half_up_to_whole_dong(long amount, int percent, long expected) =>
        Money.Percent(amount, percent).Should().Be(expected);
}
