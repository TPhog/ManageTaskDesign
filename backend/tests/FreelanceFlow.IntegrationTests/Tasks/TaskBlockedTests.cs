using System.Net;
using System.Text.Json;

namespace FreelanceFlow.IntegrationTests.Tasks;

public class TaskBlockedTests(AppFactory factory) : TaskTestBase(factory)
{
    [Fact]
    public async Task Update_to_blocked_stores_reason_and_blocker_and_leaving_clears_them()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var blocker = (await CreateTaskAsync(projectId, new { title = "Chờ brief" })).GetProperty("id").GetGuid();
        var id = (await CreateTaskAsync(projectId, new { title = "Phác thảo" })).GetProperty("id").GetGuid();

        var resp = await Client.PutAsJsonAsync($"/api/admin/tasks/{id}",
            new { title = "Phác thảo", status = "BLOCKED", blocked = new { reason = "Chờ khách gửi logo", byTaskId = blocker } });

        var blocked = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("blocked");
        blocked.GetProperty("reason").GetString().Should().Be("Chờ khách gửi logo");
        blocked.GetProperty("byTaskTitle").GetString().Should().Be("Chờ brief");
        blocked.GetProperty("byTaskDone").GetBoolean().Should().BeFalse();

        var back = await MoveAsync(id, "IN_PROGRESS", 1);
        back.GetProperty("blocked").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Move_to_blocked_without_reason_is_allowed_BR80()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var id = (await CreateTaskAsync(projectId, new { title = "x" })).GetProperty("id").GetGuid();

        var moved = await MoveAsync(id, "BLOCKED", 1);

        moved.GetProperty("status").GetString().Should().Be("BLOCKED");
        moved.GetProperty("blocked").GetProperty("reason").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Move_accepts_blocked_reason()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var id = (await CreateTaskAsync(projectId, new { title = "x" })).GetProperty("id").GetGuid();

        var resp = await Client.PostAsJsonAsync($"/api/admin/tasks/{id}/move",
            new { status = "BLOCKED", sortOrder = 1, blocked = new { reason = "Chờ thanh toán cọc" } });

        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("blocked").GetProperty("reason").GetString().Should().Be("Chờ thanh toán cọc");
    }

    [Fact]
    public async Task Blocker_from_another_project_or_self_or_cycle_returns_400()
    {
        await LoginAsync();
        var p1 = await CreateProjectAsync("P1");
        var p2 = await CreateProjectAsync("P2");
        var a = (await CreateTaskAsync(p1, new { title = "A" })).GetProperty("id").GetGuid();
        var b = (await CreateTaskAsync(p1, new { title = "B" })).GetProperty("id").GetGuid();
        var other = (await CreateTaskAsync(p2, new { title = "O" })).GetProperty("id").GetGuid();

        async Task<HttpStatusCode> Block(Guid task, Guid by) => (await Client.PutAsJsonAsync($"/api/admin/tasks/{task}",
            new { title = "t", status = "BLOCKED", blocked = new { reason = "r", byTaskId = by } })).StatusCode;

        (await Block(a, other)).Should().Be(HttpStatusCode.BadRequest); // different project
        (await Block(a, a)).Should().Be(HttpStatusCode.BadRequest);     // self
        (await Block(a, b)).Should().Be(HttpStatusCode.OK);
        (await Block(b, a)).Should().Be(HttpStatusCode.BadRequest);     // cycle a<-b<-a
    }

    [Fact]
    public async Task A_cycle_longer_than_twenty_steps_is_still_refused()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var ids = new List<Guid>();
        for (var i = 0; i < 23; i++)
            ids.Add((await CreateTaskAsync(projectId, new { title = "c" + i })).GetProperty("id").GetGuid());

        async Task<HttpStatusCode> Block(Guid task, Guid by) => (await Client.PutAsJsonAsync($"/api/admin/tasks/{task}",
            new { title = "t", status = "BLOCKED", blocked = new { reason = "r", byTaskId = by } })).StatusCode;

        for (var i = 0; i < ids.Count - 1; i++)
            (await Block(ids[i], ids[i + 1])).Should().Be(HttpStatusCode.OK);   // c0 <- c1 <- … <- c22

        (await Block(ids[^1], ids[0])).Should().Be(HttpStatusCode.BadRequest);  // would close a 23-task cycle
    }

    [Fact]
    public async Task Deleting_the_blocker_keeps_reason_and_drops_the_link()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var blocker = (await CreateTaskAsync(projectId, new { title = "B" })).GetProperty("id").GetGuid();
        var id = (await CreateTaskAsync(projectId, new { title = "A" })).GetProperty("id").GetGuid();
        await Client.PutAsJsonAsync($"/api/admin/tasks/{id}", new { title = "A", status = "BLOCKED", blocked = new { reason = "r", byTaskId = blocker } });

        (await Client.DeleteAsync($"/api/admin/tasks/{blocker}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks/{id}");
        detail.GetProperty("blocked").GetProperty("reason").GetString().Should().Be("r");
        detail.GetProperty("blocked").GetProperty("byTaskId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task List_item_exposes_blocker_title_and_done_flag()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var blocker = (await CreateTaskAsync(projectId, new { title = "Chờ brief" })).GetProperty("id").GetGuid();
        var id = (await CreateTaskAsync(projectId, new { title = "A" })).GetProperty("id").GetGuid();
        await Client.PutAsJsonAsync($"/api/admin/tasks/{id}", new { title = "A", status = "BLOCKED", blocked = new { reason = "r", byTaskId = blocker } });
        await MoveAsync(blocker, "DONE", 1);

        var page = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks?projectId={projectId}&status=BLOCKED");
        var item = page.GetProperty("items")[0];
        item.GetProperty("blockedByTitle").GetString().Should().Be("Chờ brief");
        item.GetProperty("blockedByDone").GetBoolean().Should().BeTrue();
    }
}
