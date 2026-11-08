using FluentAssertions;
using FreelanceFlow.Api.Domain.Projects;

namespace FreelanceFlow.UnitTests.Domain;

public class ClientReviewStateTests
{
    [Theory]
    [InlineData("IN_REVIEW", null, false, "AWAITING_CLIENT")]
    [InlineData("IN_REVIEW", "APPROVED", false, "CLIENT_APPROVED")]
    [InlineData("IN_REVIEW", null, true, "CHANGES_IN_PROGRESS")]
    [InlineData("IN_REVIEW", "APPROVED", true, "CHANGES_IN_PROGRESS")]
    [InlineData("IN_PROGRESS", null, true, "CHANGES_IN_PROGRESS")]
    [InlineData("IN_PROGRESS", null, false, "NONE")]
    [InlineData("APPROVED", null, true, "NONE")]
    [InlineData("DELIVERED", null, false, "NONE")]
    [InlineData("NOT_STARTED", null, false, "NONE")]
    public void One_voice_per_deliverable(string status, string? decision, bool openChange, string expected) =>
        ClientReviewStates.Of(status, decision, openChange).Should().Be(expected);
}
