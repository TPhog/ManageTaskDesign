using FluentAssertions;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Requests;

namespace FreelanceFlow.UnitTests.Domain;

public class ClientPortalRequestTests
{
    private static ClientRequest Create(string status = "IN_PROGRESS", string content = "  Đổi màu chữ  ", int sentToday = 0) =>
        ClientRequest.CreateByClient(Guid.NewGuid(), status, null, content, sentToday);

    [Fact]
    public void Client_request_is_new_from_the_portal_and_created_by_the_client()   // REQ-01, ck_requests_portal
    {
        var deliverableId = Guid.NewGuid();

        var r = ClientRequest.CreateByClient(Guid.NewGuid(), "IN_PROGRESS", deliverableId, "  Đổi màu chữ  ", 0);

        r.Status.Should().Be("NEW");
        r.Source.Should().Be("PORTAL");
        r.CreatedBy.Should().Be("CLIENT");
        r.Content.Should().Be("Đổi màu chữ");
        r.DeliverableId.Should().Be(deliverableId);
    }

    [Theory]
    [InlineData("DRAFT")]
    [InlineData("QUOTED")]
    [InlineData("IN_PROGRESS")]
    [InlineData("ON_HOLD")]
    public void Open_projects_accept_requests(string status) =>   // BR-53
        Create(status).Status.Should().Be("NEW");

    [Theory]
    [InlineData("COMPLETED")]
    [InlineData("CANCELLED")]
    public void Finished_projects_refuse_requests(string status) =>   // BR-21, AC-R2
        FluentActions.Invoking(() => Create(status))
            .Should().Throw<DomainException>().Which.Code.Should().Be("BUSINESS_RULE");

    [Fact]
    public void Blank_or_too_long_content_is_invalid()
    {
        FluentActions.Invoking(() => Create(content: "   "))
            .Should().Throw<DomainException>().Which.Code.Should().Be("VALIDATION_FAILED");
        FluentActions.Invoking(() => Create(content: new string('a', ClientRequest.MaxContent + 1)))
            .Should().Throw<DomainException>().Which.Code.Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public void The_twenty_first_request_of_the_day_is_rate_limited()   // BR-54, AC-R5
    {
        Create(sentToday: ClientRequest.DailyClientLimit - 1).Status.Should().Be("NEW");

        FluentActions.Invoking(() => Create(sentToday: ClientRequest.DailyClientLimit))
            .Should().Throw<DomainException>().Which.Code.Should().Be("RATE_LIMITED");
    }

    [Fact]
    public void Day_starts_at_midnight_in_Vietnam()   // BR-06
    {
        ClientRequest.DayStartUtc(new DateOnly(2026, 11, 8))
            .Should().Be(new DateTimeOffset(2026, 11, 7, 17, 0, 0, TimeSpan.Zero));
    }
}
