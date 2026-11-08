using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Domain.Tasks;

namespace FreelanceFlow.IntegrationTests.Tasks;

public class TasksTests(AppFactory factory) : TaskTestBase(factory)
{
    [Fact]
    public async Task Create_returns_201_with_defaults_and_logs_activity()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();

        var task = await CreateTaskAsync(projectId, new { title = "  Phác thảo logo  " });

        task.GetProperty("title").GetString().Should().Be("Phác thảo logo");
        task.GetProperty("status").GetString().Should().Be("TODO");
        task.GetProperty("priority").GetString().Should().Be("MEDIUM");
        task.GetProperty("description").GetString().Should().BeEmpty();
        task.GetProperty("projectId").GetGuid().Should().Be(projectId);
        task.GetProperty("projectName").GetString().Should().Be("Task Project");
        task.GetProperty("checklist").GetArrayLength().Should().Be(0);
        task.GetProperty("completedAt").ValueKind.Should().Be(JsonValueKind.Null);
        (await ActionsAsync(projectId)).Should().Contain("task.created");
    }

    [Fact]
    public async Task Create_appends_each_task_to_the_bottom_of_its_column()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();

        var first = await CreateTaskAsync(projectId, new { title = "A" });
        var second = await CreateTaskAsync(projectId, new { title = "B" });
        var doing = await CreateTaskAsync(projectId, new { title = "C", status = "IN_PROGRESS" });

        first.GetProperty("sortOrder").GetDouble().Should().Be(ProjectTask.SortStep);
        second.GetProperty("sortOrder").GetDouble().Should().Be(2 * ProjectTask.SortStep);
        doing.GetProperty("status").GetString().Should().Be("IN_PROGRESS");
        doing.GetProperty("sortOrder").GetDouble().Should().Be(ProjectTask.SortStep);
    }

    [Fact]
    public async Task Create_on_unknown_project_returns_404()
    {
        await LoginAsync();

        var resp = await Client.PostAsJsonAsync($"/api/admin/projects/{Guid.NewGuid()}/tasks", new { title = "X" });

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_with_invalid_fields_returns_400_with_field_errors()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();

        var resp = await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/tasks",
            new { title = "   ", status = "DOING", priority = "SOON" });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        errors.TryGetProperty("title", out _).Should().BeTrue();
        errors.TryGetProperty("status", out _).Should().BeTrue();
        errors.TryGetProperty("priority", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Deliverable_must_belong_to_the_same_project()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var otherProjectId = await CreateProjectAsync("Other Project");
        var own = await CreateDeliverableAsync(projectId, "Logo chính");
        var foreign = await CreateDeliverableAsync(otherProjectId, "Banner");

        var bad = await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/tasks",
            new { title = "X", deliverableId = foreign });
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await bad.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors")
            .TryGetProperty("deliverableId", out _).Should().BeTrue();

        var ok = await CreateTaskAsync(projectId, new { title = "Y", deliverableId = own });
        ok.GetProperty("deliverableId").GetGuid().Should().Be(own);
        ok.GetProperty("deliverableName").GetString().Should().Be("Logo chính");
    }

    [Fact]
    public async Task Unknown_task_returns_404_on_every_route()
    {
        await LoginAsync();
        var id = Guid.NewGuid();

        (await Client.GetAsync($"/api/admin/tasks/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.PutAsJsonAsync($"/api/admin/tasks/{id}", new { title = "X" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.PostAsJsonAsync($"/api/admin/tasks/{id}/move", new { status = "DONE", sortOrder = 1 }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.DeleteAsync($"/api/admin/tasks/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_returns_the_task_detail()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var id = (await CreateTaskAsync(projectId, new { title = "Chi tiết", description = "Nội dung" }))
            .GetProperty("id").GetGuid();

        var task = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks/{id}");

        task.GetProperty("title").GetString().Should().Be("Chi tiết");
        task.GetProperty("description").GetString().Should().Be("Nội dung");
        task.GetProperty("isOverdue").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Update_saves_fields_and_a_status_change_moves_the_card_to_the_bottom_of_its_new_column()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        await CreateTaskAsync(projectId, new { title = "Đang làm sẵn", status = "IN_PROGRESS" });
        var id = (await CreateTaskAsync(projectId, new { title = "Bản 1" })).GetProperty("id").GetGuid();
        var today = TodayVn;

        var resp = await Client.PutAsJsonAsync($"/api/admin/tasks/{id}", new
        {
            title = "Bản 2",
            description = "Ghi chú nội bộ",
            status = "IN_PROGRESS",
            priority = "URGENT",
            startDate = today,
            dueDate = today.AddDays(2),
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var task = await resp.Content.ReadFromJsonAsync<JsonElement>();
        task.GetProperty("title").GetString().Should().Be("Bản 2");
        task.GetProperty("description").GetString().Should().Be("Ghi chú nội bộ");
        task.GetProperty("priority").GetString().Should().Be("URGENT");
        task.GetProperty("status").GetString().Should().Be("IN_PROGRESS");
        task.GetProperty("sortOrder").GetDouble().Should().Be(2 * ProjectTask.SortStep);
        task.GetProperty("startDate").GetString().Should().Be(Iso(today));
        task.GetProperty("dueDate").GetString().Should().Be(Iso(today.AddDays(2)));
        (await ActionsAsync(projectId)).Should().Contain("task.updated").And.Contain("task.status_changed");
    }

    [Fact]
    public async Task Update_without_status_and_priority_keeps_them()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var id = (await CreateTaskAsync(projectId, new { title = "Giữ nguyên", status = "REVIEW", priority = "HIGH" }))
            .GetProperty("id").GetGuid();

        var resp = await Client.PutAsJsonAsync($"/api/admin/tasks/{id}", new { title = "Đổi tên" });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var task = await resp.Content.ReadFromJsonAsync<JsonElement>();
        task.GetProperty("title").GetString().Should().Be("Đổi tên");
        task.GetProperty("status").GetString().Should().Be("REVIEW");
        task.GetProperty("priority").GetString().Should().Be("HIGH");
        (await ActionsAsync(projectId)).Should().NotContain("task.status_changed");
    }

    [Fact]
    public async Task Move_to_done_stamps_completedAt_and_moving_back_clears_it()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var id = (await CreateTaskAsync(projectId, new { title = "Xuất file" })).GetProperty("id").GetGuid();

        var done = await MoveAsync(id, "DONE", 512.5);

        done.GetProperty("status").GetString().Should().Be("DONE");
        done.GetProperty("sortOrder").GetDouble().Should().Be(512.5);
        done.GetProperty("completedAt").ValueKind.Should().Be(JsonValueKind.String);
        (await ActionsAsync(projectId)).Should().Contain("task.status_changed");

        var reopened = await MoveAsync(id, "TODO", 10);
        reopened.GetProperty("completedAt").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Move_within_the_same_column_only_reorders()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var id = (await CreateTaskAsync(projectId, new { title = "Sắp xếp" })).GetProperty("id").GetGuid();

        var moved = await MoveAsync(id, "TODO", 1.5);

        moved.GetProperty("sortOrder").GetDouble().Should().Be(1.5);
        (await ActionsAsync(projectId)).Should().NotContain("task.status_changed");
    }

    [Fact]
    public async Task Move_with_invalid_body_returns_400()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var id = (await CreateTaskAsync(projectId, new { title = "Sai" })).GetProperty("id").GetGuid();

        var badStatus = await Client.PostAsJsonAsync($"/api/admin/tasks/{id}/move", new { status = "NOPE", sortOrder = 1 });
        badStatus.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await badStatus.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors")
            .TryGetProperty("status", out _).Should().BeTrue();

        var noOrder = await Client.PostAsJsonAsync($"/api/admin/tasks/{id}/move", new { status = "DONE" });
        noOrder.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await noOrder.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors")
            .TryGetProperty("sortOrder", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Delete_removes_the_task_and_logs_activity()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var id = (await CreateTaskAsync(projectId, new { title = "Xoá tôi" })).GetProperty("id").GetGuid();

        var resp = await Client.DeleteAsync($"/api/admin/tasks/{id}");

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.GetAsync($"/api/admin/tasks/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ActionsAsync(projectId)).Should().Contain("task.deleted");
    }

    [Fact]
    public async Task List_by_project_returns_only_that_projects_tasks_with_names()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync("Dự án một");
        var otherProjectId = await CreateProjectAsync("Dự án hai");
        var mine = (await CreateTaskAsync(projectId, new { title = "Của dự án một" })).GetProperty("id").GetGuid();
        await CreateTaskAsync(otherProjectId, new { title = "Của dự án hai" });

        var page = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks?projectId={projectId}");

        page.GetProperty("total").GetInt32().Should().Be(1);
        var item = page.GetProperty("items")[0];
        item.GetProperty("id").GetGuid().Should().Be(mine);
        item.GetProperty("projectName").GetString().Should().Be("Dự án một");
        item.GetProperty("clientName").GetString().Should().StartWith("Task Client");
        item.GetProperty("checklistTotal").GetInt32().Should().Be(0);
        item.GetProperty("checklistDone").GetInt32().Should().Be(0);
        item.GetProperty("isOverdue").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Due_filters_use_the_vietnam_calendar_day_and_skip_done_tasks()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var today = TodayVn;
        var weekEnd = ProjectTask.EndOfWeek(today);

        async Task<Guid> Make(string title, DateOnly? due, string status = "TODO") =>
            (await CreateTaskAsync(projectId, new { title, dueDate = due, status })).GetProperty("id").GetGuid();

        var overdue = await Make("Quá hạn", today.AddDays(-1));
        var overdueButDone = await Make("Quá hạn nhưng xong", today.AddDays(-1), "DONE");
        var dueToday = await Make("Hôm nay", today);
        var dueThisWeek = await Make("Cuối tuần", weekEnd);
        var dueNextWeek = await Make("Tuần sau", weekEnd.AddDays(1));
        await Make("Không hạn", null);

        (await ListIdsAsync($"projectId={projectId}&due=overdue")).Should().BeEquivalentTo(new[] { overdue });
        // on Sundays weekEnd == today, so "Cuối tuần" is also due today
        var expectedToday = weekEnd == today ? new[] { dueToday, dueThisWeek } : new[] { dueToday };
        (await ListIdsAsync($"projectId={projectId}&due=today")).Should().BeEquivalentTo(expectedToday);
        var week = await ListIdsAsync($"projectId={projectId}&due=week");
        week.Should().BeEquivalentTo(new[] { dueToday, dueThisWeek });
        week.Should().NotContain(dueNextWeek);

        var all = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks?projectId={projectId}");
        all.GetProperty("total").GetInt32().Should().Be(6);
        var items = all.GetProperty("items").EnumerateArray().ToList();
        var overdueFlag = items.ToDictionary(i => i.GetProperty("id").GetGuid(), i => i.GetProperty("isOverdue").GetBoolean());
        overdueFlag[overdue].Should().BeTrue();
        overdueFlag[overdueButDone].Should().BeFalse();
        overdueFlag[dueToday].Should().BeFalse();
        items.Last().GetProperty("title").GetString().Should().Be("Không hạn"); // tasks without a due date come last
    }

    [Fact]
    public async Task Global_list_hides_tasks_of_cancelled_projects()
    {
        await LoginAsync();
        var activeProjectId = await CreateProjectAsync("Dự án đang chạy");
        var cancelledProjectId = await CreateProjectAsync("Dự án bị huỷ");
        var visible = (await CreateTaskAsync(activeProjectId, new { title = "Việc còn hiệu lực", dueDate = TodayVn }))
            .GetProperty("id").GetGuid();
        var hidden = (await CreateTaskAsync(cancelledProjectId, new { title = "Việc của dự án huỷ", dueDate = TodayVn }))
            .GetProperty("id").GetGuid();
        (await Client.PostAsJsonAsync($"/api/admin/projects/{cancelledProjectId}/status",
            new { to = "CANCELLED", reason = "Khách huỷ", confirmWarnings = true })).EnsureSuccessStatusCode();

        var global = await ListIdsAsync("due=today&pageSize=100");

        global.Should().Contain(visible);
        global.Should().NotContain(hidden); // BR-82
        (await ListIdsAsync($"projectId={cancelledProjectId}")).Should().Contain(hidden);
    }

    [Fact]
    public async Task List_with_unknown_filter_values_returns_400()
    {
        await LoginAsync();

        (await Client.GetAsync("/api/admin/tasks?due=tomorrow")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.GetAsync("/api/admin/tasks?status=DOING")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
