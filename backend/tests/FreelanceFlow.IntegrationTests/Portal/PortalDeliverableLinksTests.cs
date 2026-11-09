using System.Net;
using System.Text.Json;

namespace FreelanceFlow.IntegrationTests.Portal;

// P15 D6: only deliverable links flagged visibleToClient reach the portal; client B never sees client A's links.
public class PortalDeliverableLinksTests(AppFactory factory) : PortalTestBase(factory)
{
    private async Task SetLinksAsync(Guid deliverableId, object links)
    {
        var resp = await Client.PutAsJsonAsync($"/api/admin/deliverables/{deliverableId}/links", new { links });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Portal_shows_only_visible_links_without_the_flag()
    {
        await LoginAsync();
        var (clientId, token) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "Dự án có link");
        var deliverableId = await CreateDeliverableAsync(projectId, "Logo");
        await SetLinksAsync(deliverableId, new[]
        {
            new { title = "Bản xem trước Figma", url = "https://figma.com/proto/public", visibleToClient = true },
            new { title = "Nội bộ", url = "https://drive.example/SECRET-INTERNAL-LINK", visibleToClient = false },
        });
        var portal = await OpenPortalSessionAsync(token);

        var body = await portal.GetStringAsync($"/api/portal/projects/{projectId}");

        body.Should().Contain("https://figma.com/proto/public");
        body.Should().NotContain("SECRET-INTERNAL-LINK");
        body.Should().NotContain("visibleToClient");
    }

    [Fact]
    public async Task Client_B_cannot_see_links_of_client_A_deliverable()
    {
        await LoginAsync();
        var (clientA, _) = await CreateClientWithPortalLinkAsync();
        var (_, tokenB) = await CreateClientWithPortalLinkAsync();
        var projectA = await CreateProjectAsync(clientA, "A");
        var deliverableA = await CreateDeliverableAsync(projectA, "Logo");
        await SetLinksAsync(deliverableA, new[] { new { title = "A-link", url = "https://a.example/only-for-a", visibleToClient = true } });
        var portalB = await OpenPortalSessionAsync(tokenB);

        (await portalB.GetAsync($"/api/portal/projects/{projectA}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await portalB.GetStringAsync("/api/portal/projects")).Should().NotContain("only-for-a");
    }

    [Fact]
    public async Task Task_links_never_reach_the_portal()
    {
        await LoginAsync();
        var (clientId, token) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "P");
        await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/tasks",
            new { title = "t", links = new[] { new { title = "l", url = "https://x.example/TASK-LINK-SECRET", visibleToClient = true } } });
        var portal = await OpenPortalSessionAsync(token);

        (await portal.GetStringAsync($"/api/portal/projects/{projectId}")).Should().NotContain("TASK-LINK-SECRET");
    }

    [Fact]
    public async Task Admin_links_endpoint_validates_and_logs_count_only()
    {
        await LoginAsync();
        var (clientId, _) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "P");
        var deliverableId = await CreateDeliverableAsync(projectId, "Logo");

        (await Client.PutAsJsonAsync($"/api/admin/deliverables/{deliverableId}/links",
            new { links = new[] { new { title = "x", url = "javascript:alert(1)" } } })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.PutAsJsonAsync($"/api/admin/deliverables/{Guid.NewGuid()}/links", new { links = Array.Empty<object>() }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        await SetLinksAsync(deliverableId, new[] { new { title = "Drive", url = "https://drive.example/TOKEN-ABC", visibleToClient = false } });
        var detail = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}");
        detail.GetProperty("deliverables")[0].GetProperty("links")[0].GetProperty("visibleToClient").GetBoolean().Should().BeFalse();
        (await Client.GetStringAsync($"/api/admin/projects/{projectId}/activity?pageSize=100")).Should().NotContain("TOKEN-ABC");
    }
}
