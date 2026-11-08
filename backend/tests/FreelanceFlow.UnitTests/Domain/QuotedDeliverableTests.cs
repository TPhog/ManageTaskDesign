using FluentAssertions;
using FreelanceFlow.Api.Domain.Catalog;
using FreelanceFlow.Api.Domain.Projects;

namespace FreelanceFlow.UnitTests.Domain;

public class QuotedDeliverableTests
{
    [Fact]
    public void Deliverable_from_an_accepted_line_copies_the_service_quota()   // BR-48
    {
        var service = new Service { Name = "Sticker", BasePrice = 300_000, DefaultIncludedRounds = 2, ExtraRoundPrice = 100_000 };
        var projectId = Guid.NewGuid();

        var d = Deliverable.CreateFromAcceptedLine(projectId, "  Sticker dán ly  ", 3, service, 40);

        d.ProjectId.Should().Be(projectId);
        d.Name.Should().Be("Sticker dán ly");
        d.Quantity.Should().Be(3);
        d.ServiceId.Should().Be(service.Id);
        d.IncludedRounds.Should().Be(2);
        d.ExtraRoundPrice.Should().Be(100_000);
        d.Status.Should().Be("NOT_STARTED");
        d.SortOrder.Should().Be(40);
    }

    [Fact]
    public void Without_a_service_the_quota_is_zero_and_the_name_fits_the_deliverable_limit()
    {
        var d = Deliverable.CreateFromAcceptedLine(Guid.NewGuid(), new string('a', 500), 0, null, 10);

        d.ServiceId.Should().BeNull();
        d.IncludedRounds.Should().Be(0);
        d.ExtraRoundPrice.Should().Be(0);
        d.Quantity.Should().Be(1);
        d.Name.Should().HaveLength(Deliverable.MaxName).And.EndWith("…");
    }
}
