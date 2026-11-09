using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Tasks;

public class TaskCalendarTests(AppFactory factory) : TaskTestBase(factory)
{
    private Task<JsonElement> CalendarAsync(string from, string to, Guid? projectId = null) =>
        Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks/calendar?from={from}&to={to}" + (projectId is { } p ? $"&projectId={p}" : ""));

    [Fact]
    public async Task Returns_tasks_by_due_date_and_overlapping_spans_but_not_outside_the_range()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var d0 = TodayVn;
        var inRange = (await CreateTaskAsync(projectId, new { title = "in", dueDate = Iso(d0.AddDays(2)) })).GetProperty("id").GetGuid();
        var span = (await CreateTaskAsync(projectId, new { title = "span", startDate = Iso(d0.AddDays(-3)), dueDate = Iso(d0.AddDays(1)) })).GetProperty("id").GetGuid();
        var outside = (await CreateTaskAsync(projectId, new { title = "out", dueDate = Iso(d0.AddDays(40)) })).GetProperty("id").GetGuid();
        var noDates = (await CreateTaskAsync(projectId, new { title = "none" })).GetProperty("id").GetGuid();

        var cal = await CalendarAsync(Iso(d0), Iso(d0.AddDays(6)), projectId);

        var ids = cal.GetProperty("tasks").EnumerateArray().Select(t => t.GetProperty("id").GetGuid()).ToList();
        ids.Should().Contain([inRange, span]).And.NotContain([outside, noDates]);
    }

    [Fact]
    public async Task Includes_deliverable_and_project_deadlines_as_markers_and_skips_cancelled_projects()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync("Có hạn");
        var due = Iso(TodayVn.AddDays(3));
        (await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/deliverables",
            new { name = "Logo", quantity = 1, includedRounds = 1, extraRoundPrice = 0, dueDate = due })).EnsureSuccessStatusCode();

        var cal = await CalendarAsync(Iso(TodayVn), Iso(TodayVn.AddDays(6)), projectId);

        var markers = cal.GetProperty("markers").EnumerateArray().ToList();
        markers.Should().Contain(m => m.GetProperty("kind").GetString() == "DELIVERABLE" && m.GetProperty("title").GetString() == "Logo" && m.GetProperty("date").GetString() == due);
    }

    private async Task<Guid> CreateProjectWithDueAsync(string name, DateOnly due)
    {
        var clientResp = await Client.PostAsJsonAsync("/api/admin/clients", new { name = "Cal Client " + Guid.NewGuid() });
        clientResp.EnsureSuccessStatusCode();
        var clientId = (await clientResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var resp = await Client.PostAsJsonAsync("/api/admin/projects", new { clientId, name, designType = "LOGO", dueDate = Iso(due) });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task CancelProjectAsync(Guid projectId) =>
        (await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/status", new { to = "CANCELLED", reason = "khách huỷ" })).EnsureSuccessStatusCode();

    [Fact]
    public async Task Project_deadline_is_a_marker_and_cancelled_projects_are_skipped_without_a_project_filter()
    {
        await LoginAsync();
        var due = TodayVn.AddDays(4);
        var live = await CreateProjectWithDueAsync("Dự án sống " + Guid.NewGuid().ToString("N")[..6], due);
        var cancelled = await CreateProjectWithDueAsync("Dự án huỷ " + Guid.NewGuid().ToString("N")[..6], due);
        (await Client.PostAsJsonAsync($"/api/admin/projects/{cancelled}/deliverables",
            new { name = "Huỷ-logo", quantity = 1, includedRounds = 1, extraRoundPrice = 0, dueDate = Iso(due) })).EnsureSuccessStatusCode();
        var cancelledTask = (await CreateTaskAsync(cancelled, new { title = "huỷ", dueDate = Iso(due) })).GetProperty("id").GetGuid();
        await CancelProjectAsync(cancelled);

        var all = await CalendarAsync(Iso(TodayVn), Iso(TodayVn.AddDays(6)));

        var markers = all.GetProperty("markers").EnumerateArray().ToList();
        markers.Should().Contain(m => m.GetProperty("kind").GetString() == "PROJECT" && m.GetProperty("id").GetGuid() == live
            && m.GetProperty("date").GetString() == Iso(due));
        markers.Should().NotContain(m => m.GetProperty("projectId").GetGuid() == cancelled);
        all.GetProperty("tasks").EnumerateArray().Should().NotContain(t => t.GetProperty("id").GetGuid() == cancelledTask);

        // an explicit projectId shows that project's tasks and deliverable markers, like the task list (BR-82)
        var one = await CalendarAsync(Iso(TodayVn), Iso(TodayVn.AddDays(6)), cancelled);
        one.GetProperty("tasks").EnumerateArray().Should().Contain(t => t.GetProperty("id").GetGuid() == cancelledTask);
        one.GetProperty("markers").EnumerateArray().Should().Contain(m => m.GetProperty("title").GetString() == "Huỷ-logo");
        one.GetProperty("truncated").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task More_than_500_tasks_sets_truncated()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO tasks (id, project_id, title, status, priority, sort_order, due_date, created_at, updated_at) " +
                "SELECT gen_random_uuid(), {0}, 'bulk ' || g, 'TODO', 'LOW', g, {1}, now(), now() FROM generate_series(1, 501) g",
                projectId, TodayVn);
        }

        var cal = await CalendarAsync(Iso(TodayVn), Iso(TodayVn), projectId);

        cal.GetProperty("tasks").GetArrayLength().Should().Be(500);
        cal.GetProperty("truncated").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Reschedule_to_the_same_date_changes_nothing_and_logs_nothing()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var id = (await CreateTaskAsync(projectId, new { title = "x", dueDate = Iso(TodayVn) })).GetProperty("id").GetGuid();

        (await Client.PutAsJsonAsync($"/api/admin/tasks/{id}/due-date", new { dueDate = Iso(TodayVn) })).StatusCode.Should().Be(HttpStatusCode.OK);

        (await ActionsAsync(projectId)).Should().NotContain("task.rescheduled");
    }

    [Fact]
    public async Task Reschedule_refuses_done_tasks_and_tasks_of_cancelled_projects()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var done = (await CreateTaskAsync(projectId, new { title = "xong", status = "DONE", dueDate = Iso(TodayVn) })).GetProperty("id").GetGuid();
        var open = (await CreateTaskAsync(projectId, new { title = "mở", dueDate = Iso(TodayVn) })).GetProperty("id").GetGuid();

        var doneResp = await Client.PutAsJsonAsync($"/api/admin/tasks/{done}/due-date", new { dueDate = Iso(TodayVn.AddDays(3)) });
        doneResp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await doneResp.Content.ReadAsStringAsync()).Should().Contain("INVALID_TRANSITION");

        await CancelProjectAsync(projectId);
        (await Client.PutAsJsonAsync($"/api/admin/tasks/{open}/due-date", new { dueDate = Iso(TodayVn.AddDays(3)) }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ActionsAsync(projectId)).Should().NotContain("task.rescheduled");
    }

    [Theory]
    [InlineData("2026-11-10", "2026-11-01")]   // from > to
    [InlineData("2026-01-01", "2026-06-01")]   // > 62 days
    [InlineData("abc", "2026-11-01")]
    public async Task Invalid_ranges_return_400(string from, string to)
    {
        await LoginAsync();
        var resp = await Client.GetAsync($"/api/admin/tasks/calendar?from={from}&to={to}");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reschedule_moves_due_and_shifts_start_and_logs()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var id = (await CreateTaskAsync(projectId, new { title = "x", startDate = Iso(TodayVn), dueDate = Iso(TodayVn.AddDays(2)) })).GetProperty("id").GetGuid();

        var resp = await Client.PutAsJsonAsync($"/api/admin/tasks/{id}/due-date", new { dueDate = Iso(TodayVn.AddDays(10)) });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await resp.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("dueDate").GetString().Should().Be(Iso(TodayVn.AddDays(10)));
        detail.GetProperty("startDate").GetString().Should().Be(Iso(TodayVn.AddDays(8)));
        (await ActionsAsync(projectId)).Should().Contain("task.rescheduled");
    }

    [Fact]
    public async Task Reschedule_requires_a_date_and_an_existing_task()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var id = (await CreateTaskAsync(projectId, new { title = "x" })).GetProperty("id").GetGuid();

        (await Client.PutAsJsonAsync($"/api/admin/tasks/{id}/due-date", new { })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.PutAsJsonAsync($"/api/admin/tasks/{Guid.NewGuid()}/due-date", new { dueDate = Iso(TodayVn) })).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
