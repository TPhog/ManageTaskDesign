using System.Net;
using System.Text.Json;
using FreelanceFlow.IntegrationTests.Images;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Portal;

public class PortalIsolationTests(AppFactory factory) : PortalTestBase(factory)
{
    // docs/09-test-plan.md §4 — EVERY /api/portal route must be listed here, keyed "METHOD pattern".
    // Value: URL probed with GET ("{projectA}" / "{quotationA}" = a resource of client A), or null when the
    // route is public / carries no resource (PortalSessionTests) or is a POST covered by a dedicated test below.
    private static readonly Dictionary<string, string?> RouteTable = new()
    {
        ["POST /api/portal/session"] = null,
        ["POST /api/portal/logout"] = null,
        ["GET /api/portal/studio"] = null, // public studio contact, no resource (PortalSessionTests.Studio_contact_is_public…)
        ["GET /api/portal/me"] = "/api/portal/me",
        ["GET /api/portal/projects"] = "/api/portal/projects",
        ["GET /api/portal/projects/{id:guid}"] = "/api/portal/projects/{projectA}",
        ["GET /api/portal/projects/{id:guid}/timeline"] = "/api/portal/projects/{projectA}/timeline",
        ["GET /api/portal/projects/{id:guid}/revisions"] = "/api/portal/projects/{projectA}/revisions",
        ["GET /api/portal/quotations"] = "/api/portal/quotations?projectId={projectA}",
        ["GET /api/portal/quotations/{id:guid}"] = "/api/portal/quotations/{quotationA}",
        ["GET /api/portal/quotations/{id:guid}/versions/{version:int}"] = "/api/portal/quotations/{quotationA}/versions/1",
        ["GET /api/portal/quotations/{id:guid}/qr/{purpose}.png"] = "/api/portal/quotations/{quotationA}/qr/deposit.png",
        ["GET /api/portal/requests"] = "/api/portal/requests?projectId={projectA}",
        ["GET /api/portal/projects/{id:guid}/images"] = "/api/portal/projects/{projectA}/images",
        ["GET /api/portal/images/{id:guid}/{variant}"] = "/api/portal/images/{imageA}/thumb", // M3: never "original"
        ["POST /api/portal/projects/{id:guid}/requests"] = null, // Client_B_cannot_send_a_request_to_a_project_of_A
        ["POST /api/portal/deliverables/{id:guid}/approve"] = null, // PortalDeliverableApprovalTests.Client_B_gets_404_approving_a_deliverable_of_A
        ["POST /api/portal/quotations/{id:guid}/accept"] = null, // PortalQuotationStatementTests.Client_B_gets_404_for_statements_on_a_quotation_of_A
        ["POST /api/portal/quotations/{id:guid}/payment-notice"] = null, // same test
    };

    private static readonly string[] ResourcePlaceholders = ["{projectA}", "{quotationA}", "{imageA}"];

    public static IEnumerable<object[]> GetUrls() =>
        RouteTable.Values.OfType<string>().Select(u => new object[] { u });

    public static IEnumerable<object[]> IdUrls() =>
        RouteTable.Values.OfType<string>()
            .Where(u => ResourcePlaceholders.Any(p => u.Contains(p)))
            .Select(u => new object[] { u });

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
        var url = Fill(template, s);

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

    [Fact]
    public async Task Quotation_list_of_B_contains_nothing_of_A()
    {
        var s = await ArrangeAsync();

        var ofB = await s.PortalB.GetFromJsonAsync<JsonElement>("/api/portal/quotations");
        var ofA = await s.PortalA.GetFromJsonAsync<JsonElement>("/api/portal/quotations");

        ofB.EnumerateArray().Select(q => q.GetProperty("id").GetGuid()).Should().NotContain(s.QuotationA);
        ofA.EnumerateArray().Select(q => q.GetProperty("id").GetGuid()).Should().Contain(s.QuotationA);
    }

    [Theory]
    [MemberData(nameof(GetUrls))]
    public async Task Portal_route_rejects_designer_cookie(string template)
    {
        var s = await ArrangeAsync(); // leaves the designer signed in on Client

        var resp = await Client.GetAsync(Fill(template, s));

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(GetUrls))]
    public async Task Portal_response_does_not_expose_internal_fields(string template)
    {
        var s = await ArrangeAsync();

        var resp = await s.PortalA.GetAsync(Fill(template, s));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        foreach (var forbidden in new[] { "\"internalNotes\"", "\"notes\"", "\"scopeOverrideReason\"", "SECRET-INTERNAL", "SECRET-CLIENT-NOTE" })
            body.Should().NotContain(forbidden);
    }

    [Fact]
    public async Task Client_B_cannot_send_a_request_to_a_project_of_A()
    {
        var s = await ArrangeAsync();

        var resp = await s.PortalB.PostAsync($"/api/portal/projects/{s.ProjectA}/requests", RequestForm("Yêu cầu lạ của B"));

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await resp.Content.ReadAsStringAsync()).Should().NotContain(s.ProjectAName);
        var ofA = await s.PortalA.GetFromJsonAsync<JsonElement>($"/api/portal/requests?projectId={s.ProjectA}");
        ofA.EnumerateArray().Select(r => r.GetProperty("content").GetString()).Should().NotContain("Yêu cầu lạ của B");
        (await s.PortalB.GetFromJsonAsync<JsonElement>("/api/portal/requests")).GetArrayLength().Should().Be(0);
    }

    private static string Fill(string template, Scenario s) => template
        .Replace("{projectA}", s.ProjectA.ToString())
        .Replace("{quotationA}", s.QuotationA.ToString())
        .Replace("{imageA}", s.ImageA.ToString());

    private sealed record Scenario(
        HttpClient PortalA, HttpClient PortalB, Guid ProjectA, string ProjectAName, string DeliverableAName, Guid ProjectB,
        Guid QuotationA, Guid ImageA);

    private async Task<Scenario> ArrangeAsync()
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var (clientA, tokenA) = await CreateClientWithPortalLinkAsync();
        var (clientB, tokenB) = await CreateClientWithPortalLinkAsync();

        var projectAName = "ISOLATION-A-" + Guid.NewGuid().ToString("N");
        var projectA = await CreateProjectAsync(clientA, projectAName);
        var deliverableAName = "ISOLATION-DELIVERABLE-A-" + Guid.NewGuid().ToString("N");
        var deliverableA = await CreateDeliverableAsync(projectA, deliverableAName);
        await CreateRoundAsync(projectA, deliverableA, "SECRET-INTERNAL-ITEM",
            ($"Sửa {projectAName}", "MINOR", "IN_QUOTA")); // M7b: A has a round whose item carries an internal note
        await ChangeProjectStatusAsync(projectA, new { to = "IN_PROGRESS", confirmWarnings = true }); // A gets a timeline entry
        (await Client.PostAsJsonAsync($"/api/admin/projects/{projectA}/progress-updates",
            new { message = $"Tiến độ {projectAName}" })).EnsureSuccessStatusCode(); // M5: designer progress update on A's timeline
        var taskResp = await Client.PostAsJsonAsync($"/api/admin/projects/{projectA}/tasks", new { title = "SECRET-INTERNAL-TASK" });
        taskResp.EnsureSuccessStatusCode();
        var taskA = (await taskResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var upload = await Client.PostAsync($"/api/admin/tasks/{taskA}/images", TestImages.Form(TestImages.Png(32, 24)));
        upload.StatusCode.Should().Be(HttpStatusCode.Created, await upload.Content.ReadAsStringAsync());
        var imageA = (await upload.Content.ReadFromJsonAsync<JsonElement>())[0].GetProperty("id").GetGuid(); // M3: client-visible task image of A
        var quotationA = await CreateSentQuotationAsync(projectA); // M7b: snapshot carries projectAName; internal note SECRET-INTERNAL-QUOTE
        var projectB = await CreateProjectAsync(clientB, "ISOLATION-B-" + Guid.NewGuid().ToString("N"));

        var portalA = await OpenPortalSessionAsync(tokenA);
        var portalB = await OpenPortalSessionAsync(tokenB);
        (await portalA.PostAsync($"/api/portal/projects/{projectA}/requests", RequestForm($"Yêu cầu {projectAName}")))
            .EnsureSuccessStatusCode(); // M7b: A's request list carries projectAName

        return new Scenario(portalA, portalB, projectA, projectAName, deliverableAName, projectB, quotationA, imageA);
    }
}
