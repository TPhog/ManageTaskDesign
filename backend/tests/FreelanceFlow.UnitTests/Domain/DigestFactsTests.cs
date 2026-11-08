using FluentAssertions;
using FreelanceFlow.Api.Domain.Dashboard;

namespace FreelanceFlow.UnitTests.Domain;

public class DigestFactsTests
{
    private static DigestFacts Empty(long receivables = 0) => new(0, [], 0, [], 0, [], [], [], receivables);

    [Fact]
    public void Nothing_to_report_when_only_receivables_exist() =>
        Empty(receivables: 8_000_000).HasContent.Should().BeFalse();

    [Fact]
    public void Any_overdue_today_tomorrow_deadline_or_expiring_quotation_is_content()
    {
        (Empty() with { OverdueCount = 1 }).HasContent.Should().BeTrue();
        (Empty() with { TodayCount = 1 }).HasContent.Should().BeTrue();
        (Empty() with { TomorrowCount = 1 }).HasContent.Should().BeTrue();
        (Empty() with { ProjectDeadlines = [new DigestDeadline("Logo", new DateOnly(2030, 5, 17))] }).HasContent.Should().BeTrue();
        (Empty() with { ExpiringQuotations = [new DigestQuotation("BG-1", new DateOnly(2030, 5, 16))] }).HasContent.Should().BeTrue();
    }

    [Fact]
    public void Schedule_covers_today_through_today_plus_six_and_vn_hour_adds_seven()
    {
        var dates = DashboardRules.ScheduleDates(new DateOnly(2030, 5, 15));
        dates.Should().HaveCount(7);
        dates[0].Should().Be(new DateOnly(2030, 5, 15));
        dates[6].Should().Be(new DateOnly(2030, 5, 21));
        DashboardRules.VnHour(new DateTimeOffset(2030, 5, 14, 17, 30, 0, TimeSpan.Zero)).Should().Be(0);   // 00:30 VN next day
        DashboardRules.VnHour(new DateTimeOffset(2030, 5, 15, 1, 0, 0, TimeSpan.Zero)).Should().Be(8);
    }
}
