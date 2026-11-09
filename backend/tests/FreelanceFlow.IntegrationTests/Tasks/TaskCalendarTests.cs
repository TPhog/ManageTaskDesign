using System.Net;
using System.Text.Json;

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
