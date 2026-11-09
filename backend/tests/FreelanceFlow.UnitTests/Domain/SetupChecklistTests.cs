using FluentAssertions;
using FreelanceFlow.Api.Domain.Dashboard;

namespace FreelanceFlow.UnitTests.Domain;

public class SetupChecklistTests
{
    [Fact]
    public void Lists_six_items_in_order_with_links()
    {
        var items = SetupChecklist.Build(new SetupFacts(true, false, false, false, false, false));
        items.Select(i => i.Key).Should().Equal("STUDIO", "BANK", "SERVICE", "TASK_TEMPLATE", "WATERMARK", "CLIENT");
        items.Select(i => i.Href).Should().Equal("/admin/settings?tab=studio", "/admin/settings?tab=payment", "/admin/services", "/admin/services", "/admin/settings?tab=images", "/admin/clients");
        items.Select(i => i.Done).Should().Equal(true, false, false, false, false, false);
    }

    [Fact]
    public void Every_item_carries_a_vietnamese_label_and_hint() =>
        SetupChecklist.Build(new SetupFacts(false, false, false, false, false, false))
            .Should().OnlyContain(i => i.Label.Length > 0 && i.Hint.Length > 0);
}
