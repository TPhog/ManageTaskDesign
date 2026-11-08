using FluentAssertions;
using FreelanceFlow.Api.Domain.Quotations;
using FreelanceFlow.Api.Domain.Revisions;

namespace FreelanceFlow.UnitTests.Domain;

public class AdditionalQuotationTests
{
    private static readonly Guid Round4 = Guid.NewGuid();

    private static ChargeableItem Item(string type, string description, Guid? roundId = null, int roundNo = 4,
        string? deliverable = "Logo", string? serviceName = null, long servicePrice = 0) =>
        new(Guid.NewGuid(), roundId ?? Round4, roundNo, type, description, deliverable, 200_000, serviceName, servicePrice);

    [Fact]
    public void Minor_items_of_a_round_share_one_line_and_a_new_deliverable_gets_its_own()   // AC-Q8, BR-46
    {
        var items = new[]
        {
            Item("MINOR", "Đổi màu"), Item("MINOR", "Đổi font"), Item("MINOR", "Căn lề"),
            Item("NEW_DELIVERABLE", "Thêm banner Facebook", serviceName: "Thiết kế Banner", servicePrice: 500_000),
        };

        var plan = AdditionalQuotation.Plan(items, "Nhận diện ABC");

        plan.Lines.Select(l => (l.Description, l.UnitPrice, l.Quantity)).Should().Equal(
            ("Vòng sửa thứ 4 — Logo", 200_000L, 1), ("Thiết kế Banner", 500_000L, 1));
        plan.Lines[0].Details.Should().Be("• Đổi màu\n• Đổi font\n• Căn lề");
        plan.Lines[1].Details.Should().Be("Thêm banner Facebook");
        plan.Lines.Should().OnlyContain(l => l.SourceType == QuotationItemSources.Revision && l.SourceId == Round4 && l.Id == null);
        plan.LineIndexByItemId.Should().HaveCount(4);
        items.Take(3).Select(i => plan.LineIndexByItemId[i.ItemId]).Should().OnlyContain(i => i == 0);
        plan.LineIndexByItemId[items[3].ItemId].Should().Be(1);
    }

    [Fact]
    public void Each_round_scope_change_and_unpriced_new_deliverable_get_their_own_lines()
    {
        var round5 = Guid.NewGuid();
        var projectRound = Guid.NewGuid();
        var items = new[]
        {
            Item("MAJOR", "Đổi bố cục"),
            Item("MINOR", "Sửa màu", round5, roundNo: 5),
            Item("SCOPE_CHANGE", "Thêm phiên bản tiếng Anh"),
            Item("NEW_DELIVERABLE", "Thiệp mời"),
            Item("MINOR", "Sửa footer", projectRound, roundNo: 1, deliverable: null),
        };

        var plan = AdditionalQuotation.Plan(items, "Nhận diện ABC");

        plan.Lines.Select(l => (l.Description, l.UnitPrice)).Should().Equal(
            ("Vòng sửa thứ 4 — Logo", 200_000L),
            ("Vòng sửa thứ 5 — Logo", 200_000L),
            ("Thêm phiên bản tiếng Anh", 0L),
            ("Thiệp mời", 0L),
            ("Vòng sửa thứ 1 — Nhận diện ABC", 0L));   // project-level round has no extra_round_price
    }

    [Fact]
    public void Long_descriptions_are_cut_to_the_quotation_line_limit()
    {
        var plan = AdditionalQuotation.Plan([Item("SCOPE_CHANGE", new string('a', 900))], "P");

        plan.Lines[0].Description.Should().HaveLength(500).And.EndWith("…");
    }

    [Fact]
    public void Plan_feeds_create_draft_in_line_order()
    {
        var items = new[] { Item("MINOR", "Đổi màu"), Item("SCOPE_CHANGE", "Thêm bản in") };
        var plan = AdditionalQuotation.Plan(items, "P");

        var q = Quotation.CreateDraft(Guid.NewGuid(), QuotationKinds.Additional, "Phát sinh", 7, 50, "", plan.Lines);

        q.Kind.Should().Be("ADDITIONAL");
        q.Items.Select(i => i.Description).Should().Equal("Vòng sửa thứ 4 — Logo", "Thêm bản in");
        q.Total.Should().Be(200_000);
        q.Items[plan.LineIndexByItemId[items[1].ItemId]].Description.Should().Be("Thêm bản in");
    }
}
