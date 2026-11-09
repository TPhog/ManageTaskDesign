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
    public async Task Completion_warning_and_completed_flow_note_count_the_same_revision_items()
    {
        await LoginAsync();
        var (projectId, taskIds) = await RunningProjectWithOpenTasksAsync();
        for (var i = 0; i < taskIds.Count; i++)
            await MoveAsync(taskIds[i], "DONE", 1024.0 * (i + 1));
        var logo = (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}"))
            .GetProperty("deliverables")[0].GetProperty("id").GetGuid();
        var poster = await CreateDeliverableAsync(projectId, "Poster");
        (await Client.PostAsJsonAsync($"/api/admin/deliverables/{poster}/status", new { to = "IN_PROGRESS" })).EnsureSuccessStatusCode();
        await TriageAsync(projectId, logo, poster, null);   // one open item each: Logo, Poster, project-level round
        (await Client.PostAsJsonAsync($"/api/admin/deliverables/{poster}/status", new { to = "CANCELLED" })).EnsureSuccessStatusCode();

        var resp = await ChangeStatusAsync(projectId, "COMPLETED");

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await WarningsAsync(resp)).Should().Contain("Còn 2 mục sửa chưa xong (Cần làm / Đang làm)");   // Poster is cancelled
        (await ChangeStatusAsync(projectId, "COMPLETED", confirm: true)).EnsureSuccessStatusCode();
        var note = (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}/flow")).GetProperty("warnings")
            .EnumerateArray().Single(w => w.GetProperty("kind").GetString() == "COMPLETED_WITH_OPEN_ITEMS");
        note.GetProperty("message").GetString().Should()
            .Be("Dự án đã hoàn thành nhưng còn 2 mục sửa chưa xong — mở lại dự án nếu cần làm tiếp");
    }

    /// <summary>A designer request triaged into one round (one open MINOR item) per given deliverable (null = project-level).</summary>
    private async Task TriageAsync(Guid projectId, params Guid?[] deliverableIds)
    {
        var created = await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/requests",
            new { content = "Sửa nhiều chỗ", source = "ZALO" });
        created.EnsureSuccessStatusCode();
        var requestId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("request").GetProperty("id").GetGuid();
        var rv = (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/requests/{requestId}"))
            .GetProperty("request").GetProperty("rowVersion").GetUInt32();
        var triage = await Client.PostAsJsonAsync($"/api/admin/requests/{requestId}/triage", new
        {
            rounds = deliverableIds.Select(d => new
            {
                deliverableId = d, receivedAt = (string?)null,
                items = new[] { new { description = "Đổi màu", type = "MINOR", billing = (string?)(d == null ? "FREE_FIX" : "IN_QUOTA") } },
            }).ToArray(),
            rowVersion = rv,
        });
        triage.StatusCode.Should().Be(HttpStatusCode.OK, await triage.Content.ReadAsStringAsync());
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
