using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Infrastructure.VietQr;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Quotations;

public class QuotationWorkflowTests(AppFactory factory) : QuotationTestBase(factory)
{
    private static DateOnly TodayVn => new SystemClock().TodayVn;

    [Fact]
    public async Task Send_assigns_number_dates_snapshot_and_visible_activity()   // AC-B3, BR-31
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var (_, id) = await CreateSendableDraftAsync();

        var q = await SendAsync(id);

        q.GetProperty("status").GetString().Should().Be("SENT");
        var number = q.GetProperty("number").GetString()!;
        number.Should().MatchRegex($@"^BG-{TodayVn.Year}-\d{{4}}$");
        q.GetProperty("version").GetInt32().Should().Be(1);
        DateOnly.Parse(q.GetProperty("issueDate").GetString()!).Should().Be(TodayVn);
        DateOnly.Parse(q.GetProperty("expiryDate").GetString()!).Should().Be(TodayVn.AddDays(7));
        var snapshot = q.GetProperty("snapshot");
        snapshot.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        snapshot.GetProperty("number").GetString().Should().Be(number);
        snapshot.GetProperty("total").GetInt64().Should().Be(2_500_000);
        snapshot.GetProperty("items").GetArrayLength().Should().Be(2);
        snapshot.GetProperty("bank").GetProperty("accountNo").GetString().Should().Be("0123456789");
        snapshot.GetProperty("bank").GetProperty("shortName").GetString().Should().Be("Vietcombank");
        snapshot.TryGetProperty("internalNotes", out _).Should().BeFalse();
        q.GetProperty("items").GetArrayLength().Should().Be(0);    // BR-39: non-DRAFT renders from snapshot
        q.GetProperty("qrOptions").EnumerateArray().Select(o => o.GetProperty("purpose").GetString())
            .Should().Equal("deposit", "full");
        (await ActivityOfAsync(id)).Should().ContainSingle(a =>
            a.Action == "quotation.sent" && a.Visible && a.Message == $"Báo giá {number} (v1) đã được gửi");
    }

    [Fact]
    public async Task Send_without_bank_account_returns_422()   // AC-B17
    {
        await LoginAsync();
        await ConfigureBankAsync(null, null);
        var (_, id) = await CreateSendableDraftAsync();
        var rowVersion = (await GetQuotationAsync(id)).GetProperty("rowVersion").GetUInt32();

        var resp = await Client.PostAsJsonAsync($"/api/admin/quotations/{id}/send", new { rowVersion });

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString()
            .Should().Be("Chưa cấu hình tài khoản nhận tiền");
    }

    [Fact]
    public async Task Send_without_items_returns_422()
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var projectId = await CreateProjectAsync();
        var created = await PostQuotationAsync(projectId, new { kind = "INITIAL", fromDeliverables = false });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var rowVersion = (await GetQuotationAsync(id)).GetProperty("rowVersion").GetUInt32();

        var resp = await Client.PostAsJsonAsync($"/api/admin/quotations/{id}/send", new { rowVersion });

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("BUSINESS_RULE");
    }

    [Fact]
    public async Task Concurrent_sends_get_distinct_consecutive_numbers()   // AC-B4, BR-32
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var (_, a) = await CreateSendableDraftAsync();
        var (_, b) = await CreateSendableDraftAsync();
        var rvA = (await GetQuotationAsync(a)).GetProperty("rowVersion").GetUInt32();
        var rvB = (await GetQuotationAsync(b)).GetProperty("rowVersion").GetUInt32();

        var responses = await Task.WhenAll(
            Client.PostAsJsonAsync($"/api/admin/quotations/{a}/send", new { rowVersion = rvA }),
            Client.PostAsJsonAsync($"/api/admin/quotations/{b}/send", new { rowVersion = rvB }));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        var numbers = new List<string>();
        foreach (var r in responses)
            numbers.Add((await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("number").GetString()!);
        numbers.Should().OnlyHaveUniqueItems();
        var sequences = numbers.Select(n => int.Parse(n[(n.LastIndexOf('-') + 1)..])).Order().ToList();
        (sequences[1] - sequences[0]).Should().Be(1);
    }

    [Fact]
    public async Task Send_with_stale_row_version_returns_409_and_draws_no_number()
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var (_, id) = await CreateSendableDraftAsync();
        var draft = await GetQuotationAsync(id);
        (await PutDraftAsync(id, DraftBody(draft))).EnsureSuccessStatusCode();

        var resp = await Client.PostAsJsonAsync($"/api/admin/quotations/{id}/send",
            new { rowVersion = draft.GetProperty("rowVersion").GetUInt32() });

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var after = await GetQuotationAsync(id);
        after.GetProperty("status").GetString().Should().Be("DRAFT");
        after.GetProperty("number").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Sent_quotation_cannot_be_edited_or_deleted()   // AC-B5, AC-B19
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var (_, id) = await CreateSendableDraftAsync();
        var sent = await SendAsync(id);

        var put = await PutDraftAsync(id, DraftBody(sent, discountAmount: 1));
        var delete = await Client.DeleteAsync($"/api/admin/quotations/{id}");

        put.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await put.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("QUOTATION_LOCKED");
        delete.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task New_version_then_send_supersedes_the_previous_one()   // AC-B6 (without BR-47 relinking — M5)
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var (_, v1) = await CreateSendableDraftAsync();
        var sentV1 = await SendAsync(v1);

        var v2 = await PostCommandAsync(v1, "new-version", new { });
        v2.GetProperty("status").GetString().Should().Be("DRAFT");
        v2.GetProperty("version").GetInt32().Should().Be(2);
        v2.GetProperty("number").GetString().Should().Be(sentV1.GetProperty("number").GetString());
        v2.GetProperty("previousVersionId").GetGuid().Should().Be(v1);
        v2.GetProperty("items").GetArrayLength().Should().Be(2);
        var v2Id = v2.GetProperty("id").GetGuid();
        (await PutDraftAsync(v2Id, DraftBody(v2, discountAmount: 500_000))).EnsureSuccessStatusCode();

        var sentV2 = await SendAsync(v2Id);

        sentV2.GetProperty("status").GetString().Should().Be("SENT");
        sentV2.GetProperty("total").GetInt64().Should().Be(2_000_000);
        sentV2.GetProperty("versions").EnumerateArray()
            .Select(v => (v.GetProperty("version").GetInt32(), v.GetProperty("status").GetString()))
            .Should().Equal((1, "SUPERSEDED"), (2, "SENT"));
        var oldV1 = await GetQuotationAsync(v1);
        oldV1.GetProperty("status").GetString().Should().Be("SUPERSEDED");
        oldV1.GetProperty("qrOptions").GetArrayLength().Should().Be(0);
        (await ActivityOfAsync(v1)).Should().Contain(a => a.Action == "quotation.superseded" && !a.Visible);
    }

    [Fact]
    public async Task Second_new_version_while_a_draft_exists_returns_409()   // AC-B7
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var (_, v1) = await CreateSendableDraftAsync();
        await SendAsync(v1);
        await PostCommandAsync(v1, "new-version", new { });

        var resp = await Client.PostAsJsonAsync($"/api/admin/quotations/{v1}/new-version", new { });

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task New_version_after_acceptance_returns_409()   // BR-34
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var (_, id) = await CreateSendableDraftAsync();
        var sent = await SendAsync(id);
        await PostCommandAsync(id, "accept", new { rowVersion = Rv(sent) });

        var resp = await Client.PostAsJsonAsync($"/api/admin/quotations/{id}/new-version", new { });

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("INVALID_TRANSITION");
    }

    [Fact]
    public async Task Snapshot_keeps_the_old_price_after_service_price_change()   // AC-B8
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var serviceName = "Logo AC-B8 " + Guid.NewGuid();
        var svc = await Client.PostAsJsonAsync("/api/admin/services",
            new { name = serviceName, description = "", basePrice = 2_000_000L, defaultIncludedRounds = 3, extraRoundPrice = 200_000L });
        svc.EnsureSuccessStatusCode();
        var serviceId = (await svc.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var projectId = await CreateProjectAsync();
        await CreateDeliverableAsync(projectId, "Logo", serviceId);
        var id = await CreateQuotationAsync(projectId);
        await SendAsync(id);

        (await Client.PutAsJsonAsync($"/api/admin/services/{serviceId}", new
        {
            name = serviceName, description = "", basePrice = 3_000_000L, defaultIncludedRounds = 3, extraRoundPrice = 200_000L, active = true,
        })).EnsureSuccessStatusCode();

        var q = await GetQuotationAsync(id);
        q.GetProperty("snapshot").GetProperty("items")[0].GetProperty("unitPrice").GetInt64().Should().Be(2_000_000);
        q.GetProperty("total").GetInt64().Should().Be(2_000_000);
    }

    [Fact]
    public async Task Qr_keeps_the_snapshot_bank_after_settings_change()   // AC-B9, docs/06 §5
    {
        await LoginAsync();
        await ConfigureBankAsync("970436", "0123456789");
        var (_, id) = await CreateSendableDraftAsync();
        var sent = await SendAsync(id);
        await ConfigureBankAsync("970418", "9999888877");

        var qr = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/quotations/{id}/qr/full");

        var memo = VietQrMemo.For(sent.GetProperty("number").GetString()!, "full");
        qr.GetProperty("memo").GetString().Should().Be(memo);
        qr.GetProperty("amount").GetInt64().Should().Be(2_500_000);
        qr.GetProperty("payload").GetString().Should().Be(
            VietQrPayloadBuilder.Build(new VietQrRequest("970436", "0123456789", 2_500_000, memo)));
        qr.GetProperty("bank").GetProperty("accountNo").GetString().Should().Be("0123456789");
    }

    [Fact]
    public async Task Expired_is_computed_on_read_and_never_stored()   // AC-B10, ADR-009
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var (_, id) = await CreateSendableDraftAsync();
        var sent = await SendAsync(id);
        var expiry = DateOnly.Parse(sent.GetProperty("expiryDate").GetString()!);

        using var later = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddSingleton<IClock>(new FixedClock(expiry.AddDays(1)))));
        var client = later.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        await AuthenticateAsync(client);

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/admin/quotations/{id}");
        detail.GetProperty("effectiveStatus").GetString().Should().Be("EXPIRED");
        detail.GetProperty("status").GetString().Should().Be("SENT");
        detail.GetProperty("qrOptions").GetArrayLength().Should().Be(2);   // BR-75: EXPIRED still shows QR
        var expired = await client.GetFromJsonAsync<JsonElement>("/api/admin/quotations?effectiveStatus=EXPIRED");
        expired.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).Should().Contain(id);
        (await GetQuotationAsync(id)).GetProperty("effectiveStatus").GetString().Should().Be("SENT");   // real clock
    }

    [Fact]
    public async Task Accept_records_date_note_and_visible_activity()   // AC-B11
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var (_, id) = await CreateSendableDraftAsync();
        var sent = await SendAsync(id);

        var q = await PostCommandAsync(id, "accept", new { acceptedAt = "2026-11-09", note = "Khách đồng ý qua Zalo", rowVersion = Rv(sent) });

        q.GetProperty("status").GetString().Should().Be("ACCEPTED");
        q.GetProperty("acceptedAt").GetString().Should().Be("2026-11-09");
        q.GetProperty("acceptedNote").GetString().Should().Be("Khách đồng ý qua Zalo");
        q.GetProperty("outstanding").GetInt64().Should().Be(2_500_000);
        (await ActivityOfAsync(id)).Should().ContainSingle(a => a.Action == "quotation.accepted" && a.Visible);
    }

    [Fact]
    public async Task Accept_with_foreign_evidence_file_returns_400()
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var (_, id) = await CreateSendableDraftAsync();
        var sent = await SendAsync(id);

        var resp = await Client.PostAsJsonAsync($"/api/admin/quotations/{id}/accept",
            new { evidenceFileId = Guid.NewGuid(), rowVersion = Rv(sent) });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reject_requires_a_reason()   // AC-B12
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var (_, id) = await CreateSendableDraftAsync();
        var sent = await SendAsync(id);

        var missing = await Client.PostAsJsonAsync($"/api/admin/quotations/{id}/reject", new { reason = " ", rowVersion = Rv(sent) });
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await missing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("reason", out _).Should().BeTrue();

        var q = await PostCommandAsync(id, "reject", new { reason = "Ngân sách không đủ", rowVersion = Rv(sent) });
        q.GetProperty("status").GetString().Should().Be("REJECTED");
        q.GetProperty("rejectedReason").GetString().Should().Be("Ngân sách không đủ");
        q.GetProperty("qrOptions").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Deposit_paid_leaves_only_the_remaining_qr()   // AC-B13
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var (_, id) = await CreateSendableDraftAsync();
        var sent = await SendAsync(id);
        var accepted = await PostCommandAsync(id, "accept", new { rowVersion = Rv(sent) });

        var q = await PostCommandAsync(id, "payment-status", new { to = "DEPOSIT_PAID", date = "2026-11-10", rowVersion = Rv(accepted) });

        q.GetProperty("paymentStatus").GetString().Should().Be("DEPOSIT_PAID");
        q.GetProperty("depositPaidAt").GetString().Should().Be("2026-11-10");
        var option = q.GetProperty("qrOptions").EnumerateArray().Should().ContainSingle().Subject;
        option.GetProperty("purpose").GetString().Should().Be("remaining");
        option.GetProperty("amount").GetInt64().Should().Be(1_250_000);
        option.GetProperty("memo").GetString().Should().EndWith(" CONLAI");
        (await ActivityOfAsync(id)).Should().ContainSingle(a =>
            a.Action == "quotation.payment_status_changed" && a.Visible && a.Message!.StartsWith("Đã nhận tiền cọc"));
    }

    [Fact]
    public async Task Deposit_paid_without_deposit_returns_422()   // AC-B14
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var (_, id) = await CreateSendableDraftAsync();
        (await PutDraftAsync(id, DraftBody(await GetQuotationAsync(id), depositPercent: 0))).EnsureSuccessStatusCode();
        var sent = await SendAsync(id);
        var accepted = await PostCommandAsync(id, "accept", new { rowVersion = Rv(sent) });

        var resp = await Client.PostAsJsonAsync($"/api/admin/quotations/{id}/payment-status",
            new { to = "DEPOSIT_PAID", rowVersion = Rv(accepted) });

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Payment_status_on_a_sent_quotation_returns_422()   // AC-B15
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var (_, id) = await CreateSendableDraftAsync();
        var sent = await SendAsync(id);

        var resp = await Client.PostAsJsonAsync($"/api/admin/quotations/{id}/payment-status", new { to = "PAID", rowVersion = Rv(sent) });

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Undoing_paid_requires_a_reason()   // AC-B16
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var (_, id) = await CreateSendableDraftAsync();
        var sent = await SendAsync(id);
        var accepted = await PostCommandAsync(id, "accept", new { rowVersion = Rv(sent) });
        var paid = await PostCommandAsync(id, "payment-status", new { to = "PAID", rowVersion = Rv(accepted) });
        paid.GetProperty("qrOptions").GetArrayLength().Should().Be(0);

        var missing = await Client.PostAsJsonAsync($"/api/admin/quotations/{id}/payment-status", new { to = "DEPOSIT_PAID", rowVersion = Rv(paid) });
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var undone = await PostCommandAsync(id, "payment-status", new { to = "DEPOSIT_PAID", reason = "Bấm nhầm", rowVersion = Rv(paid) });
        undone.GetProperty("paymentStatus").GetString().Should().Be("DEPOSIT_PAID");
        undone.GetProperty("paidAt").ValueKind.Should().Be(JsonValueKind.Null);
        var logs = (await ActivityOfAsync(id)).Where(a => a.Action == "quotation.payment_status_changed").ToList();
        logs.Should().HaveCount(2);
        logs[1].Visible.Should().BeFalse();   // undo is not announced to the client
    }

    [Fact]
    public async Task Qr_for_an_unavailable_purpose_returns_422()
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var (_, id) = await CreateSendableDraftAsync();
        await SendAsync(id);

        (await Client.GetAsync($"/api/admin/quotations/{id}/qr/remaining")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Qr_png_is_a_png_download()
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var (_, id) = await CreateSendableDraftAsync();
        var sent = await SendAsync(id);

        var resp = await Client.GetAsync($"/api/admin/quotations/{id}/qr/deposit.png");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        resp.Content.Headers.ContentDisposition!.FileName!.Trim('"')
            .Should().Be($"{sent.GetProperty("number").GetString()!.Replace("-", "")}-deposit.png");
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        bytes.Take(4).Should().Equal(0x89, 0x50, 0x4E, 0x47);
    }

    [Fact]
    public async Task Sending_v2_after_v1_was_rejected_does_not_supersede_and_succeeds()
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var (_, v1) = await CreateSendableDraftAsync();
        var sent = await SendAsync(v1);
        await PostCommandAsync(v1, "reject", new { reason = "Đắt quá", rowVersion = Rv(sent) });
        var v2 = await PostCommandAsync(v1, "new-version", new { });

        var sentV2 = await SendAsync(v2.GetProperty("id").GetGuid());

        sentV2.GetProperty("status").GetString().Should().Be("SENT");
        sentV2.GetProperty("number").GetString().Should().Be(sent.GetProperty("number").GetString());
        (await GetQuotationAsync(v1)).GetProperty("status").GetString().Should().Be("REJECTED");
        (await ActivityOfAsync(v1)).Should().NotContain(a => a.Action == "quotation.superseded");
    }

    [Fact]
    public async Task Sending_a_draft_version_after_an_older_one_was_accepted_returns_409()   // BR-34
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var (_, v1) = await CreateSendableDraftAsync();
        var sent = await SendAsync(v1);
        var v2 = await PostCommandAsync(v1, "new-version", new { });
        await PostCommandAsync(v1, "accept", new { rowVersion = Rv(sent) });
        var v2Id = v2.GetProperty("id").GetGuid();

        var resp = await Client.PostAsJsonAsync($"/api/admin/quotations/{v2Id}/send", new { rowVersion = Rv(await GetQuotationAsync(v2Id)) });

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("INVALID_TRANSITION");
        (await GetQuotationAsync(v2Id)).GetProperty("status").GetString().Should().Be("DRAFT");
        (await GetQuotationAsync(v1)).GetProperty("status").GetString().Should().Be("ACCEPTED");
    }

    private static uint Rv(JsonElement quotation) => quotation.GetProperty("rowVersion").GetUInt32();

    private async Task<JsonElement> PostCommandAsync(Guid id, string action, object body)
    {
        var resp = await Client.PostAsJsonAsync($"/api/admin/quotations/{id}/{action}", body);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Logs a client of a derived host in (GET-only use, so no CSRF headers).</summary>
    private static async Task AuthenticateAsync(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = AppFactory.TestUsername, password = AppFactory.TestPassword });
        resp.EnsureSuccessStatusCode();
        var cookies = resp.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]).ToList();
        var admin = cookies.First(c => c.StartsWith("ff_admin=", StringComparison.Ordinal));
        var csrf = cookies.First(c => c.StartsWith("ff_csrf=", StringComparison.Ordinal));
        client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"{admin}; {csrf}");
    }

    private sealed class FixedClock(DateOnly today) : IClock
    {
        public DateTimeOffset UtcNow => new(today.ToDateTime(new TimeOnly(5, 0)), TimeSpan.Zero);
        public DateOnly TodayVn => today;
    }
}
