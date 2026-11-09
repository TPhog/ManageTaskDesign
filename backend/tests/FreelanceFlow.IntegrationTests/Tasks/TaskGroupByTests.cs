using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Features.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Tasks;

public class TaskGroupByTests(AppFactory factory) : TaskTestBase(factory)
{
    [Fact]
    public async Task Group_by_due_orders_buckets_and_reports_total_counts_per_group()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var overdue = (await CreateTaskAsync(projectId, new { title = "o", dueDate = Iso(TodayVn.AddDays(-2)) })).GetProperty("id").GetGuid();
        var today = (await CreateTaskAsync(projectId, new { title = "t", dueDate = Iso(TodayVn) })).GetProperty("id").GetGuid();
        var none = (await CreateTaskAsync(projectId, new { title = "n" })).GetProperty("id").GetGuid();

        var page = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks?projectId={projectId}&groupBy=due");

        var keys = page.GetProperty("groups").EnumerateArray().Select(g => g.GetProperty("key").GetString()).ToList();
        keys.Should().ContainInOrder("overdue", "today", "none");
        page.GetProperty("groups")[0].GetProperty("label").GetString().Should().Be("Quá hạn");
        var items = page.GetProperty("items").EnumerateArray().ToList();
        items.Select(i => i.GetProperty("id").GetGuid()).Should().ContainInOrder(overdue, today, none);
        items[0].GetProperty("groupKey").GetString().Should().Be("overdue");
    }

    [Fact]
    public async Task Pagination_follows_group_order_and_group_counts_cover_all_pages()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        for (var i = 0; i < 3; i++) await CreateTaskAsync(projectId, new { title = "u" + i, priority = "URGENT" });
        for (var i = 0; i < 3; i++) await CreateTaskAsync(projectId, new { title = "l" + i, priority = "LOW" });

        var p2 = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks?projectId={projectId}&groupBy=priority&page=2&pageSize=4");

        p2.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("priority").GetString()).Should().OnlyContain(p => p == "LOW");
        p2.GetProperty("groups").EnumerateArray().Select(g => g.GetProperty("count").GetInt32()).Should().Equal(3, 3);
    }

    [Fact]
    public async Task Group_by_deliverable_uses_none_for_tasks_without_one()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var deliverableId = await CreateDeliverableAsync(projectId, "Logo");
        await CreateTaskAsync(projectId, new { title = "a", deliverableId });
        await CreateTaskAsync(projectId, new { title = "b" });

        var page = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks?projectId={projectId}&groupBy=deliverable");

        page.GetProperty("groups").EnumerateArray().Select(g => g.GetProperty("key").GetString())
            .Should().Equal(deliverableId.ToString(), "none");
    }

    [Fact]
    public async Task Group_by_project_orders_by_project_name_with_client_label()
    {
        await LoginAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var b = await CreateProjectAsync("B " + tag);
        var a = await CreateProjectAsync("A " + tag);
        await CreateTaskAsync(b, new { title = "b1" });
        await CreateTaskAsync(a, new { title = "a1" });
        await CreateTaskAsync(a, new { title = "a2" });

        var page = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks?q={tag}&groupBy=project");

        var groups = page.GetProperty("groups").EnumerateArray().ToList();
        groups.Select(g => g.GetProperty("key").GetString()).Should().Equal(a.ToString(), b.ToString());
        groups.Select(g => g.GetProperty("count").GetInt32()).Should().Equal(2, 1);
        groups[0].GetProperty("label").GetString().Should().StartWith("A " + tag + " · Task Client");
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("groupKey").GetString())
            .Should().Equal(a.ToString(), a.ToString(), b.ToString());
        page.GetProperty("truncated").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Group_by_status_follows_the_kanban_column_order()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        await CreateTaskAsync(projectId, new { title = "blocked", status = "BLOCKED" });
        await CreateTaskAsync(projectId, new { title = "done", status = "DONE" });
        await CreateTaskAsync(projectId, new { title = "todo" });
        await CreateTaskAsync(projectId, new { title = "review", status = "REVIEW" });

        var page = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks?projectId={projectId}&groupBy=status");

        page.GetProperty("groups").EnumerateArray().Select(g => g.GetProperty("key").GetString())
            .Should().Equal("TODO", "REVIEW", "DONE", "BLOCKED");
        page.GetProperty("groups")[0].GetProperty("label").GetString().Should().Be("Cần làm");
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("title").GetString())
            .Should().Equal("todo", "review", "done", "blocked");
    }

    [Fact]
    public async Task Above_the_cap_open_tasks_are_kept_and_truncated_is_set()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var open = (await CreateTaskAsync(projectId, new { title = "còn mở" })).GetProperty("id").GetGuid();
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO tasks (id, project_id, title, status, priority, sort_order, completed_at, created_at, updated_at) " +
                "SELECT gen_random_uuid(), {0}, 'xong ' || g, 'DONE', 'LOW', g, now(), now(), now() FROM generate_series(1, {1}) g",
                projectId, TasksEndpoints.MaxGroupedRows);
        }

        var page = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks?projectId={projectId}&groupBy=status");

        page.GetProperty("truncated").GetBoolean().Should().BeTrue();
        page.GetProperty("total").GetInt32().Should().Be(TasksEndpoints.MaxGroupedRows);
        page.GetProperty("groups")[0].GetProperty("key").GetString().Should().Be("TODO");
        page.GetProperty("items")[0].GetProperty("id").GetGuid().Should().Be(open);
    }

    [Fact]
    public async Task Without_group_by_response_is_unchanged_and_bad_value_is_400()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        await CreateTaskAsync(projectId, new { title = "a" });

        var page = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks?projectId={projectId}");
        page.GetProperty("groups").GetArrayLength().Should().Be(0);
        page.GetProperty("items")[0].GetProperty("groupKey").ValueKind.Should().Be(JsonValueKind.Null);

        (await Client.GetAsync("/api/admin/tasks?groupBy=colour")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
