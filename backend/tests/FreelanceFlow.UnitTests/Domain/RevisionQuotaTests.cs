using FluentAssertions;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Requests;
using FreelanceFlow.Api.Domain.Revisions;

namespace FreelanceFlow.UnitTests.Domain;

public class RevisionQuotaTests
{
    private static DeliverableQuota Logo(int used, int included = 3) => new("Logo", included, used, 200_000);

    [Theory]
    [InlineData("SCOPE_CHANGE", "Thay đổi phạm vi luôn tính phí")]
    [InlineData("NEW_DELIVERABLE", "Hạng mục mới luôn tính phí")]   // AC-Q7
    public void Scope_change_and_new_deliverable_are_always_chargeable(string type, string reason)
    {
        var s = RevisionQuota.SuggestBilling(type, Logo(0), roundAlreadyCounted: true);

        s.Billing.Should().Be("CHARGEABLE");
        s.Reason.Should().Be(reason);
    }

    [Fact]
    public void Project_level_round_suggests_chargeable() =>
        RevisionQuota.SuggestBilling("MINOR", null, false)
            .Should().Be(new BillingSuggestion("CHARGEABLE", "Vòng sửa cấp dự án không dùng quota"));

    [Fact]
    public void Remaining_quota_suggests_in_quota() =>
        RevisionQuota.SuggestBilling("MINOR", Logo(used: 2), false)
            .Should().Be(new BillingSuggestion("IN_QUOTA", "Logo đã dùng 2/3 vòng → Trong quota"));

    [Fact]
    public void Exhausted_quota_suggests_chargeable_with_usage_and_price()   // AC-Q2
    {
        var s = RevisionQuota.SuggestBilling("MAJOR", Logo(used: 3), false);

        s.Billing.Should().Be("CHARGEABLE");
        s.Reason.Should().Be("Logo đã dùng 3/3 vòng → Tính phí 200.000 ₫/vòng");
    }

    [Fact]
    public void Already_counted_round_stays_in_quota_even_when_exhausted() =>
        RevisionQuota.SuggestBilling("MINOR", Logo(used: 3), roundAlreadyCounted: true).Billing.Should().Be("IN_QUOTA");

    [Fact]
    public void Plan_with_null_billing_puts_every_minor_item_in_one_counted_round()   // AC-Q1
    {
        var plan = RevisionQuota.PlanRound(Logo(used: 0), Enumerable.Repeat(new PlannedItemInput("MINOR", null), 5).ToList());

        plan.Items.Should().HaveCount(5).And.OnlyContain(i => i.Billing == "IN_QUOTA");
        plan.Counted.Should().BeTrue();
    }

    [Fact]
    public void Plan_keeps_explicit_billing_and_counts_the_round_once()   // AC-Q3, BR-42
    {
        var plan = RevisionQuota.PlanRound(Logo(used: 1),
            [new("MINOR", null), new("MAJOR", "IN_QUOTA"), new("MINOR", "CHARGEABLE")]);

        plan.Items.Select(i => i.Billing).Should().Equal("IN_QUOTA", "IN_QUOTA", "CHARGEABLE");
        plan.Counted.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Plan_on_exhausted_quota_is_order_independent_when_an_item_is_explicitly_in_quota(bool explicitFirst)   // BR-42
    {
        PlannedItemInput[] items = explicitFirst
            ? [new("MINOR", "IN_QUOTA"), new("MINOR", null)]
            : [new("MINOR", null), new("MINOR", "IN_QUOTA")];

        var plan = RevisionQuota.PlanRound(Logo(used: 3), items);

        plan.Items.Select(i => i.Billing).Should().Equal("IN_QUOTA", "IN_QUOTA");
        plan.Counted.Should().BeTrue();
    }

    [Fact]
    public void Plan_on_exhausted_quota_without_explicit_in_quota_stays_chargeable()
    {
        var plan = RevisionQuota.PlanRound(Logo(used: 3), [new("MINOR", null), new("MAJOR", "CHARGEABLE")]);

        plan.Items.Select(i => i.Billing).Should().Equal("CHARGEABLE", "CHARGEABLE");
        plan.Counted.Should().BeFalse();
    }

    [Fact]
    public void Plan_on_a_project_level_round_rejects_explicit_in_quota()   // BR-41
    {
        FluentActions.Invoking(() => RevisionQuota.PlanRound(null, [new("MINOR", "IN_QUOTA")]))
            .Should().Throw<DomainException>().Which.Code.Should().Be("BUSINESS_RULE");
        RevisionQuota.PlanRound(null, [new("MINOR", null), new("MINOR", "FREE_FIX")]).Counted.Should().BeFalse();
    }

    [Fact]
    public void Plan_of_a_free_fix_round_is_not_counted()   // AC-Q4, BR-43
    {
        var plan = RevisionQuota.PlanRound(Logo(used: 3), [new("MINOR", "FREE_FIX")]);

        plan.Counted.Should().BeFalse();
        plan.Items[0].Billing.Should().Be("FREE_FIX");
        plan.Items[0].SuggestedBilling.Should().Be("CHARGEABLE");
    }

    [Theory]
    [InlineData("HUGE", null)]
    [InlineData("MINOR", "FREE")]
    public void Plan_rejects_unknown_type_or_billing(string type, string? billing) =>
        FluentActions.Invoking(() => RevisionQuota.PlanRound(Logo(0), [new(type, billing)]))
            .Should().Throw<DomainException>().Which.Code.Should().Be("VALIDATION_FAILED");

    [Fact]
    public void Used_rounds_counts_rounds_with_a_live_in_quota_item_once_per_round()   // §5.1, BR-42/43, AC-Q6
    {
        var logo = Guid.NewGuid();
        var r1 = Round(logo);
        var r2 = Round(logo);
        var r3 = Round(logo);
        var projectLevel = Round(null);
        var items = new[]
        {
            Item(r1, "IN_QUOTA"), Item(r1, "IN_QUOTA"), Item(r1, "CHARGEABLE"),   // counted once
            Item(r2, "IN_QUOTA", "CANCELLED"),                                   // cancelled → not counted
            Item(r3, "FREE_FIX"), Item(r3, "CHARGEABLE"),                        // no IN_QUOTA → not counted
            Item(projectLevel, "CHARGEABLE"),
        };

        var usage = RevisionQuota.UsedRounds(new[] { r1, r2, r3, projectLevel }.AsQueryable(), items.AsQueryable()).ToList();

        usage.Should().ContainSingle().Which.Should().Be(new DeliverableRoundUsage { DeliverableId = logo, UsedRounds = 1 });
    }

    [Fact]
    public void Round_is_counted_only_with_a_non_cancelled_in_quota_item()
    {
        var round = Round(Guid.NewGuid());

        RevisionQuota.RoundIsCounted([Item(round, "IN_QUOTA", "DONE")]).Should().BeTrue();
        RevisionQuota.RoundIsCounted([Item(round, "IN_QUOTA", "CANCELLED"), Item(round, "CHARGEABLE")]).Should().BeFalse();
    }

    [Theory]
    [InlineData(3, 1, 2)]
    [InlineData(2, 3, 0)]   // BR-44: quota lowered below usage
    public void Remaining_rounds_never_goes_negative(int included, int used, int remaining) =>
        RevisionQuota.RemainingRounds(included, used).Should().Be(remaining);

    [Fact]
    public void Vnd_formats_with_dot_thousands() => RevisionQuota.Vnd(1_234_500).Should().Be("1.234.500 ₫");

    private static RevisionRound Round(Guid? deliverableId) =>
        RevisionRound.Create(Guid.NewGuid(), deliverableId, 1, new DateOnly(2026, 11, 8), null);

    private static RevisionItem Item(RevisionRound round, string billing, string workStatus = "TODO") =>
        new() { RoundId = round.Id, Description = "x", Type = "MINOR", Billing = billing, WorkStatus = workStatus };
}
