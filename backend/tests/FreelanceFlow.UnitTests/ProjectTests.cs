using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Projects;

namespace FreelanceFlow.UnitTests;

public class ProjectTests
{
    private Project Draft => new() { Status = "DRAFT" };
    private Project Quoted => new() { Status = "QUOTED" };
    private Project InProgress => new() { Status = "IN_PROGRESS" };
    private Project OnHold => new() { Status = "ON_HOLD" };
    private Project Completed => new() { Status = "COMPLETED" };
    private Project Cancelled => new() { Status = "CANCELLED" };

    [Theory]
    [InlineData("DRAFT", "QUOTED")]
    [InlineData("DRAFT", "IN_PROGRESS")]
    [InlineData("DRAFT", "CANCELLED")]
    [InlineData("QUOTED", "IN_PROGRESS")]
    [InlineData("QUOTED", "CANCELLED")]
    [InlineData("IN_PROGRESS", "ON_HOLD")]
    [InlineData("IN_PROGRESS", "COMPLETED")]
    [InlineData("IN_PROGRESS", "CANCELLED")]
    [InlineData("ON_HOLD", "IN_PROGRESS")]
    [InlineData("ON_HOLD", "CANCELLED")]
    [InlineData("COMPLETED", "IN_PROGRESS")]
    public void ValidateTransition_AllowedTransitions_DoNotThrow(string from, string to)
    {
        var project = new Project { Status = from };
        var reason = NeedsReason(to, from) ? "some reason" : null;
        var ex = Record.Exception(() => project.ValidateTransition(to, reason, true, false, false));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("DRAFT", "ON_HOLD")]
    [InlineData("DRAFT", "COMPLETED")]
    [InlineData("QUOTED", "COMPLETED")]
    [InlineData("QUOTED", "ON_HOLD")]
    [InlineData("ON_HOLD", "COMPLETED")]
    [InlineData("CANCELLED", "DRAFT")]
    [InlineData("CANCELLED", "IN_PROGRESS")]
    [InlineData("COMPLETED", "CANCELLED")]
    public void ValidateTransition_ForbiddenTransitions_ThrowInvalidTransition(string from, string to)
    {
        var project = new Project { Status = from };
        var ex = Assert.Throws<DomainException>(() => project.ValidateTransition(to, "reason", true, false, false));
        Assert.Equal("INVALID_TRANSITION", ex.Code);
    }

    [Fact]
    public void ValidateTransition_CancelledWithoutReason_ThrowsValidationFailed()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Draft.ValidateTransition("CANCELLED", null, true, false, false));
        Assert.Equal("VALIDATION_FAILED", ex.Code);
    }

    [Fact]
    public void ValidateTransition_OnHoldWithoutReason_ThrowsValidationFailed()
    {
        var ex = Assert.Throws<DomainException>(() =>
            InProgress.ValidateTransition("ON_HOLD", null, true, false, false));
        Assert.Equal("VALIDATION_FAILED", ex.Code);
    }

    [Fact]
    public void ValidateTransition_ReopenWithoutReason_ThrowsValidationFailed()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Completed.ValidateTransition("IN_PROGRESS", null, true, false, false));
        Assert.Equal("VALIDATION_FAILED", ex.Code);
    }

    [Fact]
    public void ValidateTransition_ToInProgressWithNoAcceptedQuotation_ReturnsWarning()
    {
        var warnings = Draft.ValidateTransition("IN_PROGRESS", null, hasAcceptedInitialQuotation: false,
            hasIncompleteDeliverables: false, hasUnpaidAcceptedQuotations: false);
        Assert.Single(warnings);
    }

    [Fact]
    public void ValidateTransition_QuotedToInProgressWithNoAcceptedQuotation_ReturnsWarning()
    {
        var warnings = Quoted.ValidateTransition("IN_PROGRESS", null, hasAcceptedInitialQuotation: false,
            hasIncompleteDeliverables: false, hasUnpaidAcceptedQuotations: false);
        Assert.Single(warnings);
    }

    [Fact]
    public void ValidateTransition_OnHoldToInProgressWithNoAcceptedQuotation_NoWarning()
    {
        var warnings = OnHold.ValidateTransition("IN_PROGRESS", null, hasAcceptedInitialQuotation: false,
            hasIncompleteDeliverables: false, hasUnpaidAcceptedQuotations: false);
        Assert.Empty(warnings);
    }

    [Fact]
    public void ValidateTransition_CompletedToInProgressWithNoAcceptedQuotation_NoWarning()
    {
        var warnings = Completed.ValidateTransition("IN_PROGRESS", "reopen", hasAcceptedInitialQuotation: false,
            hasIncompleteDeliverables: false, hasUnpaidAcceptedQuotations: false);
        Assert.Empty(warnings);
    }

    [Fact]
    public void ValidateTransition_ToInProgressWithAcceptedQuotation_NoWarnings()
    {
        var warnings = Draft.ValidateTransition("IN_PROGRESS", null, hasAcceptedInitialQuotation: true,
            hasIncompleteDeliverables: false, hasUnpaidAcceptedQuotations: false);
        Assert.Empty(warnings);
    }

    [Fact]
    public void ValidateTransition_ToCompletedWithIssues_ReturnsTwoWarnings()
    {
        var warnings = InProgress.ValidateTransition("COMPLETED", null, hasAcceptedInitialQuotation: true,
            hasIncompleteDeliverables: true, hasUnpaidAcceptedQuotations: true);
        Assert.Equal(2, warnings.Length);
    }

    [Fact]
    public void ValidateTransition_ToCompletedWithOpenTasksAndRevisionItems_ListsThem()   // P14 DF-08
    {
        var warnings = InProgress.ValidateTransition("COMPLETED", null, true, false, false,
            openTaskTitles: ["A"], openRevisionItems: 2);

        Assert.Contains("Còn 1 công việc chưa xong: A", warnings);
        Assert.Contains("Còn 2 mục sửa chưa xong (Cần làm / Đang làm)", warnings);
    }

    [Fact]
    public void ValidateTransition_ToCompletedWithManyOpenTasks_ShowsThreeTitlesAndTheRest()
    {
        var warnings = InProgress.ValidateTransition("COMPLETED", null, true, false, false,
            openTaskTitles: ["T1", "T2", "T3", "T4", "T5"]);

        Assert.Equal(["Còn 5 công việc chưa xong: T1, T2, T3, … (+2)"], warnings);
    }

    [Fact]
    public void ValidateTransition_ToInProgress_IgnoresOpenItems()
    {
        var warnings = Draft.ValidateTransition("IN_PROGRESS", null, true, false, false,
            openTaskTitles: ["A"], openRevisionItems: 2);

        Assert.Empty(warnings);
    }

    [Fact]
    public void ApplyStatusChange_SetsStatusAndReason()
    {
        var project = new Project { Status = "IN_PROGRESS" };
        project.ApplyStatusChange("ON_HOLD", "รอก็ต้องรอ", true);

        Assert.Equal("ON_HOLD", project.Status);
        Assert.Equal("รอก็ต้องรอ", project.StatusReason);
        Assert.True(project.StatusReasonPublic);
    }

    private static bool NeedsReason(string to, string from) =>
        to is "CANCELLED" or "ON_HOLD" || (to is "IN_PROGRESS" && from is "COMPLETED");
}
