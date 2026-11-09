using System.Net;
using System.Text.Json;

namespace FreelanceFlow.IntegrationTests.Tasks;

public class TaskLinksTests(AppFactory factory) : TaskTestBase(factory)
{
    [Fact]
    public async Task Links_are_saved_trimmed_and_never_client_visible()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var created = await CreateTaskAsync(projectId, new
        {
            title = "x",
            links = new[] { new { title = " Figma ", url = "https://figma.com/file/1", visibleToClient = true } },
        });

        var link = created.GetProperty("links")[0];
        link.GetProperty("title").GetString().Should().Be("Figma");
        link.GetProperty("visibleToClient").GetBoolean().Should().BeFalse();
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://x.com/a")]
    [InlineData("hello")]
    [InlineData("data:text/html,x")]
    [InlineData("https://x.com/a b")]
    public async Task Non_http_urls_return_400(string url)
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var resp = await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/tasks",
            new { title = "x", links = new[] { new { title = "l", url } } });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("links");
    }

    [Fact]
    public async Task Update_with_null_links_keeps_them_and_empty_array_clears()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var id = (await CreateTaskAsync(projectId, new { title = "x", links = new[] { new { title = "a", url = "https://x.com/a" } } })).GetProperty("id").GetGuid();

        var kept = await (await Client.PutAsJsonAsync($"/api/admin/tasks/{id}", new { title = "x" })).Content.ReadFromJsonAsync<JsonElement>();
        kept.GetProperty("links").GetArrayLength().Should().Be(1);

        var cleared = await (await Client.PutAsJsonAsync($"/api/admin/tasks/{id}", new { title = "x", links = Array.Empty<object>() })).Content.ReadFromJsonAsync<JsonElement>();
        cleared.GetProperty("links").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Activity_records_the_link_count_but_never_the_url()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var id = (await CreateTaskAsync(projectId, new { title = "x" })).GetProperty("id").GetGuid();
        await Client.PutAsJsonAsync($"/api/admin/tasks/{id}", new { title = "x", links = new[] { new { title = "a", url = "https://secret.example/token123" } } });

        var page = await Client.GetStringAsync($"/api/admin/projects/{projectId}/activity?pageSize=100");

        page.Should().Contain("task.links_updated");
        page.Should().NotContain("token123");
    }
}
