using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Portal;

public class PortalIsolationTests(AppFactory factory) : PortalTestBase(factory)
{
    // docs/09-test-plan.md §4 — EVERY /api/portal route must be listed here, keyed "METHOD pattern".
    // Value: URL probed with GET ("{projectA}" = a project of client A), or null when the route is
    // public / carries no resource (covered by PortalSessionTests).
    private static readonly Dictionary<string, string?> RouteTable = new()
    {
        ["POST /api/portal/session"] = null,
        ["POST /api/portal/logout"] = null,
        ["GET /api/portal/me"] = "/api/portal/me",
        ["GET /api/portal/projects"] = "/api/portal/projects",
        ["GET /api/portal/projects/{id:guid}"] = "/api/portal/projects/{projectA}",
        ["GET /api/portal/projects/{id:guid}/timeline"] = "/api/portal/projects/{projectA}/timeline",
    };

    public static IEnumerable<object[]> GetUrls() =>
        RouteTable.Values.OfType<string>().Select(u => new object[] { u });

    public static IEnumerable<object[]> IdUrls() =>
        RouteTable.Values.OfType<string>().Where(u => u.Contains("{projectA}")).Select(u => new object[] { u });

    [Fact]
    public void Every_portal_route_is_declared_in_the_isolation_table()
    {
        var routes = Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/portal", StringComparison.Ordinal) == true)
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? new[] { "ANY" })
                .Select(m => $"{m} {e.RoutePattern.RawText!.TrimEnd('/')}"))
            .ToList();

        routes.Should().BeEquivalentTo(RouteTable.Keys,
            "every new /api/portal endpoint needs an isolation case in RouteTable (docs/09-test-plan.md §4)");
    }

    [Theory]
    [MemberData(nameof(IdUrls))]
    public async Task Client_B_gets_404_for_resources_of_client_A(string template)
    {
        var s = await ArrangeAsync();
        var url = template.Replace("{projectA}", s.ProjectA.ToString());

        (await s.PortalA.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK, "control: A reads its own resource");
        var resp = await s.PortalB.GetAsync(url);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().NotContain(s.ProjectAName).And.NotContain(s.DeliverableAName);
    }

    [Fact]
    public async Task Project_list_of_B_contains_nothing_of_A()
    {
        var s = await ArrangeAsync();

        var list = await s.PortalB.GetFromJsonAsync<JsonElement>("/api/portal/projects");

        var ids = list.EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).ToList();
        ids.Should().Contain(s.ProjectB).And.NotContain(s.ProjectA);
    }

    [Theory]
    [MemberData(nameof(GetUrls))]
    public async Task Portal_route_rejects_designer_cookie(string template)
    {
        var s = await ArrangeAsync(); // leaves the designer signed in on Client

        var resp = await Client.GetAsync(template.Replace("{projectA}", s.ProjectA.ToString()));

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(GetUrls))]
    public async Task Portal_response_does_not_expose_internal_fields(string template)
    {
        var s = await ArrangeAsync();

        var resp = await s.PortalA.GetAsync(template.Replace("{projectA}", s.ProjectA.ToString()));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        foreach (var forbidden in new[] { "\"internalNotes\"", "\"notes\"", "\"scopeOverrideReason\"", "SECRET-INTERNAL", "SECRET-CLIENT-NOTE" })
            body.Should().NotContain(forbidden);
    }

    private sealed record Scenario(
        HttpClient PortalA, HttpClient PortalB, Guid ProjectA, string ProjectAName, string DeliverableAName, Guid ProjectB);

    private async Task<Scenario> ArrangeAsync()
    {
        await LoginAsync();
        var (clientA, tokenA) = await CreateClientWithPortalLinkAsync();
        var (clientB, tokenB) = await CreateClientWithPortalLinkAsync();

        var projectAName = "ISOLATION-A-" + Guid.NewGuid().ToString("N");
        var projectA = await CreateProjectAsync(clientA, projectAName);
        var deliverableAName = "ISOLATION-DELIVERABLE-A-" + Guid.NewGuid().ToString("N");
        await CreateDeliverableAsync(projectA, deliverableAName);
        await ChangeProjectStatusAsync(projectA, new { to = "IN_PROGRESS", confirmWarnings = true }); // A gets a timeline entry
        (await Client.PostAsJsonAsync($"/api/admin/projects/{projectA}/progress-updates",
            new { message = $"Tiến độ {projectAName}" })).EnsureSuccessStatusCode(); // M5: designer progress update on A's timeline
        var projectB = await CreateProjectAsync(clientB, "ISOLATION-B-" + Guid.NewGuid().ToString("N"));

        return new Scenario(
            await OpenPortalSessionAsync(tokenA),
            await OpenPortalSessionAsync(tokenB),
            projectA, projectAName, deliverableAName, projectB);
    }
}
