using FluentAssertions;
using FreelanceFlow.Api.Domain.Dashboard;
using FreelanceFlow.Api.Features.Dashboard;
using FreelanceFlow.Api.Infrastructure.Notifications;

namespace FreelanceFlow.UnitTests.Features;

public class DailyDigestMessageTests
{
    private static readonly DateOnly Today = new(2030, 5, 15);
    private static DigestFacts Empty(long receivables = 0) => new(0, [], 0, [], 0, [], [], [], receivables);
    private static DigestTask T(string title, int dayOffset) => new(title, "Dự án A", Today.AddDays(dayOffset));

    [Fact]
    public void Returns_null_when_there_is_nothing_to_report() =>
        DailyDigestMessage.Build(Empty(receivables: 5_000_000), Today, "https://ff.test").Should().BeNull();

    [Fact]
    public void Lists_each_group_with_counts_and_dd_MM_yyyy_title()
    {
        var f = Empty(7_300_000) with
        {
            OverdueCount = 1, Overdue = [T("Sửa logo", -2)],
            TodayCount = 2, Today = [T("Phác thảo", 0), T("Gửi file", 0)],
            TomorrowCount = 1, Tomorrow = [T("Chốt màu", 1)],
            ProjectDeadlines = [new DigestDeadline("Bộ nhận diện", Today.AddDays(2))],
            ExpiringQuotations = [new DigestQuotation("BG-2030-0001", Today.AddDays(1))],
        };
        var n = DailyDigestMessage.Build(f, Today, "https://ff.test")!;
        n.Title.Should().Be("Nhắc việc ngày 15/05/2030");
        n.Severity.Should().Be(NotificationSeverity.Warning);
        n.Url.Should().Be("https://ff.test/admin");
        n.Description.Should().Contain("Sửa logo").And.Contain("Phác thảo").And.Contain("Chốt màu")
            .And.Contain("Bộ nhận diện").And.Contain("BG-2030-0001");
        n.Fields!.Select(x => x.Name).Should().Equal("Quá hạn", "Hôm nay", "Ngày mai", "Công nợ");
        n.Fields!.Single(x => x.Name == "Quá hạn").Value.Should().Be("1");
    }

    [Fact]
    public void Severity_is_info_when_only_upcoming_work_exists()
    {
        var n = DailyDigestMessage.Build(Empty() with { TodayCount = 1, Today = [T("Phác thảo", 0)] }, Today, null)!;
        n.Severity.Should().Be(NotificationSeverity.Info);
        n.Url.Should().BeNull();
    }

    [Fact]
    public void Deadline_and_quotation_lists_also_say_how_many_more()
    {
        var f = Empty() with
        {
            ProjectDeadlines = Enumerable.Range(0, 7).Select(i => new DigestDeadline($"Dự án {i}", Today.AddDays(1))).ToList(),
            ExpiringQuotations = Enumerable.Range(0, 6).Select(i => new DigestQuotation($"BG-{i}", Today.AddDays(1))).ToList(),
        };
        var n = DailyDigestMessage.Build(f, Today, null)!;
        n.Description.Should().Contain("và 2 dự án khác").And.Contain("và 1 báo giá khác");
    }

    [Fact]
    public void Caps_each_list_at_five_and_says_how_many_more()
    {
        var many = Enumerable.Range(0, 9).Select(i => T($"Task {i}", -1)).ToList();
        var n = DailyDigestMessage.Build(Empty() with { OverdueCount = 9, Overdue = many }, Today, null)!;
        n.Description.Should().Contain("Task 4").And.NotContain("Task 5").And.Contain("và 4 việc khác");
    }
}
