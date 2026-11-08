using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Tasks;

namespace FreelanceFlow.UnitTests;

public class TaskTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 3, 0, 0, TimeSpan.Zero);
    private static readonly IClock Clock = new TaskFakeClock(Now);

    private static ProjectTask NewTask(string status = "TODO") =>
        ProjectTask.Create(Guid.NewGuid(), "Phác thảo", "", status, "MEDIUM", null, null, null, ProjectTask.SortStep, Clock);

    private static DateOnly D(string iso) => DateOnly.ParseExact(iso, "yyyy-MM-dd");

    [Fact]
    public void Create_TrimsTitle_AndKeepsGivenFields()
    {
        var projectId = Guid.NewGuid();
        var deliverableId = Guid.NewGuid();

        var task = ProjectTask.Create(projectId, "  Logo  ", "Mô tả", "IN_PROGRESS", "HIGH",
            D("2026-11-01"), D("2026-11-20"), deliverableId, 2048, Clock);

        Assert.Equal(projectId, task.ProjectId);
        Assert.Equal("Logo", task.Title);
        Assert.Equal("Mô tả", task.Description);
        Assert.Equal("IN_PROGRESS", task.Status);
        Assert.Equal("HIGH", task.Priority);
        Assert.Equal(D("2026-11-01"), task.StartDate);
        Assert.Equal(D("2026-11-20"), task.DueDate);
        Assert.Equal(deliverableId, task.DeliverableId);
        Assert.Equal(2048, task.SortOrder);
        Assert.Null(task.CompletedAt);
    }

    [Fact]
    public void Create_AsDone_StampsCompletedAt()
    {
        var task = NewTask("DONE");

        Assert.Equal("DONE", task.Status);
        Assert.Equal(Now, task.CompletedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Edit_BlankTitle_ThrowsValidationFailed(string title)
    {
        var ex = Assert.Throws<DomainException>(() => NewTask().Edit(title, "", "MEDIUM", null, null, null));
        Assert.Equal("VALIDATION_FAILED", ex.Code);
    }

    [Fact]
    public void Edit_UnknownPriority_ThrowsValidationFailed()
    {
        var ex = Assert.Throws<DomainException>(() => NewTask().Edit("Logo", "", "SOON", null, null, null));
        Assert.Equal("VALIDATION_FAILED", ex.Code);
    }

    // BR-80: every status can reach every other status
    [Theory]
    [InlineData("TODO", "DONE")]
    [InlineData("DONE", "TODO")]
    [InlineData("BLOCKED", "REVIEW")]
    [InlineData("REVIEW", "BLOCKED")]
    [InlineData("IN_PROGRESS", "TODO")]
    public void Move_AllowsAnyStatusChange(string from, string to)
    {
        var task = NewTask(from);

        var changed = task.Move(to, 10, Clock);

        Assert.True(changed);
        Assert.Equal(to, task.Status);
        Assert.Equal(10, task.SortOrder);
    }

    [Fact]
    public void Move_ToDone_StampsCompletedAt_AndLeavingDoneClearsIt()
    {
        var task = NewTask();

        task.Move("DONE", 1, Clock);
        Assert.Equal(Now, task.CompletedAt);

        task.Move("REVIEW", 2, Clock);
        Assert.Null(task.CompletedAt);
    }

    [Fact]
    public void Move_WithinSameStatus_OnlyReorders()
    {
        var task = NewTask("DONE");

        var changed = task.Move("DONE", 99.5, Clock);

        Assert.False(changed);
        Assert.Equal(99.5, task.SortOrder);
        Assert.Equal(Now, task.CompletedAt);
    }

    [Fact]
    public void Move_UnknownStatus_ThrowsValidationFailed()
    {
        var ex = Assert.Throws<DomainException>(() => NewTask().Move("DOING", 1, Clock));
        Assert.Equal("VALIDATION_FAILED", ex.Code);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Move_NonFiniteSortOrder_ThrowsValidationFailed(double sortOrder)
    {
        var ex = Assert.Throws<DomainException>(() => NewTask().Move("TODO", sortOrder, Clock));
        Assert.Equal("VALIDATION_FAILED", ex.Code);
    }

    // BR-81: overdue = due_date < today(VN) and status ≠ DONE
    [Theory]
    [InlineData("2026-11-07", "TODO", true)]
    [InlineData("2026-11-07", "BLOCKED", true)]
    [InlineData("2026-11-07", "DONE", false)]
    [InlineData("2026-11-08", "TODO", false)]
    [InlineData(null, "TODO", false)]
    public void IsOverdue_FollowsBr81(string? due, string status, bool expected)
    {
        var task = NewTask(status);
        task.DueDate = due == null ? null : D(due);

        Assert.Equal(expected, task.IsOverdue(D("2026-11-08")));
    }

    [Theory]
    [InlineData("2026-11-09", "2026-11-15")] // Monday
    [InlineData("2026-11-11", "2026-11-15")] // Wednesday
    [InlineData("2026-11-15", "2026-11-15")] // Sunday
    [InlineData("2026-12-31", "2027-01-03")] // Thursday, week crosses the year
    public void EndOfWeek_IsTheComingSunday(string today, string expected)
    {
        Assert.Equal(D(expected), ProjectTask.EndOfWeek(D(today)));
    }

    [Fact]
    public void AddChecklistItem_TrimsText_AndAppendsInOrder()
    {
        var task = NewTask();

        var first = task.AddChecklistItem(" Phác thảo ");
        var second = task.AddChecklistItem("Chốt màu");

        Assert.Equal("Phác thảo", first.Text);
        Assert.Equal(0, first.SortOrder);
        Assert.Equal(1, second.SortOrder);
        Assert.Equal(task.Id, second.TaskId);
        Assert.False(second.Done);
        Assert.Equal(2, task.ChecklistItems.Count);
    }

    [Fact]
    public void AddChecklistItem_BlankText_ThrowsValidationFailed()
    {
        var ex = Assert.Throws<DomainException>(() => NewTask().AddChecklistItem("  "));
        Assert.Equal("VALIDATION_FAILED", ex.Code);
    }

    [Fact]
    public void AddChecklistItem_AfterRemove_KeepsIncreasingSortOrder()
    {
        var task = NewTask();
        var first = task.AddChecklistItem("A");
        task.AddChecklistItem("B");

        task.RemoveChecklistItem(first.Id);
        var third = task.AddChecklistItem("C");

        Assert.Equal(2, third.SortOrder);
    }

    [Fact]
    public void UpdateChecklistItem_SetsTextAndDone()
    {
        var task = NewTask();
        var item = task.AddChecklistItem("Phác thảo");

        task.UpdateChecklistItem(item.Id, " Phác thảo 3 hướng ", true);

        Assert.Equal("Phác thảo 3 hướng", item.Text);
        Assert.True(item.Done);
    }

    [Fact]
    public void UpdateChecklistItem_UnknownItem_ThrowsNotFound()
    {
        var ex = Assert.Throws<DomainException>(() => NewTask().UpdateChecklistItem(Guid.NewGuid(), "X", true));
        Assert.Equal("NOT_FOUND", ex.Code);
    }

    [Fact]
    public void RemoveChecklistItem_RemovesAndReturnsIt()
    {
        var task = NewTask();
        var item = task.AddChecklistItem("Phác thảo");

        var removed = task.RemoveChecklistItem(item.Id);

        Assert.Same(item, removed);
        Assert.Empty(task.ChecklistItems);
    }

    [Fact]
    public void RemoveChecklistItem_UnknownItem_ThrowsNotFound()
    {
        var ex = Assert.Throws<DomainException>(() => NewTask().RemoveChecklistItem(Guid.NewGuid()));
        Assert.Equal("NOT_FOUND", ex.Code);
    }
}

file class TaskFakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow => now;
    public DateOnly TodayVn => DateOnly.FromDateTime(now.Date);
}
