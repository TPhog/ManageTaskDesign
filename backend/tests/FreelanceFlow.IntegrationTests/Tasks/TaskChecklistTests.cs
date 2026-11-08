using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Tasks;

public class TaskChecklistTests(AppFactory factory) : TaskTestBase(factory)
{
    [Fact]
    public async Task Items_are_added_in_order_toggled_and_removed()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var taskId = await CreateTaskIdAsync(projectId);

        var first = await Client.PostAsJsonAsync($"/api/admin/tasks/{taskId}/checklist", new { text = " Phác thảo " });
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var afterSecond = await AddItemAsync(taskId, "Chốt màu");

        var items = afterSecond.GetProperty("checklist");
        items.GetArrayLength().Should().Be(2);
        items[0].GetProperty("text").GetString().Should().Be("Phác thảo");
        items[0].GetProperty("sortOrder").GetInt32().Should().Be(0);
        items[0].GetProperty("done").GetBoolean().Should().BeFalse();
        items[1].GetProperty("text").GetString().Should().Be("Chốt màu");
        items[1].GetProperty("sortOrder").GetInt32().Should().Be(1);
        var itemId = items[0].GetProperty("id").GetGuid();

        var toggled = await Client.PutAsJsonAsync($"/api/admin/tasks/{taskId}/checklist/{itemId}",
            new { text = "Phác thảo 3 hướng", done = true });
        toggled.StatusCode.Should().Be(HttpStatusCode.OK);
        var toggledItem = (await toggled.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("checklist")[0];
        toggledItem.GetProperty("text").GetString().Should().Be("Phác thảo 3 hướng");
        toggledItem.GetProperty("done").GetBoolean().Should().BeTrue();

        var row = (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks?projectId={projectId}"))
            .GetProperty("items")[0];
        row.GetProperty("checklistTotal").GetInt32().Should().Be(2);
        row.GetProperty("checklistDone").GetInt32().Should().Be(1);

        var removed = await Client.DeleteAsync($"/api/admin/tasks/{taskId}/checklist/{itemId}");
        removed.StatusCode.Should().Be(HttpStatusCode.OK);
        var left = (await removed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("checklist");
        left.GetArrayLength().Should().Be(1);
        left[0].GetProperty("text").GetString().Should().Be("Chốt màu");
    }

    [Fact]
    public async Task Invalid_body_returns_400()
    {
        await LoginAsync();
        var taskId = await CreateTaskIdAsync(await CreateProjectAsync());
        var itemId = (await AddItemAsync(taskId, "Phác thảo")).GetProperty("checklist")[0].GetProperty("id").GetGuid();

        var blank = await Client.PostAsJsonAsync($"/api/admin/tasks/{taskId}/checklist", new { text = "  " });
        blank.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await blank.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors")
            .TryGetProperty("text", out _).Should().BeTrue();

        var noDone = await Client.PutAsJsonAsync($"/api/admin/tasks/{taskId}/checklist/{itemId}", new { text = "X" });
        noDone.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await noDone.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors")
            .TryGetProperty("done", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Unknown_task_or_item_returns_404()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var taskId = await CreateTaskIdAsync(projectId);
        var otherTaskId = await CreateTaskIdAsync(projectId);
        var foreignItemId = (await AddItemAsync(otherTaskId, "Của task khác"))
            .GetProperty("checklist")[0].GetProperty("id").GetGuid();

        (await Client.PostAsJsonAsync($"/api/admin/tasks/{Guid.NewGuid()}/checklist", new { text = "X" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.PutAsJsonAsync($"/api/admin/tasks/{taskId}/checklist/{Guid.NewGuid()}", new { text = "X", done = true }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.PutAsJsonAsync($"/api/admin/tasks/{taskId}/checklist/{foreignItemId}", new { text = "X", done = true }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.DeleteAsync($"/api/admin/tasks/{taskId}/checklist/{foreignItemId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deleting_a_task_deletes_its_checklist()
    {
        await LoginAsync();
        var taskId = await CreateTaskIdAsync(await CreateProjectAsync());
        await AddItemAsync(taskId, "Phác thảo");

        (await Client.DeleteAsync($"/api/admin/tasks/{taskId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.TaskChecklistItems.CountAsync(i => i.TaskId == taskId)).Should().Be(0);
    }

    private async Task<Guid> CreateTaskIdAsync(Guid projectId) =>
        (await CreateTaskAsync(projectId, new { title = "Checklist task " + Guid.NewGuid() })).GetProperty("id").GetGuid();

    private async Task<JsonElement> AddItemAsync(Guid taskId, string text)
    {
        var resp = await Client.PostAsJsonAsync($"/api/admin/tasks/{taskId}/checklist", new { text });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }
}
