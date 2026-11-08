using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Tasks;

public class TaskNotesTests(AppFactory factory) : TaskTestBase(factory)
{
    private async Task<(Guid ProjectId, Guid TaskId)> NewTaskAsync(object? body = null)
    {
        var projectId = await CreateProjectAsync("Notes Project");
        var task = await CreateTaskAsync(projectId, body ?? new { title = "Phác thảo logo" });
        return (projectId, task.GetProperty("id").GetGuid());
    }

    private async Task<JsonElement> AddNoteAsync(Guid taskId, string body)
    {
        var resp = await Client.PostAsJsonAsync($"/api/admin/tasks/{taskId}/notes", new { body });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<List<string>> NoteBodiesAsync(Guid taskId) =>
        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks/{taskId}/notes"))
            .EnumerateArray().Select(n => n.GetProperty("body").GetString()!).ToList();

    private async Task<JsonElement> ListItemAsync(Guid projectId, Guid taskId) =>
        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks?projectId={projectId}&pageSize=100"))
            .GetProperty("items").EnumerateArray().Single(t => t.GetProperty("id").GetGuid() == taskId);

    [Fact]
    public async Task Notes_are_listed_newest_first_and_counted_on_the_task_and_the_list()
    {
        await LoginAsync();
        var (projectId, taskId) = await NewTaskAsync();

        var first = await AddNoteAsync(taskId, "  Khách thích tông xanh  ");
        await AddNoteAsync(taskId, "Gửi phác thảo trước thứ 6");

        first.GetProperty("body").GetString().Should().Be("Khách thích tông xanh");
        (await NoteBodiesAsync(taskId)).Should().Equal("Gửi phác thảo trước thứ 6", "Khách thích tông xanh");
        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks/{taskId}")).GetProperty("notesCount").GetInt32().Should().Be(2);
        (await ListItemAsync(projectId, taskId)).GetProperty("notesCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task A_note_can_be_edited_and_deleted()
    {
        await LoginAsync();
        var (_, taskId) = await NewTaskAsync();
        var note = await AddNoteAsync(taskId, "Bản cũ");
        var noteId = note.GetProperty("id").GetGuid();

        var put = await Client.PutAsJsonAsync($"/api/admin/tasks/{taskId}/notes/{noteId}", new { body = "Bản mới" });

        put.StatusCode.Should().Be(HttpStatusCode.OK);
        var edited = await put.Content.ReadFromJsonAsync<JsonElement>();
        edited.GetProperty("body").GetString().Should().Be("Bản mới");
        edited.GetProperty("updatedAt").GetDateTimeOffset().Should().BeOnOrAfter(edited.GetProperty("createdAt").GetDateTimeOffset());

        (await Client.DeleteAsync($"/api/admin/tasks/{taskId}/notes/{noteId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await NoteBodiesAsync(taskId)).Should().BeEmpty();
        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks/{taskId}")).GetProperty("notesCount").GetInt32().Should().Be(0);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task A_blank_note_is_rejected(string? body)
    {
        await LoginAsync();
        var (_, taskId) = await NewTaskAsync();

        var resp = await Client.PostAsJsonAsync($"/api/admin/tasks/{taskId}/notes", new { body });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("body", out _).Should().BeTrue();
    }

    [Fact]
    public async Task A_note_over_5000_characters_is_rejected()
    {
        await LoginAsync();
        var (_, taskId) = await NewTaskAsync();

        var resp = await Client.PostAsJsonAsync($"/api/admin/tasks/{taskId}/notes", new { body = new string('x', 5001) });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Note_routes_are_scoped_to_their_task()
    {
        await LoginAsync();
        var (_, taskA) = await NewTaskAsync();
        var (_, taskB) = await NewTaskAsync();
        var noteOfA = (await AddNoteAsync(taskA, "Của A")).GetProperty("id").GetGuid();

        (await Client.PutAsJsonAsync($"/api/admin/tasks/{taskB}/notes/{noteOfA}", new { body = "B sửa" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.DeleteAsync($"/api/admin/tasks/{taskB}/notes/{noteOfA}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/api/admin/tasks/{Guid.NewGuid()}/notes")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.PostAsJsonAsync($"/api/admin/tasks/{Guid.NewGuid()}/notes", new { body = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await NoteBodiesAsync(taskA)).Should().Equal("Của A");
    }

    [Fact]
    public async Task Deleting_a_task_deletes_its_notes()
    {
        await LoginAsync();
        var (_, taskId) = await NewTaskAsync();
        await AddNoteAsync(taskId, "Sẽ mất theo task");

        (await Client.DeleteAsync($"/api/admin/tasks/{taskId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.TaskNotes.CountAsync(n => n.TaskId == taskId)).Should().Be(0);
    }

    [Fact]
    public async Task Checklist_description_is_saved_kept_on_toggle_and_replaced_when_sent()
    {
        await LoginAsync();
        var (_, taskId) = await NewTaskAsync();

        var add = await Client.PostAsJsonAsync($"/api/admin/tasks/{taskId}/checklist",
            new { text = "Chọn font", description = "Ưu tiên font không chân" });
        add.StatusCode.Should().Be(HttpStatusCode.Created);
        var item = (await add.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("checklist")[0];
        item.GetProperty("description").GetString().Should().Be("Ưu tiên font không chân");
        var itemId = item.GetProperty("id").GetGuid();

        var toggle = await Client.PutAsJsonAsync($"/api/admin/tasks/{taskId}/checklist/{itemId}", new { text = "Chọn font", done = true });
        var toggled = (await toggle.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("checklist")[0];
        toggled.GetProperty("done").GetBoolean().Should().BeTrue();
        toggled.GetProperty("description").GetString().Should().Be("Ưu tiên font không chân");

        var clear = await Client.PutAsJsonAsync($"/api/admin/tasks/{taskId}/checklist/{itemId}",
            new { text = "Chọn font", done = true, description = "" });
        (await clear.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("checklist")[0]
            .GetProperty("description").GetString().Should().BeEmpty();
    }

    [Fact]
    public async Task Checklist_description_over_5000_characters_is_rejected()
    {
        await LoginAsync();
        var (_, taskId) = await NewTaskAsync();

        var resp = await Client.PostAsJsonAsync($"/api/admin/tasks/{taskId}/checklist",
            new { text = "Chọn font", description = new string('x', 5001) });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("description", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Task_description_is_markdown_up_to_20000_characters_and_flags_the_list()
    {
        await LoginAsync();
        var (projectId, withText) = await NewTaskAsync(new { title = "Có mô tả", description = "**Moodboard**: https://pinterest.com/x" });
        var (plainProject, plain) = await NewTaskAsync();

        (await ListItemAsync(projectId, withText)).GetProperty("hasDescription").GetBoolean().Should().BeTrue();
        (await ListItemAsync(plainProject, plain)).GetProperty("hasDescription").GetBoolean().Should().BeFalse();

        var tooLong = await Client.PutAsJsonAsync($"/api/admin/tasks/{withText}",
            new { title = "Có mô tả", description = new string('x', 20_001) });
        tooLong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await tooLong.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("description", out _).Should().BeTrue();
    }
}
