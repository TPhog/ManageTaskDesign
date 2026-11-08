using System.Net;
using System.Text.Json;

namespace FreelanceFlow.IntegrationTests.Portal;

public class PortalProjectsTests(AppFactory factory) : PortalTestBase(factory)
{
    [Fact]
    public async Task List_returns_own_projects_with_deliverable_summary()
    {
        await LoginAsync();
        var (clientId, token) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "Bo nhan dien");
        await MoveDeliverableAsync(await CreateDeliverableAsync(projectId, "Logo"), "IN_PROGRESS", "IN_REVIEW", "APPROVED", "DELIVERED");
        await MoveDeliverableAsync(await CreateDeliverableAsync(projectId, "Danh thiep"), "IN_PROGRESS", "IN_REVIEW", "APPROVED");
        await MoveDeliverableAsync(await CreateDeliverableAsync(projectId, "Banner"), "CANCELLED");
        await CreateDeliverableAsync(projectId, "Poster");
        var portal = await OpenPortalSessionAsync(token);

        var list = await portal.GetFromJsonAsync<JsonElement>("/api/portal/projects");

        list.GetArrayLength().Should().Be(1);
        var item = list[0];
        item.GetProperty("id").GetGuid().Should().Be(projectId);
        item.GetProperty("name").GetString().Should().Be("Bo nhan dien");
        item.GetProperty("status").GetString().Should().Be("DRAFT");
        var summary = item.GetProperty("deliverableSummary");
        summary.GetProperty("total").GetInt32().Should().Be(3);
        summary.GetProperty("delivered").GetInt32().Should().Be(1);
        summary.GetProperty("approved").GetInt32().Should().Be(1);
        item.GetProperty("hasOpenQuotation").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Detail_returns_deliverables_and_latest_confirmed_brief_only()
    {
        await LoginAsync();
        var (clientId, token) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "Poster su kien");
        var deliverableId = await CreateDeliverableAsync(projectId, "Poster A2", includedRounds: 3);
        var v1 = await GetLatestBriefIdAsync(projectId);
        (await Client.PutAsJsonAsync($"/api/admin/briefs/{v1}", new { content = "Brief đã chốt v1" })).EnsureSuccessStatusCode();
        (await Client.PostAsync($"/api/admin/briefs/{v1}/confirm", null)).EnsureSuccessStatusCode();
        (await Client.PostAsync($"/api/admin/projects/{projectId}/briefs", null)).EnsureSuccessStatusCode();
        var v2 = await GetLatestBriefIdAsync(projectId);
        (await Client.PutAsJsonAsync($"/api/admin/briefs/{v2}", new { content = "DRAFT-V2-MARKER" })).EnsureSuccessStatusCode();
        var portal = await OpenPortalSessionAsync(token);

        var resp = await portal.GetAsync($"/api/portal/projects/{projectId}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await resp.Content.ReadAsStringAsync();
        raw.Should().NotContain("DRAFT-V2-MARKER").And.NotContain("SECRET-INTERNAL");
        var detail = JsonDocument.Parse(raw).RootElement;
        detail.GetProperty("name").GetString().Should().Be("Poster su kien");
        var brief = detail.GetProperty("brief");
        brief.GetProperty("version").GetInt32().Should().Be(1);
        brief.GetProperty("content").GetString().Should().Be("Brief đã chốt v1");
        var deliverable = detail.GetProperty("deliverables")[0];
        deliverable.GetProperty("id").GetGuid().Should().Be(deliverableId);
        deliverable.GetProperty("includedRounds").GetInt32().Should().Be(3);
        deliverable.GetProperty("usedRounds").GetInt32().Should().Be(0);
        deliverable.GetProperty("remainingRounds").GetInt32().Should().Be(3);
        detail.GetProperty("deliverableSummary").GetProperty("total").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Detail_without_confirmed_brief_has_null_brief()
    {
        await LoginAsync();
        var (clientId, token) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "Chua chot brief");
        var portal = await OpenPortalSessionAsync(token);

        var detail = await portal.GetFromJsonAsync<JsonElement>($"/api/portal/projects/{projectId}");

        detail.GetProperty("brief").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Status_reason_is_shown_only_when_public()
    {
        await LoginAsync();
        var (clientId, token) = await CreateClientWithPortalLinkAsync();
        var privateId = await CreateProjectAsync(clientId, "Tam dung noi bo");
        await ChangeProjectStatusAsync(privateId, new { to = "IN_PROGRESS", confirmWarnings = true });
        await ChangeProjectStatusAsync(privateId, new { to = "ON_HOLD", reason = "PRIVATE-REASON-MARKER", reasonPublic = false });
        var publicId = await CreateProjectAsync(clientId, "Tam dung cong khai");
        await ChangeProjectStatusAsync(publicId, new { to = "IN_PROGRESS", confirmWarnings = true });
        await ChangeProjectStatusAsync(publicId, new { to = "ON_HOLD", reason = "Chờ khách gửi nội dung", reasonPublic = true });
        var portal = await OpenPortalSessionAsync(token);

        var privateResp = await portal.GetAsync($"/api/portal/projects/{privateId}");
        var privateRaw = await privateResp.Content.ReadAsStringAsync();
        var publicDetail = await portal.GetFromJsonAsync<JsonElement>($"/api/portal/projects/{publicId}");

        privateRaw.Should().NotContain("PRIVATE-REASON-MARKER");
        JsonDocument.Parse(privateRaw).RootElement.GetProperty("statusReason").ValueKind.Should().Be(JsonValueKind.Null);
        publicDetail.GetProperty("status").GetString().Should().Be("ON_HOLD");
        publicDetail.GetProperty("statusReason").GetString().Should().Be("Chờ khách gửi nội dung");
    }

    [Fact]
    public async Task Timeline_lists_only_client_visible_messages_newest_first()
    {
        await LoginAsync();
        var (clientId, token) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "Timeline");
        var logo = await CreateDeliverableAsync(projectId, "Logo");
        await ChangeProjectStatusAsync(projectId, new { to = "IN_PROGRESS", confirmWarnings = true }); // visible
        await MoveDeliverableAsync(logo, "IN_PROGRESS");                                               // not visible
        await MoveDeliverableAsync(logo, "IN_REVIEW");                                                 // visible
        await ChangeProjectStatusAsync(projectId, new { to = "ON_HOLD", reason = "PRIVATE-REASON-MARKER", reasonPublic = false }); // not visible
        var portal = await OpenPortalSessionAsync(token);

        var resp = await portal.GetAsync($"/api/portal/projects/{projectId}/timeline");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await resp.Content.ReadAsStringAsync();
        raw.Should().NotContain("PRIVATE-REASON-MARKER");
        var page = JsonDocument.Parse(raw).RootElement;
        page.GetProperty("total").GetInt32().Should().Be(2);
        var items = page.GetProperty("items").EnumerateArray().ToList();
        items.Select(i => i.GetProperty("message").GetString())
            .Should().Equal("Logo: Chờ duyệt", "Dự án chuyển sang: Đang thực hiện");
        items.Should().AllSatisfy(i =>
            i.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("occurredAt", "message", "kind", "images"));
    }

    [Fact]
    public async Task Timeline_shows_progress_updates_and_request_outcomes()   // BR-95, M5 "cập nhật tiến độ"
    {
        await LoginAsync();
        var (clientId, token) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "Progress");
        var logo = await CreateDeliverableAsync(projectId, "Logo");   // 2 free rounds
        (await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/progress-updates",
            new { message = "  Đã xong bản phác thảo logo  " })).StatusCode.Should().Be(HttpStatusCode.Created);
        var request = (await (await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/requests",
                new { content = "Đổi màu", source = "ZALO", deliverableId = logo }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("request");
        (await Client.PostAsJsonAsync($"/api/admin/requests/{request.GetProperty("id").GetGuid()}/triage", new
        {
            rounds = new[]
            {
                new { deliverableId = (Guid?)logo, receivedAt = (string?)null,
                      items = new[] { new { description = "Đổi màu", type = "MINOR", billing = (string?)null } } },
            },
            rowVersion = request.GetProperty("rowVersion").GetUInt32(),
        })).StatusCode.Should().Be(HttpStatusCode.OK);
        var portal = await OpenPortalSessionAsync(token);

        var timeline = await portal.GetFromJsonAsync<JsonElement>($"/api/portal/projects/{projectId}/timeline");
        var detail = await portal.GetFromJsonAsync<JsonElement>($"/api/portal/projects/{projectId}");

        timeline.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("message").GetString())
            .Should().Equal("Yêu cầu của bạn đã được tiếp nhận", "Đã xong bản phác thảo logo");
        var deliverable = detail.GetProperty("deliverables")[0];
        deliverable.GetProperty("usedRounds").GetInt32().Should().Be(1);
        deliverable.GetProperty("remainingRounds").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Progress_update_requires_a_message_and_an_existing_project()
    {
        await LoginAsync();
        var (clientId, _) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "Progress");

        (await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/progress-updates", new { message = "   " }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.PostAsJsonAsync($"/api/admin/projects/{Guid.NewGuid()}/progress-updates", new { message = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unknown_project_returns_404()
    {
        await LoginAsync();
        var (_, token) = await CreateClientWithPortalLinkAsync();
        var portal = await OpenPortalSessionAsync(token);

        (await portal.GetAsync($"/api/portal/projects/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await portal.GetAsync($"/api/portal/projects/{Guid.NewGuid()}/timeline")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Summary_counts_deliverables_waiting_for_the_client()   // QA C-06
    {
        await LoginAsync();
        var (clientId, token) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "Cho duyet");
        await MoveDeliverableAsync(await CreateDeliverableAsync(projectId, "Logo"), "IN_PROGRESS", "IN_REVIEW");
        await CreateDeliverableAsync(projectId, "Menu");
        var portal = await OpenPortalSessionAsync(token);

        var list = await portal.GetFromJsonAsync<JsonElement>("/api/portal/projects");
        var detail = await portal.GetFromJsonAsync<JsonElement>($"/api/portal/projects/{projectId}");

        list[0].GetProperty("deliverableSummary").GetProperty("inReview").GetInt32().Should().Be(1);
        detail.GetProperty("deliverableSummary").GetProperty("inReview").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Timeline_marks_designer_messages_apart_from_system_events()   // QA C-12
    {
        await LoginAsync();
        var (clientId, token) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "Tin nhan");
        await ChangeProjectStatusAsync(projectId, new { to = "IN_PROGRESS", confirmWarnings = true });
        (await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/progress-updates",
            new { message = "Đã xong phác thảo" })).EnsureSuccessStatusCode();
        var portal = await OpenPortalSessionAsync(token);

        var items = (await portal.GetFromJsonAsync<JsonElement>($"/api/portal/projects/{projectId}/timeline"))
            .GetProperty("items").EnumerateArray().ToList();

        items.Select(i => (i.GetProperty("message").GetString(), i.GetProperty("kind").GetString()))
            .Should().Equal(("Đã xong phác thảo", "MESSAGE"), ("Dự án chuyển sang: Đang thực hiện", "EVENT"));
    }
}
