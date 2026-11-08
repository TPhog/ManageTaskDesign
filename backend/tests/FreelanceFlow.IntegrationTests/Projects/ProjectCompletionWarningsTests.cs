using System.Net;
using System.Text.Json;
using FreelanceFlow.IntegrationTests.Tasks;

namespace FreelanceFlow.IntegrationTests.Projects;

/// <summary>P14 DF-08: completing a project lists the open tasks and the unfinished revision items (warn, don't block).</summary>
public class ProjectCompletionWarningsTests(AppFactory factory) : TaskTestBase(factory)
{
    private async Task<uint> RowVersionAsync(Guid projectId) =>
        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}")).GetProperty("rowVersion").GetUInt32();

    private async Task<HttpResponseMessage> ChangeStatusAsync(Guid projectId, string to, bool confirm = false) =>
        await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/status",
            new { to, rowVersion = await RowVersionAsync(projectId), confirmWarnings = confirm });

    /// <summary>Project IN_PROGRESS with "Logo" DELIVERED and four TODO tasks T1..T4.</summary>
    private async Task<(Guid ProjectId, List<Guid> TaskIds)> RunningProjectWithOpenTasksAsync()
    {
        var projectId = await CreateProjectAsync();
        var logo = await CreateDeliverableAsync(projectId, "Logo");
        foreach (var to in new[] { "IN_PROGRESS", "IN_REVIEW", "APPROVED", "DELIVERED" })
            (await Client.PostAsJsonAsync($"/api/admin/deliverables/{logo}/status", new { to })).EnsureSuccessStatusCode();
        var taskIds = new List<Guid>();
        foreach (var title in new[] { "T1", "T2", "T3", "T4" })
            taskIds.Add((await CreateTaskAsync(projectId, new { title })).GetProperty("id").GetGuid());
        (await ChangeStatusAsync(projectId, "IN_PROGRESS", confirm: true)).EnsureSuccessStatusCode();
        return (projectId, taskIds);
    }

    private static async Task<List<string>> WarningsAsync(HttpResponseMessage resp) =>
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("warnings")
            .EnumerateArray().Select(w => w.GetString()!).ToList();

    [Fact]
    public async Task Completing_with_open_tasks_lists_the_first_three_and_counts_the_rest()
    {
        await LoginAsync();
        var (projectId, _) = await RunningProjectWithOpenTasksAsync();

        var resp = await ChangeStatusAsync(projectId, "COMPLETED");

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var warnings = await WarningsAsync(resp);
        warnings.Should().Contain("Còn 4 công việc chưa xong: T1, T2, T3, … (+1)");
        warnings.Should().NotContain(w => w.Contains("mục sửa"));
    }

    [Fact]
    public async Task Completing_after_every_task_is_done_has_no_open_task_warning()
    {
        await LoginAsync();
        var (projectId, taskIds) = await RunningProjectWithOpenTasksAsync();
        for (var i = 0; i < taskIds.Count; i++)
            await MoveAsync(taskIds[i], "DONE", 1024.0 * (i + 1));

        var resp = await ChangeStatusAsync(projectId, "COMPLETED");

        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Completing_with_open_revision_items_counts_them()
    {
        await LoginAsync();
        var (projectId, taskIds) = await RunningProjectWithOpenTasksAsync();
        for (var i = 0; i < taskIds.Count; i++)
            await MoveAsync(taskIds[i], "DONE", 1024.0 * (i + 1));
        var logo = (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}"))
            .GetProperty("deliverables")[0].GetProperty("id").GetGuid();
        var created = await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/requests",
            new { content = "Sửa logo", source = "ZALO", deliverableId = logo });
        created.EnsureSuccessStatusCode();
        var requestId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("request").GetProperty("id").GetGuid();
        var rv = (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/requests/{requestId}"))
            .GetProperty("request").GetProperty("rowVersion").GetUInt32();
        var triage = await Client.PostAsJsonAsync($"/api/admin/requests/{requestId}/triage", new
        {
            rounds = new[]
            {
                new
                {
                    deliverableId = (Guid?)logo, receivedAt = (string?)null,
                    items = new[]
                    {
                        new { description = "Đổi màu", type = "MINOR", billing = (string?)"IN_QUOTA" },
                        new { description = "Đổi font", type = "MINOR", billing = (string?)"IN_QUOTA" },
                    },
                },
            },
            rowVersion = rv,
        });
        triage.StatusCode.Should().Be(HttpStatusCode.OK, await triage.Content.ReadAsStringAsync());

        var resp = await ChangeStatusAsync(projectId, "COMPLETED");

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var warnings = await WarningsAsync(resp);
        warnings.Should().Contain("Còn 2 mục sửa chưa xong (Cần làm / Đang làm)");
        warnings.Should().NotContain(w => w.Contains("công việc chưa xong"));
    }
}
