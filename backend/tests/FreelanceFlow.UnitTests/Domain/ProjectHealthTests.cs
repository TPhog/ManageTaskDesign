using FluentAssertions;
using FreelanceFlow.Api.Domain.Dashboard;
using FreelanceFlow.Api.Domain.Flow;

namespace FreelanceFlow.UnitTests.Domain;

public class ProjectHealthTests
{
    private static readonly DateOnly Today = new(2030, 5, 15);

    private static FlowDeliverable Del(int total, int done, int included, int used, string status = "IN_PROGRESS") =>
        new(Guid.NewGuid(), "Logo", status, total, done, null, included, used);

    private static FlowFacts Facts(string projectStatus = "IN_PROGRESS", IReadOnlyList<FlowDeliverable>? del = null,
        IReadOnlyList<FlowQuotation>? quotes = null, int overdue = 0, int newRequests = 0) =>
        new(projectStatus, true, del ?? [Del(4, 2, 2, 1)], quotes ?? [], overdue,
            Enumerable.Range(0, newRequests).Select(_ => Guid.NewGuid()).ToList(), 0);

    private static ProjectHealthResult Assess(FlowFacts f, DateOnly? due) =>
        ProjectHealth.Assess(f, ProjectFlow.Evaluate(f, Today), due, Today);

    [Fact]
    public void Progress_and_quota_are_summed_over_active_deliverables()
    {
        var f = Facts(del: [Del(4, 2, 2, 1), Del(2, 2, 2, 0), Del(10, 0, 5, 5, "CANCELLED")]);
        var r = Assess(f, Today.AddDays(3));
        r.ProgressPercent.Should().Be(67);        // 4 of 6 tasks
        r.QuotaUsed.Should().Be(1);
        r.QuotaTotal.Should().Be(4);
        r.DaysToDeadline.Should().Be(3);
    }

    [Fact]
    public void Progress_is_null_without_tasks_and_deadline_null_without_due_date()
    {
        var r = Assess(Facts(del: [Del(0, 0, 2, 0)]), null);
        r.ProgressPercent.Should().BeNull();
        r.DaysToDeadline.Should().BeNull();
    }

    [Fact]
    public void Late_when_past_due_unless_completed()
    {
        Assess(Facts(), Today.AddDays(-1)).Risks.Should().Contain(HealthRisks.Late);
        Assess(Facts(), Today).Risks.Should().NotContain(HealthRisks.Late);
        Assess(Facts("COMPLETED"), Today.AddDays(-10)).Risks.Should().NotContain(HealthRisks.Late);
    }

    [Fact]
    public void Unpaid_deposit_and_open_requests_and_overdue_tasks_are_flagged()
    {
        var quote = new FlowQuotation(Guid.NewGuid(), "INITIAL", "ACCEPTED", "BG-1", 1, null, "UNPAID", 50, 10_000_000, 5_000_000, DateTimeOffset.UnixEpoch);
        var r = Assess(Facts(quotes: [quote], overdue: 2, newRequests: 1), Today.AddDays(5));
        r.Risks.Should().Contain([HealthRisks.DepositUnpaid, HealthRisks.OpenRequests, HealthRisks.TasksOverdue]);
        r.Score.Should().Be(3 + 2 + 2);
    }

    [Fact]
    public void Completed_project_with_money_due_carries_balance_due()
    {
        var quote = new FlowQuotation(Guid.NewGuid(), "INITIAL", "ACCEPTED", "BG-1", 1, null, "DEPOSIT_PAID", 50, 10_000_000, 5_000_000, DateTimeOffset.UnixEpoch);
        var f = Facts("COMPLETED", [Del(2, 2, 2, 0, "DELIVERED")], [quote]);
        Assess(f, Today.AddDays(-4)).Risks.Should().Equal(HealthRisks.BalanceDue);
    }

    [Fact]
    public void Weights_are_late_4_money_3_overdue_and_requests_2_quota_1()
    {
        HealthRisks.Weight(HealthRisks.Late).Should().Be(4);
        HealthRisks.Weight(HealthRisks.DepositUnpaid).Should().Be(3);
        HealthRisks.Weight(HealthRisks.BalanceDue).Should().Be(3);
        HealthRisks.Weight(HealthRisks.TasksOverdue).Should().Be(2);
        HealthRisks.Weight(HealthRisks.OpenRequests).Should().Be(2);
        HealthRisks.Weight(HealthRisks.QuotaExceeded).Should().Be(1);
    }
}
