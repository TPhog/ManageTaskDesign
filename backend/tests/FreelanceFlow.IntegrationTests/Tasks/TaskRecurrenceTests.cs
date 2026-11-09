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
    public async Task Two_concurrent_done_moves_spawn_once_and_the_loser_gets_409()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        // several tasks, each finished twice at once: the race may or may not happen on a given pair, never a double spawn
        const int tasks = 5;
        for (var i = 0; i < tasks; i++)
        {
            var id = (await CreateTaskAsync(projectId, new { title = "Song song " + i, recurrence = Daily })).GetProperty("id").GetGuid();
            var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
                Client.PostAsJsonAsync($"/api/admin/tasks/{id}/move", new { status = "DONE", sortOrder = 1 })));

            responses.Select(r => r.StatusCode).Should().OnlyContain(s => s == HttpStatusCode.OK || s == HttpStatusCode.Conflict);
            responses.Should().Contain(r => r.StatusCode == HttpStatusCode.OK);
            foreach (var r in responses.Where(r => r.StatusCode == HttpStatusCode.Conflict))
                (await r.Content.ReadAsStringAsync()).Should().Contain("CONCURRENCY_CONFLICT");
        }

        var page = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks?projectId={projectId}&pageSize=100");
        page.GetProperty("total").GetInt32().Should().Be(tasks * 2);
        page.GetProperty("items").EnumerateArray().GroupBy(i => i.GetProperty("title").GetString())
            .Should().OnlyContain(g => g.Count() == 2);
    }

    [Fact]
    public async Task A_second_spawn_for_the_same_source_is_refused_with_409_and_rolls_the_move_back()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var id = (await CreateTaskAsync(projectId, new { title = "Mất cờ", recurrence = Daily })).GetProperty("id").GetGuid();
        await MoveAsync(id, "DONE", 1);
        // what the losing request of a race sees: the flag still false although a next occurrence exists
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Tasks.Where(t => t.Id == id).ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, "TODO").SetProperty(t => t.RecurrenceSpawned, false));
        }

        var resp = await Client.PostAsJsonAsync($"/api/admin/tasks/{id}/move", new { status = "DONE", sortOrder = 1 });

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("CONCURRENCY_CONFLICT");
        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks/{id}")).GetProperty("status").GetString().Should().Be("TODO");
    }

    [Fact]
    public async Task Next_occurrence_of_a_task_on_a_closed_deliverable_stays_on_the_project_only()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var deliverableId = await CreateDeliverableAsync(projectId, "Logo");
        var id = (await CreateTaskAsync(projectId, new { title = "x", deliverableId, recurrence = Daily })).GetProperty("id").GetGuid();
        (await Client.PostAsJsonAsync($"/api/admin/deliverables/{deliverableId}/status", new { to = "CANCELLED" })).EnsureSuccessStatusCode();

        var nextId = (await MoveAsync(id, "DONE", 1)).GetProperty("nextOccurrence").GetProperty("id").GetGuid();

        var next = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks/{nextId}");
        next.GetProperty("deliverableId").ValueKind.Should().Be(JsonValueKind.Null);
        next.GetProperty("projectId").GetGuid().Should().Be(projectId);
    }

    [Fact]
    public async Task Next_occurrence_keeps_an_open_deliverable()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var deliverableId = await CreateDeliverableAsync(projectId, "Logo");
        var id = (await CreateTaskAsync(projectId, new { title = "x", deliverableId, recurrence = Daily })).GetProperty("id").GetGuid();

        var nextId = (await MoveAsync(id, "DONE", 1)).GetProperty("nextOccurrence").GetProperty("id").GetGuid();

        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks/{nextId}")).GetProperty("deliverableId").GetGuid().Should().Be(deliverableId);
    }

    [Theory]
    [InlineData("UPDATE tasks SET recurrence_kind = 'WEEKLY', recurrence_weekdays = 0 WHERE id = {0}")]
    [InlineData("UPDATE tasks SET recurrence_kind = 'WEEKLY', recurrence_weekdays = 128 WHERE id = {0}")]
    [InlineData("UPDATE tasks SET recurrence_kind = 'MONTHLY', recurrence_month_day = NULL WHERE id = {0}")]
    [InlineData("UPDATE tasks SET recurrence_kind = 'MONTHLY', recurrence_month_day = 32 WHERE id = {0}")]
    public async Task Database_rejects_an_incomplete_rule(string sql)
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var id = (await CreateTaskAsync(projectId, new { title = "x" })).GetProperty("id").GetGuid();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var act = () => db.Database.ExecuteSqlRawAsync(sql, id);

        (await act.Should().ThrowAsync<Npgsql.PostgresException>()).Which.ConstraintName.Should().Be("ck_tasks_recurrence_rule");
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
