using System.Net;
using System.Text.Json;

namespace FreelanceFlow.IntegrationTests.Portal;

// TSK-05 / POR-06: tasks are designer-internal — no portal response may carry them or their activity.
public class PortalTaskPrivacyTests(AppFactory factory) : PortalTestBase(factory)
{
    [Fact]
    public async Task Portal_never_shows_tasks_or_task_activity()
    {
        await LoginAsync();
        var (clientId, token) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "Du an co task");
        var create = await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/tasks",
            new { title = "SECRET-TASK-TITLE", description = "SECRET-TASK-DESC" });
        create.EnsureSuccessStatusCode();
        var taskId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await Client.PostAsJsonAsync($"/api/admin/tasks/{taskId}/move", new { status = "DONE", sortOrder = 1 }))
            .EnsureSuccessStatusCode();
        var portal = await OpenPortalSessionAsync(token);

        foreach (var url in new[]
                 {
                     "/api/portal/projects",
                     $"/api/portal/projects/{projectId}",
                     $"/api/portal/projects/{projectId}/timeline",
                 })
        {
            var resp = await portal.GetAsync(url);
            resp.StatusCode.Should().Be(HttpStatusCode.OK, url);
            var body = await resp.Content.ReadAsStringAsync();
            body.Should().NotContain("SECRET-TASK", url);
            body.Should().NotContain("task.", url);
        }
    }
}
