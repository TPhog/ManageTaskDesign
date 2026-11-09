using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Tasks;

public class TaskRecurrenceTests(AppFactory factory) : TaskTestBase(factory)
{
    private static object Daily => new { kind = "DAILY" };

    [Fact]
    public async Task Done_on_recurring_task_creates_next_occurrence_in_same_project_and_logs()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var created = await CreateTaskAsync(projectId, new { title = "Gửi báo cáo tháng", dueDate = Iso(TodayVn), recurrence = Daily });
        var id = created.GetProperty("id").GetGuid();
        var itemResp = await Client.PostAsJsonAsync($"/api/admin/tasks/{id}/checklist", new { text = "Xuất PDF" });
        itemResp.EnsureSuccessStatusCode();

        var done = await MoveAsync(id, "DONE", 1);

        var next = done.GetProperty("nextOccurrence");
        next.ValueKind.Should().Be(JsonValueKind.Object);
        next.GetProperty("dueDate").GetString().Should().Be(Iso(TodayVn.AddDays(1)));
        var nextId = next.GetProperty("id").GetGuid();
        var detail = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks/{nextId}");
        detail.GetProperty("title").GetString().Should().Be("Gửi báo cáo tháng");
        detail.GetProperty("status").GetString().Should().Be("TODO");
        detail.GetProperty("projectId").GetGuid().Should().Be(projectId);
        detail.GetProperty("recurrence").GetProperty("kind").GetString().Should().Be("DAILY");
        detail.GetProperty("recurrenceSourceId").GetGuid().Should().Be(id);
        detail.GetProperty("checklist").GetArrayLength().Should().Be(1);
        detail.GetProperty("checklist")[0].GetProperty("done").GetBoolean().Should().BeFalse();
        (await ActionsAsync(projectId)).Count(a => a == "task.created").Should().Be(2);
    }

    [Fact]
    public async Task Reopening_and_finishing_again_does_not_spawn_twice()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var id = (await CreateTaskAsync(projectId, new { title = "Backup", recurrence = Daily })).GetProperty("id").GetGuid();

        await MoveAsync(id, "DONE", 1);
        await MoveAsync(id, "TODO", 1);
        await MoveAsync(id, "DONE", 1);

        var titles = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks?projectId={projectId}");
        titles.GetProperty("items").EnumerateArray().Count(i => i.GetProperty("title").GetString() == "Backup").Should().Be(2);
    }

    [Fact]
    public async Task Done_via_drawer_update_also_spawns()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var id = (await CreateTaskAsync(projectId, new { title = "Đối soát", recurrence = Daily })).GetProperty("id").GetGuid();

        var resp = await Client.PutAsJsonAsync($"/api/admin/tasks/{id}", new { title = "Đối soát", status = "DONE" });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("nextOccurrence").ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task Weekly_without_weekdays_returns_400()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var resp = await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/tasks",
            new { title = "x", recurrence = new { kind = "WEEKLY", weekdays = 0 } });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("recurrence");
    }

    [Fact]
    public async Task Kind_none_clears_the_rule_and_null_keeps_it()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var id = (await CreateTaskAsync(projectId, new { title = "x", recurrence = Daily })).GetProperty("id").GetGuid();

        var kept = await (await Client.PutAsJsonAsync($"/api/admin/tasks/{id}", new { title = "x2" })).Content.ReadFromJsonAsync<JsonElement>();
        kept.GetProperty("recurrence").GetProperty("kind").GetString().Should().Be("DAILY");

        var cleared = await (await Client.PutAsJsonAsync($"/api/admin/tasks/{id}", new { title = "x2", recurrence = new { kind = "NONE" } })).Content.ReadFromJsonAsync<JsonElement>();
        cleared.GetProperty("recurrence").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Cancelled_project_does_not_spawn()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var id = (await CreateTaskAsync(projectId, new { title = "x", recurrence = Daily })).GetProperty("id").GetGuid();
        (await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/status", new { to = "CANCELLED", reason = "khách huỷ" })).EnsureSuccessStatusCode();

        var done = await MoveAsync(id, "DONE", 1);

        done.GetProperty("nextOccurrence").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Completed_project_does_not_spawn()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var id = (await CreateTaskAsync(projectId, new { title = "x", recurrence = Daily })).GetProperty("id").GetGuid();
        // Reaching COMPLETED through the API needs an accepted quotation and delivered work; the status alone matters here
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Projects.Where(p => p.Id == projectId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, "COMPLETED"));
        }

        var done = await MoveAsync(id, "DONE", 1);

        done.GetProperty("nextOccurrence").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Spawn_keeps_links_and_the_weekly_rule()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var id = (await CreateTaskAsync(projectId, new
        {
            title = "Báo cáo tuần",
            dueDate = Iso(TodayVn),
            recurrence = new { kind = "WEEKLY", weekdays = 127 },
            links = new[] { new { title = "Sheet", url = "https://docs.example/sheet" } },
        })).GetProperty("id").GetGuid();

        var nextId = (await MoveAsync(id, "DONE", 1)).GetProperty("nextOccurrence").GetProperty("id").GetGuid();

        var next = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks/{nextId}");
        next.GetProperty("links").GetArrayLength().Should().Be(1);
        next.GetProperty("recurrence").GetProperty("weekdays").GetInt32().Should().Be(127);
        next.GetProperty("dueDate").GetString().Should().Be(Iso(TodayVn.AddDays(1)));
    }
}
