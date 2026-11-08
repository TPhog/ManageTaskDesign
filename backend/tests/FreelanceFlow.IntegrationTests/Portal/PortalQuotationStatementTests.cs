using System.Net;
using System.Text.Json;

namespace FreelanceFlow.IntegrationTests.Portal;

/// <summary>Plan 12 CF-03 / BR-76/77: the client's statements are recorded and announced; the designer confirms.</summary>
public class PortalQuotationStatementTests(AppFactory factory) : PortalTestBase(factory)
{
    private sealed record Arranged(HttpClient Portal, Guid ProjectId, Guid QuotationId, string ProjectName);

    /// <summary>SENT quotation: total 2.000.000, deposit 50% = 1.000.000.</summary>
    private async Task<Arranged> ArrangeAsync()
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var (clientId, token) = await CreateClientWithPortalLinkAsync();
        var name = "BaoGia " + Guid.NewGuid().ToString("N");
        var projectId = await CreateProjectAsync(clientId, name);
        var quotationId = await CreateSentQuotationAsync(projectId);
        return new Arranged(await OpenPortalSessionAsync(token), projectId, quotationId, name);
    }

    private static Task<HttpResponseMessage> AcceptAsync(HttpClient portal, Guid id) =>
        portal.PostAsync($"/api/portal/quotations/{id}/accept", null);

    private static Task<HttpResponseMessage> NoticeAsync(HttpClient portal, Guid id, string? purpose, string? note = null) =>
        portal.PostAsJsonAsync($"/api/portal/quotations/{id}/payment-notice", new { purpose, note });

    private async Task<List<(string Action, string ActorType)>> AdminActivityAsync(Guid projectId) =>
        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}/activity?pageSize=100"))
            .GetProperty("items").EnumerateArray()
            .Select(a => (a.GetProperty("action").GetString()!, a.GetProperty("actorType").GetString()!))
            .ToList();

    private static async Task<string?> CodeOf(HttpResponseMessage resp) =>
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();

    [Fact]
    public async Task Client_acceptance_is_recorded_logged_and_announced_without_changing_the_status()
    {
        var s = await ArrangeAsync();

        var resp = await AcceptAsync(s.Portal, s.QuotationId);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("SENT");
        body.GetProperty("clientAcceptedAt").ValueKind.Should().Be(JsonValueKind.String);
        var admin = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/quotations/{s.QuotationId}");
        admin.GetProperty("status").GetString().Should().Be("SENT");
        admin.GetProperty("clientAcceptedAt").ValueKind.Should().Be(JsonValueKind.String);
        (await AdminActivityAsync(s.ProjectId)).Should().Contain(("quotation.client_accepted", "CLIENT"));
        var timeline = await s.Portal.GetFromJsonAsync<JsonElement>($"/api/portal/projects/{s.ProjectId}/timeline");
        timeline.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("message").GetString())
            .Should().Contain(m => m!.StartsWith("Bạn đã đồng ý báo giá"));
        var hits = await Factory.Notifications.WaitForAsync(n =>
            n.Title == "Khách đồng ý báo giá" && n.Fields!.Any(f => f.Value == s.ProjectName));
        hits.Should().ContainSingle();
    }

    [Fact]
    public async Task Accepting_twice_is_idempotent_and_logged_once()
    {
        var s = await ArrangeAsync();

        (await AcceptAsync(s.Portal, s.QuotationId)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await AcceptAsync(s.Portal, s.QuotationId)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await AdminActivityAsync(s.ProjectId)).Count(a => a.Action == "quotation.client_accepted").Should().Be(1);
    }

    [Fact]
    public async Task Accepting_a_quotation_the_designer_already_accepted_is_409()
    {
        var s = await ArrangeAsync();
        var rowVersion = (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/quotations/{s.QuotationId}"))
            .GetProperty("rowVersion").GetUInt32();
        (await Client.PostAsJsonAsync($"/api/admin/quotations/{s.QuotationId}/accept",
            new { acceptedAt = (string?)null, note = (string?)null, evidenceFileId = (Guid?)null, rowVersion })).EnsureSuccessStatusCode();

        var resp = await AcceptAsync(s.Portal, s.QuotationId);

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeOf(resp)).Should().Be("INVALID_TRANSITION");
    }

    [Fact]
    public async Task Payment_notice_is_recorded_with_its_amount_and_announced()
    {
        var s = await ArrangeAsync();

        var resp = await NoticeAsync(s.Portal, s.QuotationId, "deposit", "Chuyển từ TK Vietcombank");

        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        var notice = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("clientPaymentNotice");
        notice.GetProperty("purpose").GetString().Should().Be("deposit");
        notice.GetProperty("amount").GetInt64().Should().Be(1_000_000);
        notice.GetProperty("pending").GetBoolean().Should().BeTrue();
        notice.GetProperty("note").GetString().Should().Be("Chuyển từ TK Vietcombank");
        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/quotations/{s.QuotationId}"))
            .GetProperty("paymentStatus").GetString().Should().Be("UNPAID");
        (await AdminActivityAsync(s.ProjectId)).Should().Contain(("quotation.client_payment_notice", "CLIENT"));
        (await Factory.Notifications.WaitForAsync(n =>
            n.Title == "Khách báo đã chuyển khoản" && n.Fields!.Any(f => f.Value == s.ProjectName))).Should().ContainSingle();
    }

    [Fact]
    public async Task Payment_notice_rejects_amounts_not_due_and_bad_input()
    {
        var s = await ArrangeAsync();

        var notDue = await NoticeAsync(s.Portal, s.QuotationId, "remaining");
        notDue.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeOf(notDue)).Should().Be("PAYMENT_NOT_APPLICABLE");
        (await NoticeAsync(s.Portal, s.QuotationId, "cash")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await NoticeAsync(s.Portal, s.QuotationId, null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await NoticeAsync(s.Portal, s.QuotationId, "deposit", new string('a', 501))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await AdminActivityAsync(s.ProjectId)).Should().NotContain(a => a.Action == "quotation.client_payment_notice");
    }

    [Fact]
    public async Task Repeating_the_same_payment_notice_is_idempotent()
    {
        var s = await ArrangeAsync();

        (await NoticeAsync(s.Portal, s.QuotationId, "deposit")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await NoticeAsync(s.Portal, s.QuotationId, "deposit")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await AdminActivityAsync(s.ProjectId)).Count(a => a.Action == "quotation.client_payment_notice").Should().Be(1);
    }

    [Fact]
    public async Task Designer_flow_asks_to_confirm_the_client_statements_first()
    {
        var s = await ArrangeAsync();
        await AcceptAsync(s.Portal, s.QuotationId);
        await NoticeAsync(s.Portal, s.QuotationId, "deposit");

        var flow = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{s.ProjectId}/flow");

        var kinds = flow.GetProperty("actions").EnumerateArray().Select(a => a.GetProperty("kind").GetString()).ToList();
        kinds.Take(2).Should().Equal("CONFIRM_CLIENT_ACCEPTANCE", "CONFIRM_CLIENT_PAYMENT");
        kinds.Should().NotContain("RECORD_ACCEPTANCE");
        flow.GetProperty("actions")[0].GetProperty("target").GetProperty("quotationId").GetGuid().Should().Be(s.QuotationId);
        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/quotations/{s.QuotationId}"))
            .GetProperty("clientPaymentNotice").GetProperty("pending").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Client_B_gets_404_for_statements_on_a_quotation_of_A()   // docs/09 §4 isolation
    {
        var a = await ArrangeAsync();
        var (_, tokenB) = await CreateClientWithPortalLinkAsync();
        var portalB = await OpenPortalSessionAsync(tokenB);

        var accept = await AcceptAsync(portalB, a.QuotationId);
        var notice = await NoticeAsync(portalB, a.QuotationId, "deposit");

        accept.StatusCode.Should().Be(HttpStatusCode.NotFound);
        notice.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await accept.Content.ReadAsStringAsync()).Should().NotContain(a.ProjectName);
        var admin = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/quotations/{a.QuotationId}");
        admin.GetProperty("clientAcceptedAt").ValueKind.Should().Be(JsonValueKind.Null);
        admin.GetProperty("clientPaymentNotice").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Statements_need_the_csrf_token_and_a_portal_session()
    {
        var s = await ArrangeAsync();

        (await Client.PostAsync($"/api/portal/quotations/{s.QuotationId}/accept", null))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        s.Portal.DefaultRequestHeaders.Remove("X-CSRF-Token");
        (await AcceptAsync(s.Portal, s.QuotationId)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await NoticeAsync(s.Portal, s.QuotationId, "deposit")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
