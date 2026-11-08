using FluentAssertions;
using FreelanceFlow.Api.Domain.Dashboard;

namespace FreelanceFlow.UnitTests.Domain;

public class DashboardMoneyRulesTests
{
    private static DateOnly D(int m, int d) => new(2030, m, d);
    private static readonly DateWindow May = new(D(5, 1), D(6, 1));

    [Fact]
    public void Deposit_counts_on_its_own_date_and_the_remainder_on_the_paid_date()
    {
        var rows = new[]
        {
            new PaymentRow(D(4, 28), D(5, 10), 5_000_000, 10_000_000),   // deposit in April, rest in May
            new PaymentRow(D(5, 3), null, 2_000_000, 4_000_000),         // deposit in May, still open
        };
        var c = DashboardMoneyRules.Collected(rows, May);
        c.Deposits.Should().Be(2_000_000);
        c.Remainders.Should().Be(5_000_000);
        c.Total.Should().Be(7_000_000);
    }

    [Fact]
    public void Paid_in_full_without_deposit_counts_the_whole_total_on_paid_date()
    {
        var c = DashboardMoneyRules.Collected([new PaymentRow(null, D(5, 20), 0, 3_000_000)], May);
        c.Total.Should().Be(3_000_000);
    }

    [Fact]
    public void End_of_window_is_exclusive()
    {
        DashboardMoneyRules.Collected([new PaymentRow(null, D(6, 1), 0, 1_000)], May).Total.Should().Be(0);
    }

    [Theory]
    [InlineData(150L, 100L, 50.0)]
    [InlineData(50L, 100L, -50.0)]
    [InlineData(100L, 300L, -66.7)]
    public void Delta_is_a_percentage_with_one_decimal(long current, long previous, double expected) =>
        DashboardMoneyRules.DeltaPercent(current, previous).Should().Be(expected);

    [Fact]
    public void Delta_is_null_when_there_is_no_previous_value() =>
        DashboardMoneyRules.DeltaPercent(500, 0).Should().BeNull();

    [Theory]
    [InlineData("PAID", 10_000_000L, 5_000_000L, 0L)]
    [InlineData("DEPOSIT_PAID", 10_000_000L, 5_000_000L, 5_000_000L)]
    [InlineData("UNPAID", 10_000_000L, 5_000_000L, 10_000_000L)]
    public void Outstanding_follows_br73(string status, long total, long deposit, long expected) =>
        DashboardMoneyRules.Outstanding(status, total, deposit).Should().Be(expected);

    [Fact]
    public void Aging_runs_from_the_deposit_once_it_is_paid_otherwise_from_acceptance()
    {
        DashboardMoneyRules.AgingFrom("DEPOSIT_PAID", D(5, 1), D(5, 9)).Should().Be(D(5, 9));
        DashboardMoneyRules.AgingFrom("DEPOSIT_PAID", D(5, 1), null).Should().Be(D(5, 1));
        DashboardMoneyRules.AgingFrom("UNPAID", D(5, 1), D(5, 9)).Should().Be(D(5, 1));
    }

    [Theory]
    [InlineData(0, "0-7")]
    [InlineData(7, "0-7")]
    [InlineData(8, "8-30")]
    [InlineData(30, "8-30")]
    [InlineData(31, "31+")]
    public void Aging_bucket_boundaries(int days, string bucket) =>
        DashboardMoneyRules.AgingBucket(days).Should().Be(bucket);

    [Fact]
    public void Aging_days_never_negative() =>
        DashboardMoneyRules.AgingDays(D(5, 20), D(5, 15)).Should().Be(0);
}
