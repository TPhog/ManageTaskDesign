using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Projects;

namespace FreelanceFlow.UnitTests;

public class BriefTests
{
    private static IClock FakeClock() =>
        new FakeClock2(new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Confirm_DraftBrief_SetsConfirmedStatus()
    {
        var brief = new ProjectBrief { Status = "DRAFT", Version = 1, Content = "Logo tông xanh" };
        brief.Confirm(FakeClock());

        Assert.Equal("CONFIRMED", brief.Status);
        Assert.NotNull(brief.ConfirmedAt);
    }

    [Fact]
    public void Confirm_AlreadyConfirmed_ThrowsBriefLocked()
    {
        var brief = new ProjectBrief { Status = "CONFIRMED", ConfirmedAt = DateTimeOffset.UtcNow };
        var ex = Assert.Throws<DomainException>(() => brief.Confirm(FakeClock()));
        Assert.Equal("BRIEF_LOCKED", ex.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    public void Confirm_EmptyBrief_ThrowsBusinessRule(string content)   // QA D-08
    {
        var brief = new ProjectBrief { Status = "DRAFT", Version = 1, Content = content };

        var ex = Assert.Throws<DomainException>(() => brief.Confirm(FakeClock()));

        Assert.Equal("BUSINESS_RULE", ex.Code);
        Assert.Equal("DRAFT", brief.Status);
    }

    [Fact]
    public void CreateNewVersion_CopiesContent()
    {
        var source = new ProjectBrief
        {
            ProjectId = Guid.NewGuid(),
            Version = 2,
            Content = "Nội dung brief v2",
            Status = "CONFIRMED",
        };

        var newBrief = source.CreateNewVersion(3);

        Assert.Equal(source.ProjectId, newBrief.ProjectId);
        Assert.Equal(3, newBrief.Version);
        Assert.Equal("DRAFT", newBrief.Status);
        Assert.Equal("Nội dung brief v2", newBrief.Content); // BR-28
    }
}

public class DeliverableTests
{
    [Fact]
    public void ValidateIncludedRoundsChange_AboveUsed_ReturnsNull()
    {
        var d = new Deliverable { IncludedRounds = 3 };
        var warning = d.ValidateIncludedRoundsChange(3, 2);
        Assert.Null(warning);
    }

    [Fact]
    public void ValidateIncludedRoundsChange_BelowUsed_ReturnsWarning()
    {
        var d = new Deliverable { IncludedRounds = 3 };
        var warning = d.ValidateIncludedRoundsChange(1, 3);
        Assert.NotNull(warning);
        Assert.Contains("BR-44", warning);
    }

    [Theory]
    [InlineData("NOT_STARTED", "IN_PROGRESS")]
    [InlineData("NOT_STARTED", "CANCELLED")]
    [InlineData("IN_PROGRESS", "IN_REVIEW")]
    [InlineData("IN_REVIEW", "APPROVED")]
    [InlineData("APPROVED", "DELIVERED")]
    public void ValidateStatusTransition_AllowedTransitions_ReturnsNull(string from, string to)
    {
        var d = new Deliverable { Status = from };
        Assert.Null(d.ValidateStatusTransition(to));
    }

    [Theory]
    [InlineData("NOT_STARTED", "DELIVERED")]
    [InlineData("DELIVERED", "NOT_STARTED")]
    [InlineData("CANCELLED", "IN_PROGRESS")]
    public void ValidateStatusTransition_ForbiddenTransitions_ReturnsError(string from, string to)
    {
        var d = new Deliverable { Status = from };
        Assert.NotNull(d.ValidateStatusTransition(to));
    }
}

file class FakeClock2(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow => now;
    public DateOnly TodayVn => DateOnly.FromDateTime(now.Date);
}
