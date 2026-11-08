using System.Net;
using System.Text.Json;

namespace FreelanceFlow.IntegrationTests.Portal;

public class PortalRevisionsTests(AppFactory factory) : PortalTestBase(factory)
{
    private async Task<(HttpClient Portal, Guid ProjectId)> ArrangeAsync()
    {
        await LoginAsync();
        var (clientId, token) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "Dự án lần sửa");
        return (await OpenPortalSessionAsync(token), projectId);
    }

    [Fact]
    public async Task Revisions_show_rounds_per_deliverable_with_quota_and_client_safe_fields_only()   // POR-02, REV-07
    {
        var (portal, projectId) = await ArrangeAsync();
        var logo = await CreateDeliverableAsync(projectId, "Logo", includedRounds: 3);
        await CreateRoundAsync(projectId, logo, "SECRET-INTERNAL-ROUND",
            ("Đổi màu chữ sang xanh navy", "MINOR", "IN_QUOTA"), ("Thêm tagline", "MAJOR", "CHARGEABLE"));
        await CreateRoundAsync(projectId, null, "SECRET-INTERNAL-PROJECT-ROUND", ("Sửa lỗi chính tả", "MINOR", "FREE_FIX"));

        var resp = await portal.GetAsync($"/api/portal/projects/{projectId}/revisions");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        foreach (var forbidden in new[] { "SECRET-INTERNAL", "\"internalNotes\"", "\"scopeOverrideReason\"", "\"quotationItemId\"",
                                          "\"quotation\"", "\"tasks\"", "\"notes\"", "\"extraRoundPrice\"", "\"sourceRequestId\"" })
            body.Should().NotContain(forbidden);

        var rev = JsonDocument.Parse(body).RootElement;
        var d = rev.GetProperty("deliverables").EnumerateArray().Single();
        d.GetProperty("id").GetGuid().Should().Be(logo);
        d.GetProperty("name").GetString().Should().Be("Logo");
        d.GetProperty("includedRounds").GetInt32().Should().Be(3);
        d.GetProperty("usedRounds").GetInt32().Should().Be(1);   // BR-42: one counted round
        d.GetProperty("remainingRounds").GetInt32().Should().Be(2);
        var round = d.GetProperty("rounds").EnumerateArray().Single();
        round.GetProperty("roundNo").GetInt32().Should().Be(1);
        round.GetProperty("items").EnumerateArray()
            .Select(i => (i.GetProperty("description").GetString(), i.GetProperty("billing").GetString(), i.GetProperty("workStatus").GetString()))
            .Should().Equal(("Đổi màu chữ sang xanh navy", "IN_QUOTA", "TODO"), ("Thêm tagline", "CHARGEABLE", "TODO"));
        round.GetProperty("items")[0].GetProperty("type").GetString().Should().Be("MINOR");

        var projectRound = rev.GetProperty("projectRounds").EnumerateArray().Single();
        projectRound.GetProperty("items")[0].GetProperty("billing").GetString().Should().Be("FREE_FIX");
    }

    [Fact]
    public async Task Project_without_rounds_lists_its_deliverables_with_the_full_quota()
    {
        var (portal, projectId) = await ArrangeAsync();
        await CreateDeliverableAsync(projectId, "Banner", includedRounds: 2);

        var rev = await portal.GetFromJsonAsync<JsonElement>($"/api/portal/projects/{projectId}/revisions");

        var d = rev.GetProperty("deliverables").EnumerateArray().Single();
        d.GetProperty("usedRounds").GetInt32().Should().Be(0);
        d.GetProperty("remainingRounds").GetInt32().Should().Be(2);
        d.GetProperty("rounds").GetArrayLength().Should().Be(0);
        rev.GetProperty("projectRounds").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Unknown_project_returns_404()
    {
        var (portal, _) = await ArrangeAsync();

        (await portal.GetAsync($"/api/portal/projects/{Guid.NewGuid()}/revisions"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
