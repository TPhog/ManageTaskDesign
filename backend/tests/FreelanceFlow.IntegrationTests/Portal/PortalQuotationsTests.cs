using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Infrastructure.VietQr;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Portal;

public class PortalQuotationsTests(AppFactory factory) : PortalTestBase(factory)
{
    private async Task<(HttpClient Portal, Guid ProjectId, string Token)> ArrangeAsync(string projectName = "Báo giá portal")
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var (clientId, token) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, projectName);
        return (await OpenPortalSessionAsync(token), projectId, token);
    }

    private static List<Guid> Ids(JsonElement list) =>
        list.EnumerateArray().Select(q => q.GetProperty("id").GetGuid()).ToList();

    private async Task<uint> RowVersionAsync(Guid id) =>
        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/quotations/{id}")).GetProperty("rowVersion").GetUInt32();

    private async Task AdminPostAsync(string url, object body)
    {
        var resp = await Client.PostAsJsonAsync(url, body);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task List_shows_sent_quotations_from_the_snapshot_and_never_drafts()   // QUO-10, POR-06
    {
        var (portal, projectId, _) = await ArrangeAsync("Dự án có báo giá");
        var sentId = await CreateSentQuotationAsync(projectId);
        var draftId = await CreateDraftQuotationAsync(projectId);

        var list = await portal.GetFromJsonAsync<JsonElement>($"/api/portal/quotations?projectId={projectId}");

        Ids(list).Should().Equal(sentId);
        var item = list[0];
        item.GetProperty("number").GetString().Should().MatchRegex(@"^[A-Z]+-\d{4}-\d{4}$");
        item.GetProperty("version").GetInt32().Should().Be(1);
        item.GetProperty("projectName").GetString().Should().Be("Dự án có báo giá");
        item.GetProperty("title").GetString().Should().Be(QuoteTitle);
        item.GetProperty("total").GetInt64().Should().Be(2_000_000);
        item.GetProperty("depositAmount").GetInt64().Should().Be(1_000_000);
        item.GetProperty("status").GetString().Should().Be("SENT");
        item.GetProperty("effectiveStatus").GetString().Should().Be("SENT");
        item.GetProperty("paymentStatus").GetString().Should().Be("UNPAID");
        Ids(await portal.GetFromJsonAsync<JsonElement>("/api/portal/quotations")).Should().Equal(sentId);
        (await portal.GetAsync($"/api/portal/quotations/{draftId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await portal.GetAsync($"/api/portal/quotations/{draftId}/qr/full.png")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Detail_renders_the_snapshot_with_qr_options_and_no_internal_fields()   // POR-03, BR-39, BR-75
    {
        var (portal, projectId, _) = await ArrangeAsync();
        var id = await CreateSentQuotationAsync(projectId);

        var resp = await portal.GetAsync($"/api/portal/quotations/{id}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().NotContain("SECRET-INTERNAL-QUOTE").And.NotContain("\"internalNotes\"");
        var detail = JsonDocument.Parse(body).RootElement;
        detail.GetProperty("id").GetGuid().Should().Be(id);
        detail.GetProperty("projectId").GetGuid().Should().Be(projectId);
        detail.GetProperty("status").GetString().Should().Be("SENT");
        detail.GetProperty("effectiveStatus").GetString().Should().Be("SENT");
        var snapshot = detail.GetProperty("snapshot");
        var number = snapshot.GetProperty("number").GetString()!;
        snapshot.GetProperty("title").GetString().Should().Be(QuoteTitle);
        snapshot.GetProperty("items")[0].GetProperty("description").GetString().Should().Be("Thiết kế logo");
        snapshot.GetProperty("items")[0].GetProperty("details").GetString().Should().Be("3 phương án");
        snapshot.GetProperty("total").GetInt64().Should().Be(2_000_000);
        snapshot.GetProperty("notesToClient").GetString().Should().Be("Cảm ơn anh/chị");
        snapshot.GetProperty("bank").GetProperty("accountNo").GetString().Should().Be("0123456789");

        var qr = detail.GetProperty("qrOptions");
        qr.EnumerateArray().Select(o => o.GetProperty("purpose").GetString()).Should().Equal("deposit", "full");
        var depositMemo = VietQrMemo.For(number, "deposit");
        qr[0].GetProperty("amount").GetInt64().Should().Be(1_000_000);
        qr[0].GetProperty("memo").GetString().Should().Be(depositMemo);
        qr[0].GetProperty("payload").GetString().Should().Be(
            VietQrPayloadBuilder.Build(new VietQrRequest("970436", "0123456789", 1_000_000, depositMemo)));
        detail.GetProperty("versions").EnumerateArray().Select(v => v.GetProperty("version").GetInt32()).Should().Equal(1);
    }

    [Fact]
    public async Task Live_settings_changes_never_reach_a_sent_quotation()   // AC-B9, BR-39
    {
        var (portal, projectId, _) = await ArrangeAsync();
        var id = await CreateSentQuotationAsync(projectId);
        try
        {
            await ConfigureBankAsync(accountNo: "9876543210");

            var detail = await portal.GetFromJsonAsync<JsonElement>($"/api/portal/quotations/{id}");

            detail.GetProperty("snapshot").GetProperty("bank").GetProperty("accountNo").GetString().Should().Be("0123456789");
            detail.GetProperty("qrOptions").EnumerateArray()
                .Should().OnlyContain(o => o.GetProperty("payload").GetString()!.Contains("0123456789"));
        }
        finally
        {
            await ConfigureBankAsync();
        }
    }

    [Fact]
    public async Task Expired_is_computed_on_read_and_still_offers_the_qr()   // AC-B10, docs/08 §3.2
    {
        var (_, projectId, token) = await ArrangeAsync();
        var id = await CreateSentQuotationAsync(projectId);
        var expiry = DateOnly.Parse((await Client.GetFromJsonAsync<JsonElement>($"/api/admin/quotations/{id}"))
            .GetProperty("expiryDate").GetString()!);

        using var later = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddSingleton<IClock>(new FixedClock(expiry.AddDays(1)))));
        var portal = await OpenPortalSessionAsync(token,
            later.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false }));

        var detail = await portal.GetFromJsonAsync<JsonElement>($"/api/portal/quotations/{id}");
        detail.GetProperty("status").GetString().Should().Be("SENT");
        detail.GetProperty("effectiveStatus").GetString().Should().Be("EXPIRED");
        detail.GetProperty("qrOptions").GetArrayLength().Should().Be(2);   // BR-75: EXPIRED keeps the QR
        (await portal.GetFromJsonAsync<JsonElement>("/api/portal/quotations"))[0]
            .GetProperty("effectiveStatus").GetString().Should().Be("EXPIRED");
        (await portal.GetFromJsonAsync<JsonElement>("/api/portal/projects"))[0]
            .GetProperty("hasOpenQuotation").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Draft_versions_stay_hidden_and_superseded_is_reachable_only_as_history()   // BR-33/34, docs/09 §3 luồng 4
    {
        var (portal, projectId, _) = await ArrangeAsync();
        var v1 = await CreateSentQuotationAsync(projectId);
        var next = await Client.PostAsync($"/api/admin/quotations/{v1}/new-version", null);
        next.StatusCode.Should().Be(HttpStatusCode.OK, await next.Content.ReadAsStringAsync());
        var v2 = (await next.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // v2 is still a DRAFT: invisible everywhere
        Ids(await portal.GetFromJsonAsync<JsonElement>("/api/portal/quotations")).Should().Equal(v1);
        (await portal.GetAsync($"/api/portal/quotations/{v2}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await portal.GetAsync($"/api/portal/quotations/{v1}/versions/2")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await portal.GetFromJsonAsync<JsonElement>($"/api/portal/quotations/{v1}"))
            .GetProperty("versions").GetArrayLength().Should().Be(1);

        await SendQuotationAsync(v2);   // v1 → SUPERSEDED (BR-34)

        Ids(await portal.GetFromJsonAsync<JsonElement>("/api/portal/quotations")).Should().Equal(v2);
        (await portal.GetAsync($"/api/portal/quotations/{v1}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var current = await portal.GetFromJsonAsync<JsonElement>($"/api/portal/quotations/{v2}");
        current.GetProperty("versions").EnumerateArray()
            .Select(v => (v.GetProperty("version").GetInt32(), v.GetProperty("status").GetString()))
            .Should().Equal((1, "SUPERSEDED"), (2, "SENT"));
        var old = await portal.GetFromJsonAsync<JsonElement>($"/api/portal/quotations/{v2}/versions/1");
        old.GetProperty("id").GetGuid().Should().Be(v1);
        old.GetProperty("status").GetString().Should().Be("SUPERSEDED");
        old.GetProperty("snapshot").GetProperty("version").GetInt32().Should().Be(1);
        old.GetProperty("qrOptions").GetArrayLength().Should().Be(0);   // BR-75: no QR on SUPERSEDED
        (await portal.GetAsync($"/api/portal/quotations/{v2}/versions/9")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Qr_follows_payment_status_and_rejected_quotations_show_none()   // BR-75, AC-B13
    {
        var (portal, projectId, _) = await ArrangeAsync();
        var accepted = await CreateSentQuotationAsync(projectId);
        await AdminPostAsync($"/api/admin/quotations/{accepted}/accept", new { rowVersion = await RowVersionAsync(accepted) });
        await AdminPostAsync($"/api/admin/quotations/{accepted}/payment-status",
            new { to = "DEPOSIT_PAID", rowVersion = await RowVersionAsync(accepted) });

        var detail = await portal.GetFromJsonAsync<JsonElement>($"/api/portal/quotations/{accepted}");
        detail.GetProperty("status").GetString().Should().Be("ACCEPTED");
        detail.GetProperty("paymentStatus").GetString().Should().Be("DEPOSIT_PAID");
        detail.GetProperty("qrOptions").EnumerateArray()
            .Select(o => (o.GetProperty("purpose").GetString(), o.GetProperty("amount").GetInt64()))
            .Should().Equal(("remaining", 1_000_000L));
        (await portal.GetAsync($"/api/portal/quotations/{accepted}/qr/deposit.png"))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var png = await portal.GetAsync($"/api/portal/quotations/{accepted}/qr/remaining.png");
        png.StatusCode.Should().Be(HttpStatusCode.OK);
        png.Content.Headers.ContentType!.MediaType.Should().Be("image/png");

        await AdminPostAsync($"/api/admin/quotations/{accepted}/payment-status",
            new { to = "PAID", rowVersion = await RowVersionAsync(accepted) });
        (await portal.GetFromJsonAsync<JsonElement>($"/api/portal/quotations/{accepted}"))
            .GetProperty("qrOptions").GetArrayLength().Should().Be(0);

        var rejected = await CreateSentQuotationAsync(projectId);
        await AdminPostAsync($"/api/admin/quotations/{rejected}/reject",
            new { reason = "Khách chọn phương án khác", rowVersion = await RowVersionAsync(rejected) });
        var list = await portal.GetFromJsonAsync<JsonElement>($"/api/portal/quotations?projectId={projectId}");
        list.EnumerateArray().Single(q => q.GetProperty("id").GetGuid() == rejected)
            .GetProperty("status").GetString().Should().Be("REJECTED");
        (await portal.GetFromJsonAsync<JsonElement>($"/api/portal/quotations/{rejected}"))
            .GetProperty("qrOptions").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Project_list_points_to_the_newest_open_quotation()   // chip "Có báo giá mới"
    {
        var (portal, projectId, _) = await ArrangeAsync();
        (await portal.GetFromJsonAsync<JsonElement>("/api/portal/projects"))[0]
            .GetProperty("openQuotationId").ValueKind.Should().Be(JsonValueKind.Null);

        await CreateSentQuotationAsync(projectId);
        var newest = await CreateSentQuotationAsync(projectId, unitPrice: 500_000);

        var item = (await portal.GetFromJsonAsync<JsonElement>("/api/portal/projects"))[0];
        item.GetProperty("hasOpenQuotation").GetBoolean().Should().BeTrue();
        item.GetProperty("openQuotationId").GetGuid().Should().Be(newest);
    }

    [Fact]
    public async Task Unknown_project_filter_returns_404()
    {
        var (portal, _, _) = await ArrangeAsync();

        (await portal.GetAsync($"/api/portal/quotations?projectId={Guid.NewGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed class FixedClock(DateOnly today) : IClock
    {
        public DateTimeOffset UtcNow => new(today.ToDateTime(new TimeOnly(5, 0)), TimeSpan.Zero);
        public DateOnly TodayVn => today;
    }
}
