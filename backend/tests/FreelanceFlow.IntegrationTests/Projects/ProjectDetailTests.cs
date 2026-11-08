using System.Net;
using System.Text.Json;

namespace FreelanceFlow.IntegrationTests.Projects;

public class ProjectDetailTests(AppFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task GetProject_includes_latest_brief()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();

        var detail = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}");

        var brief = detail.GetProperty("brief");
        brief.GetProperty("version").GetInt32().Should().Be(1);
        brief.GetProperty("status").GetString().Should().Be("DRAFT");
        brief.GetProperty("content").GetString().Should().NotBeNull();
    }

    [Fact]
    public async Task GetProject_returns_newest_brief_version()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var v1 = (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}"))
            .GetProperty("brief").GetProperty("id").GetGuid();

        (await Client.PutAsJsonAsync($"/api/admin/briefs/{v1}", new { content = "Brief v1" })).EnsureSuccessStatusCode();
        (await Client.PostAsync($"/api/admin/briefs/{v1}/confirm", null)).EnsureSuccessStatusCode();
        (await Client.PostAsync($"/api/admin/projects/{projectId}/briefs", null)).EnsureSuccessStatusCode();

        var detail = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}");
        detail.GetProperty("brief").GetProperty("version").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Confirming_an_empty_brief_returns_422_and_keeps_it_draft()   // QA D-08
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var v1 = (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}"))
            .GetProperty("brief").GetProperty("id").GetGuid();

        var resp = await Client.PostAsync($"/api/admin/briefs/{v1}/confirm", null);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("BUSINESS_RULE");
        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}"))
            .GetProperty("brief").GetProperty("status").GetString().Should().Be("DRAFT");
    }

    private async Task<Guid> CreateProjectAsync()
    {
        var clientResp = await Client.PostAsJsonAsync("/api/admin/clients", new { name = "Brief Client " + Guid.NewGuid() });
        clientResp.EnsureSuccessStatusCode();
        var clientId = (await clientResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var projectResp = await Client.PostAsJsonAsync("/api/admin/projects",
            new { clientId, name = "Brief Project", designType = "LOGO" });
        projectResp.EnsureSuccessStatusCode();
        return (await projectResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
}
