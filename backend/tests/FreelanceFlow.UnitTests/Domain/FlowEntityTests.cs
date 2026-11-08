using FluentAssertions;
using FreelanceFlow.Api.Domain.Catalog;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Domain.Tasks;

namespace FreelanceFlow.UnitTests.Domain;

public class FlowEntityTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 3, 0, 0, TimeSpan.Zero);
    private static readonly IClock Clock = new FlowEntityClock(Now);

    private static Deliverable WithStatus(string status) => new() { Name = "Logo", Status = status };

    private static ProjectTask NewTask(string description = "") =>
        ProjectTask.Create(Guid.NewGuid(), "Logo", description, "TODO", "MEDIUM", null, null, null, ProjectTask.SortStep, Clock);

    [Fact]
    public void Client_approval_is_recorded_while_in_review_and_keeps_the_status()
    {
        var d = WithStatus("IN_REVIEW");

        d.RecordClientApproval(Clock).Should().BeTrue();

        d.Status.Should().Be("IN_REVIEW");
        d.ClientDecision.Should().Be(ClientDecisions.Approved);
        d.ClientDecisionAt.Should().Be(Now);
    }

    [Theory]
    [InlineData("NOT_STARTED")]
    [InlineData("IN_PROGRESS")]
    [InlineData("APPROVED")]
    [InlineData("DELIVERED")]
    public void Client_approval_outside_in_review_is_an_invalid_transition(string status)
    {
        var act = () => WithStatus(status).RecordClientApproval(Clock);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_TRANSITION");
    }

    [Fact]
    public void Recording_the_approval_twice_changes_nothing()
    {
        var d = WithStatus("IN_REVIEW");
        d.RecordClientApproval(Clock);

        d.RecordClientApproval(new FlowEntityClock(Now.AddHours(1))).Should().BeFalse();

        d.ClientDecisionAt.Should().Be(Now);
    }

    [Theory]
    [InlineData("APPROVED")]
    [InlineData("IN_PROGRESS")]
    [InlineData("CANCELLED")]
    public void Leaving_in_review_clears_the_client_decision(string to)
    {
        var d = WithStatus("IN_REVIEW");
        d.RecordClientApproval(Clock);

        d.ChangeStatus(to);

        d.Status.Should().Be(to);
        d.ClientDecision.Should().BeNull();
        d.ClientDecisionAt.Should().BeNull();
    }

    [Fact]
    public void Confirm_approval_moves_in_review_to_approved()
    {
        var d = WithStatus("IN_REVIEW");

        d.ConfirmApproval();

        d.Status.Should().Be("APPROVED");
    }

    [Fact]
    public void Submit_for_review_needs_in_progress()
    {
        var d = WithStatus("IN_PROGRESS");
        d.SubmitForReview();
        d.Status.Should().Be("IN_REVIEW");

        var act = () => WithStatus("NOT_STARTED").SubmitForReview();
        act.Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_TRANSITION");
    }

    [Fact]
    public void Start_moves_only_not_started_deliverables()
    {
        var d = WithStatus("NOT_STARTED");
        d.Start();
        d.Status.Should().Be("IN_PROGRESS");

        var act = () => WithStatus("DELIVERED").Start();
        act.Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_TRANSITION");
    }

    [Fact]
    public void Change_status_rejects_a_transition_outside_the_table()
    {
        var act = () => WithStatus("NOT_STARTED").ChangeStatus("DELIVERED");

        act.Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_TRANSITION");
    }

    [Fact]
    public void Project_start_from_draft_warns_without_an_accepted_initial_quotation()
    {
        var p = new Project { Status = "DRAFT" };

        p.Start(hasAcceptedInitialQuotation: false).Should().Equal("Chưa có báo giá gốc được khách chấp nhận.");

        p.Status.Should().Be("IN_PROGRESS");
        new Project { Status = "QUOTED" }.Start(hasAcceptedInitialQuotation: true).Should().BeEmpty();
    }

    [Theory]
    [InlineData("IN_PROGRESS")]
    [InlineData("ON_HOLD")]
    [InlineData("COMPLETED")]
    [InlineData("CANCELLED")]
    public void Project_start_is_only_for_draft_or_quoted(string status)
    {
        var act = () => new Project { Status = status }.Start(hasAcceptedInitialQuotation: true);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_TRANSITION");
    }

    [Fact]
    public void Service_task_template_is_trimmed_and_kept_in_order()
    {
        var s = new Service();
        s.TaskTemplate().Should().BeEmpty();

        s.SetTaskTemplate([" Moodboard ", "Phác thảo", "Hoàn thiện"]);
        s.TaskTemplate().Should().Equal("Moodboard", "Phác thảo", "Hoàn thiện");

        s.SetTaskTemplate([]);
        s.TaskTemplate().Should().BeEmpty();
    }

    [Fact]
    public void Service_task_template_rejects_blank_long_or_too_many_steps()
    {
        var s = new Service();
        var invalid = new[]
        {
            new[] { "  " },
            new[] { new string('x', 201) },
            Enumerable.Range(1, 21).Select(i => $"Bước {i}").ToArray(),
        };

        foreach (var steps in invalid)
        {
            var act = () => s.SetTaskTemplate(steps);
            act.Should().Throw<DomainException>().Which.Code.Should().Be("VALIDATION_FAILED");
        }

        s.SetTaskTemplate(Enumerable.Range(1, 20).Select(i => $"Bước {i}"));
        s.TaskTemplate().Should().HaveCount(20);
    }

    [Fact]
    public void Task_note_body_is_trimmed_and_limited()
    {
        var taskId = Guid.NewGuid();

        var note = TaskNote.Create(taskId, "  Khách thích tông xanh  ");
        note.TaskId.Should().Be(taskId);
        note.Body.Should().Be("Khách thích tông xanh");

        note.Edit("Đổi sang tông xanh navy");
        note.Body.Should().Be("Đổi sang tông xanh navy");

        foreach (var body in new[] { "   ", new string('x', 5001) })
        {
            var act = () => TaskNote.Create(taskId, body);
            act.Should().Throw<DomainException>().Which.Code.Should().Be("VALIDATION_FAILED");
        }
    }

    [Fact]
    public void Task_description_is_limited_to_20000_characters()
    {
        var task = NewTask(new string('x', 20_000));

        var act = () => task.Edit("Logo", new string('x', 20_001), "MEDIUM", null, null, null);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public void Checklist_description_is_kept_on_toggle_and_replaced_when_given()
    {
        var task = NewTask();
        var item = task.AddChecklistItem("Chọn font", "Ưu tiên font không chân");
        item.Description.Should().Be("Ưu tiên font không chân");

        task.UpdateChecklistItem(item.Id, "Chọn font", done: true);
        item.Description.Should().Be("Ưu tiên font không chân");
        item.Done.Should().BeTrue();

        task.UpdateChecklistItem(item.Id, "Chọn font", done: true, description: "");
        item.Description.Should().BeEmpty();

        var act = () => task.UpdateChecklistItem(item.Id, "Chọn font", true, new string('x', 5001));
        act.Should().Throw<DomainException>().Which.Code.Should().Be("VALIDATION_FAILED");
    }

    [Theory]
    [InlineData(0, 0, null)]
    [InlineData(3, 1, 33)]
    [InlineData(3, 2, 67)]
    [InlineData(8, 1, 13)]
    [InlineData(4, 0, 0)]
    [InlineData(2, 2, 100)]
    public void Task_progress_percent_rounds_half_up_and_is_null_without_tasks(int total, int done, int? expected) =>
        TaskProgress.Percent(total, done).Should().Be(expected);

    [Fact]
    public void Task_progress_counts_done_tasks_per_deliverable()
    {
        var project = Guid.NewGuid();
        var logo = Guid.NewGuid();
        var banner = Guid.NewGuid();
        ProjectTask T(Guid? deliverableId, string status) =>
            ProjectTask.Create(project, "Việc", "", status, "MEDIUM", null, null, deliverableId, ProjectTask.SortStep, Clock);
        var tasks = new[] { T(logo, "DONE"), T(logo, "TODO"), T(banner, "DONE"), T(null, "DONE") }.AsQueryable();

        var counts = TaskProgress.PerDeliverable(tasks).ToDictionary(x => x.DeliverableId);

        counts.Keys.Should().BeEquivalentTo(new[] { logo, banner });
        counts[logo].Should().Be(new DeliverableTaskCount { DeliverableId = logo, Total = 2, Done = 1 });
        counts[banner].Total.Should().Be(1);
        counts[banner].Done.Should().Be(1);
    }
}

file sealed class FlowEntityClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow => now;
    public DateOnly TodayVn => DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(7)).DateTime);
}
