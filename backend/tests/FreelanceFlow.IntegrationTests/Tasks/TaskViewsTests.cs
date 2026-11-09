using System.Net;
using System.Text.Json;

namespace FreelanceFlow.IntegrationTests.Tasks;

public class TaskViewsTests(AppFactory factory) : TaskTestBase(factory)
{
    private static object Config(string mode = "list", string groupBy = "due") => new
    {
        mode, groupBy, due = (string?)null, status = (string?)null, clientId = (Guid?)null, clientLabel = (string?)null,
        projectId = (Guid?)null, projectLabel = (string?)null, q = (string?)null, calendarUnit = "month",
    };

    [Fact]
    public async Task Create_list_update_delete_roundtrip()
    {
        await LoginAsync();
        var name = "Việc gấp " + Guid.NewGuid().ToString("N")[..6];

        var created = await Client.PostAsJsonAsync("/api/admin/task-views", new { name, config = Config(groupBy: "priority") });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var list = await Client.GetFromJsonAsync<JsonElement>("/api/admin/task-views");
        list.GetProperty("views").EnumerateArray().Should().Contain(v => v.GetProperty("id").GetGuid() == id
            && v.GetProperty("config").GetProperty("groupBy").GetString() == "priority");

        (await Client.PutAsJsonAsync($"/api/admin/task-views/{id}", new { name = name + " 2", config = Config("calendar", "none") })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.DeleteAsync($"/api/admin/task-views/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.DeleteAsync($"/api/admin/task-views/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Duplicate_name_ignoring_case_returns_409_and_bad_config_returns_400()
    {
        await LoginAsync();
        var name = "Dup " + Guid.NewGuid().ToString("N")[..6];
        (await Client.PostAsJsonAsync("/api/admin/task-views", new { name, config = Config() })).EnsureSuccessStatusCode();

        (await Client.PostAsJsonAsync("/api/admin/task-views", new { name = name.ToUpperInvariant(), config = Config() })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Client.PostAsJsonAsync("/api/admin/task-views", new { name = "", config = Config() })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.PostAsJsonAsync("/api/admin/task-views", new { name = "ok " + Guid.NewGuid(), config = Config("grid") })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Last_view_is_upserted_and_returned()
    {
        await LoginAsync();

        (await Client.PutAsJsonAsync("/api/admin/task-views/last", new { config = Config("calendar", "none") })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.PutAsJsonAsync("/api/admin/task-views/last", new { config = Config("list", "status") })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var last = (await Client.GetFromJsonAsync<JsonElement>("/api/admin/task-views")).GetProperty("last");
        last.GetProperty("mode").GetString().Should().Be("list");
        last.GetProperty("groupBy").GetString().Should().Be("status");
    }

    [Fact]
    public async Task Requires_authentication()
    {
        var anon = Factory.CreateClient();
        (await anon.GetAsync("/api/admin/task-views")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
