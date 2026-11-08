using System.Net;
using System.Text.Json;

namespace FreelanceFlow.IntegrationTests.Tasks;

/// <summary>P14 DF-06: a task's due date may not be before its start date.</summary>
public class TaskDatesTests(AppFactory factory) : TaskTestBase(factory)
{
    [Fact]
    public async Task Create_rejects_due_before_start_with_400()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var resp = await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/tasks",
            new { title = "Phác thảo", startDate = Iso(TodayVn.AddDays(1)), dueDate = Iso(TodayVn) });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").GetProperty("dueDate")[0].GetString()
            .Should().Be("Hạn hoàn thành phải từ ngày bắt đầu trở đi");
    }

    [Fact]
    public async Task Update_rejects_due_before_start_but_allows_same_day_and_open_ended()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var id = (await CreateTaskAsync(projectId, new { title = "Moodboard" })).GetProperty("id").GetGuid();

        var bad = await Client.PutAsJsonAsync($"/api/admin/tasks/{id}",
            new { title = "Moodboard", startDate = Iso(TodayVn.AddDays(2)), dueDate = Iso(TodayVn) });
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var same = await Client.PutAsJsonAsync($"/api/admin/tasks/{id}",
            new { title = "Moodboard", startDate = Iso(TodayVn), dueDate = Iso(TodayVn) });
        same.StatusCode.Should().Be(HttpStatusCode.OK);

        var onlyDue = await Client.PutAsJsonAsync($"/api/admin/tasks/{id}",
            new { title = "Moodboard", dueDate = Iso(TodayVn.AddDays(-3)) });
        onlyDue.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
