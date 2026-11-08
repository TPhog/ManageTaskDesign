using FluentAssertions;
using FreelanceFlow.Api.Domain.Dashboard;

namespace FreelanceFlow.UnitTests.Domain;

public class DashboardRulesTests
{
    private static readonly DateOnly Today = new(2030, 5, 15);

    private static readonly AcceptedAmount[] Accepted =
    [
        new(new DateOnly(2030, 5, 3), "INITIAL", 10_000_000),
        new(new DateOnly(2030, 5, 10), "ADDITIONAL", 1_300_000),
        new(new DateOnly(2030, 4, 20), "INITIAL", 8_000_000),
        new(new DateOnly(2029, 12, 1), "INITIAL", 3_000_000),
        new(new DateOnly(2029, 11, 30), "INITIAL", 999),   // before the 6-month window
        new(new DateOnly(2030, 6, 1), "INITIAL", 7),       // a future date never counts
    ];

    [Fact]
    public void Chart_covers_six_months_ending_with_the_current_one() =>   // DSH-04
        DashboardRules.AcceptedByMonth(Today, []).Select(m => m.Month)
            .Should().Equal("2029-12", "2030-01", "2030-02", "2030-03", "2030-04", "2030-05");

    [Fact]
    public void Chart_crosses_the_year_boundary() =>
        DashboardRules.AcceptedByMonth(new DateOnly(2030, 2, 28), []).Select(m => m.Month)
            .Should().Equal("2029-09", "2029-10", "2029-11", "2029-12", "2030-01", "2030-02");

    [Fact]
    public void Chart_sums_each_month_and_fills_empty_months_with_zero() =>
        DashboardRules.AcceptedByMonth(Today, Accepted).Select(m => m.Total)
            .Should().Equal(3_000_000, 0, 0, 0, 8_000_000, 11_300_000);

    [Fact]
    public void Accepted_in_month_uses_the_current_calendar_month()   // BR-90 "Giá trị đã chốt tháng"
    {
        DashboardRules.AcceptedInMonth(Today, Accepted).Should().Be(11_300_000);
        DashboardRules.AcceptedInMonth(Today,
        [
            new(new DateOnly(2030, 5, 1), "INITIAL", 1),
            new(new DateOnly(2030, 5, 31), "INITIAL", 10),
            new(new DateOnly(2030, 4, 30), "INITIAL", 100),
        ]).Should().Be(11);
    }

    [Fact]
    public void Accepted_in_month_can_keep_only_additional_quotations() =>   // BR-90 "Doanh thu phát sinh tháng"
        DashboardRules.AcceptedInMonth(Today, Accepted, "ADDITIONAL").Should().Be(1_300_000);

    [Fact]
    public void Windows_include_both_ends()
    {
        DashboardRules.ExpiringSoonUntil(Today).Should().Be(new DateOnly(2030, 5, 18));   // [today, today+3]
        DashboardRules.UpcomingUntil(Today).Should().Be(new DateOnly(2030, 5, 22));       // [today, today+7]
        DashboardRules.ChartStart(Today).Should().Be(new DateOnly(2029, 12, 1));
        DashboardRules.MonthKey(new DateOnly(2030, 3, 9)).Should().Be("2030-03");
    }

    [Fact]
    public void Open_project_statuses_exclude_completed_and_cancelled() =>   // DSH-02
        DashboardRules.OpenProjectStatuses.Should().BeEquivalentTo("DRAFT", "QUOTED", "IN_PROGRESS", "ON_HOLD");
}
