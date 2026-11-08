using System.Net;
using System.Text.Json;

namespace FreelanceFlow.IntegrationTests.Portal;

/// <summary>Plan 12 CF-01/02 BR-48: one state per deliverable on the portal; no approval while a change request is open.</summary>
public class PortalReviewStateTests(AppFactory factory) : PortalTestBase(factory)
{
    private sealed record Arranged(HttpClient Portal, Guid ProjectId, Guid DeliverableId);

    private async Task<Arranged> ArrangeAsync()
    {
        await LoginAsync();
        var (clientId, token) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "MotGiong " + Guid.NewGuid().ToString("N"));
        var deliverableId = await CreateDeliverableAsync(projectId, "Logo", includedRounds: 2);
        await MoveDeliverableAsync(deliverableId, "IN_PROGRESS", "IN_REVIEW");
        return new Arranged(await OpenPortalSessionAsync(token), projectId, deliverableId);
    }

    private static async Task<(JsonElement Deliverable, JsonElement Summary)> ReadAsync(Arranged s)
    {
        var p = await s.Portal.GetFromJsonAsync<JsonElement>($"/api/portal/projects/{s.ProjectId}");
        var d = p.GetProperty("deliverables").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == s.DeliverableId);
        return (d, p.GetProperty("deliverableSummary"));
    }

    /// <summary>GET /api/portal/projects — the list computes awaitingClient in SQL (separate code path from the detail).</summary>
    private static async Task<int> ListAwaitingAsync(Arranged s) =>
        (await s.Portal.GetFromJsonAsync<JsonElement>("/api/portal/projects")).EnumerateArray()
            .Single(p => p.GetProperty("id").GetGuid() == s.ProjectId)
            .GetProperty("deliverableSummary").GetProperty("awaitingClient").GetInt32();

    private static Task<HttpResponseMessage> ApproveAsync(Arranged s) =>
        s.Portal.PostAsync($"/api/portal/deliverables/{s.DeliverableId}/approve", null);

    private async Task<Guid> SendChangeRequestAsync(Arranged s)
    {
        var resp = await s.Portal.PostAsync($"/api/portal/projects/{s.ProjectId}/requests", RequestForm("Đổi màu chữ", s.DeliverableId));
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Awaiting_deliverable_counts_in_the_header_until_the_client_approves()
    {
        var s = await ArrangeAsync();

        var (before, summaryBefore) = await ReadAsync(s);
        before.GetProperty("reviewState").GetString().Should().Be("AWAITING_CLIENT");
        before.GetProperty("hasOpenChangeRequest").GetBoolean().Should().BeFalse();
        summaryBefore.GetProperty("awaitingClient").GetInt32().Should().Be(1);
        var list = await s.Portal.GetFromJsonAsync<JsonElement>("/api/portal/projects");
        list.EnumerateArray().Single(p => p.GetProperty("id").GetGuid() == s.ProjectId)
            .GetProperty("deliverableSummary").GetProperty("awaitingClient").GetInt32().Should().Be(1);

        (await ApproveAsync(s)).StatusCode.Should().Be(HttpStatusCode.OK);

        var (after, summaryAfter) = await ReadAsync(s);
        after.GetProperty("reviewState").GetString().Should().Be("CLIENT_APPROVED");
        summaryAfter.GetProperty("awaitingClient").GetInt32().Should().Be(0);
        summaryAfter.GetProperty("inReview").GetInt32().Should().Be(1, "inReview keeps its raw meaning");
        list = await s.Portal.GetFromJsonAsync<JsonElement>("/api/portal/projects");
        list.EnumerateArray().Single(p => p.GetProperty("id").GetGuid() == s.ProjectId)
            .GetProperty("deliverableSummary").GetProperty("awaitingClient").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task A_new_change_request_shows_changes_in_progress_and_blocks_approval()
    {
        var s = await ArrangeAsync();
        await SendChangeRequestAsync(s);

        var (d, summary) = await ReadAsync(s);
        d.GetProperty("reviewState").GetString().Should().Be("CHANGES_IN_PROGRESS");
        d.GetProperty("hasOpenChangeRequest").GetBoolean().Should().BeTrue();
        summary.GetProperty("awaitingClient").GetInt32().Should().Be(0);
        (await ListAwaitingAsync(s)).Should().Be(0, "a NEW request tied to the deliverable means it is not awaiting the client");

        var resp = await ApproveAsync(s);
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("CHANGE_REQUEST_OPEN");
        (await ReadAsync(s)).Deliverable.GetProperty("clientDecision").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Open_revision_items_keep_changes_in_progress_until_done()
    {
        var s = await ArrangeAsync();
        var round = await CreateRoundAsync(s.ProjectId, s.DeliverableId, "noi bo", ("Đổi màu", "MINOR", "IN_QUOTA"));
        var itemId = round.GetProperty("items")[0].GetProperty("id").GetGuid();

        var (open, openSummary) = await ReadAsync(s);
        open.GetProperty("reviewState").GetString().Should().Be("CHANGES_IN_PROGRESS");
        open.GetProperty("hasOpenChangeRequest").GetBoolean().Should().BeTrue();
        openSummary.GetProperty("awaitingClient").GetInt32().Should().Be(0);
        (await ListAwaitingAsync(s)).Should().Be(0, "an open revision item of the deliverable means it is not awaiting the client");
        var blocked = await ApproveAsync(s);
        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await blocked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("CHANGE_REQUEST_OPEN");

        foreach (var to in new[] { "IN_PROGRESS", "DONE" })
        {
            var rev = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{s.ProjectId}/revisions");
            var rowVersion = rev.GetProperty("deliverables").EnumerateArray()
                .SelectMany(x => x.GetProperty("rounds").EnumerateArray())
                .SelectMany(r => r.GetProperty("items").EnumerateArray())
                .Single(i => i.GetProperty("id").GetGuid() == itemId).GetProperty("rowVersion").GetUInt32();
            (await Client.PostAsJsonAsync($"/api/admin/revision-items/{itemId}/work-status", new { to, rowVersion }))
                .EnsureSuccessStatusCode();
        }

        var (done, doneSummary) = await ReadAsync(s);
        done.GetProperty("reviewState").GetString().Should().Be("AWAITING_CLIENT");
        done.GetProperty("hasOpenChangeRequest").GetBoolean().Should().BeFalse();
        doneSummary.GetProperty("awaitingClient").GetInt32().Should().Be(1);
        (await ListAwaitingAsync(s)).Should().Be(1);
        (await ApproveAsync(s)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Declining_the_request_hands_the_decision_back_to_the_client()
    {
        var s = await ArrangeAsync();
        var requestId = await SendChangeRequestAsync(s);
        var rowVersion = (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/requests/{requestId}"))
            .GetProperty("request").GetProperty("rowVersion").GetUInt32();

        (await Client.PostAsJsonAsync($"/api/admin/requests/{requestId}/decline", new { reason = "Ngoài phạm vi", rowVersion }))
            .EnsureSuccessStatusCode();

        (await ReadAsync(s)).Deliverable.GetProperty("reviewState").GetString().Should().Be("AWAITING_CLIENT");
        (await ListAwaitingAsync(s)).Should().Be(1, "a DECLINED request no longer blocks");
    }

    [Fact]
    public async Task Me_exposes_the_response_time_set_by_the_designer()
    {
        var s = await ArrangeAsync();
        var current = await Client.GetFromJsonAsync<JsonElement>("/api/admin/settings");
        current.GetProperty("portalResponseTime").GetString().Should().NotBeNullOrWhiteSpace();
        var original = current.GetProperty("portalResponseTime").GetString();

        try
        {
            (await PutSettingsAsync(current, "trong 2 giờ làm việc")).StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await s.Portal.GetFromJsonAsync<JsonElement>("/api/portal/me"))
                .GetProperty("designer").GetProperty("responseTime").GetString().Should().Be("trong 2 giờ làm việc");

            var tooLong = await PutSettingsAsync(await Client.GetFromJsonAsync<JsonElement>("/api/admin/settings"), new string('x', 61));
            tooLong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            await PutSettingsAsync(await Client.GetFromJsonAsync<JsonElement>("/api/admin/settings"), original);
        }
    }

    private Task<HttpResponseMessage> PutSettingsAsync(JsonElement s, string? portalResponseTime) =>
        Client.PutAsJsonAsync("/api/admin/settings", new
        {
            displayName = s.GetProperty("displayName").GetString(),
            contactEmail = s.GetProperty("contactEmail").GetString(),
            contactPhone = s.GetProperty("contactPhone").GetString(),
            logoFileId = s.GetProperty("logoFileId").ValueKind == JsonValueKind.Null ? (Guid?)null : s.GetProperty("logoFileId").GetGuid(),
            bankBin = s.GetProperty("bankBin").GetString(),
            bankAccountNo = s.GetProperty("bankAccountNo").GetString(),
            bankAccountName = s.GetProperty("bankAccountName").GetString(),
            quotationPrefix = s.GetProperty("quotationPrefix").GetString(),
            defaultValidityDays = s.GetProperty("defaultValidityDays").GetInt32(),
            defaultDepositPercent = s.GetProperty("defaultDepositPercent").GetInt32(),
            defaultTerms = s.GetProperty("defaultTerms").GetString(),
            portalResponseTime,
            rowVersion = s.GetProperty("rowVersion").GetInt64(),
        });
}
