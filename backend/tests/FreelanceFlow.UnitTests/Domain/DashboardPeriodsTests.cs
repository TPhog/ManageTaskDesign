using FluentAssertions;
using FreelanceFlow.Api.Domain.Dashboard;

namespace FreelanceFlow.UnitTests.Domain;

public class DashboardPeriodsTests
{
    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    [Fact]
    public void Month_compares_with_the_same_elapsed_days_of_the_previous_month()
    {
        var w = DashboardPeriods.Resolve(DashboardPeriod.Month, D(2030, 5, 15));
        w.Current.Should().Be(new DateWindow(D(2030, 5, 1), D(2030, 5, 16)));
        w.Previous.Should().Be(new DateWindow(D(2030, 4, 1), D(2030, 4, 16)));
        w.PeriodStart.Should().Be(D(2030, 5, 1));
        w.PeriodEnd.Should().Be(D(2030, 5, 31));
    }

    [Fact]
    public void Previous_window_never_runs_into_the_current_period()
    {
        // 31 elapsed days in March vs a 28-day February: capped at 1 March
        var w = DashboardPeriods.Resolve(DashboardPeriod.Month, D(2030, 3, 31));
        w.Previous.Should().Be(new DateWindow(D(2030, 2, 1), D(2030, 3, 1)));
    }

    [Fact]
    public void Quarter_starts_on_the_first_month_of_its_quarter()
    {
        var w = DashboardPeriods.Resolve(DashboardPeriod.Quarter, D(2030, 5, 15));
        w.PeriodStart.Should().Be(D(2030, 4, 1));
        w.PeriodEnd.Should().Be(D(2030, 6, 30));
        w.Current.Should().Be(new DateWindow(D(2030, 4, 1), D(2030, 5, 16)));
        w.Previous.Should().Be(new DateWindow(D(2030, 1, 1), D(2030, 2, 15)));   // 45 elapsed days
    }

    [Fact]
    public void Year_compares_year_to_date()
    {
        var w = DashboardPeriods.Resolve(DashboardPeriod.Year, D(2030, 5, 15));
        w.Current.Should().Be(new DateWindow(D(2030, 1, 1), D(2030, 5, 16)));
        w.Previous.Should().Be(new DateWindow(D(2029, 1, 1), D(2029, 5, 16)));
    }

    [Theory]
    [InlineData(null, true, DashboardPeriod.Month)]
    [InlineData("quarter", true, DashboardPeriod.Quarter)]
    [InlineData("year", true, DashboardPeriod.Year)]
    [InlineData("week", false, DashboardPeriod.Month)]
    public void TryParse_accepts_only_known_periods(string? value, bool ok, DashboardPeriod expected)
    {
        DashboardPeriods.TryParse(value, out var p).Should().Be(ok);
        if (ok) p.Should().Be(expected);
    }
}
