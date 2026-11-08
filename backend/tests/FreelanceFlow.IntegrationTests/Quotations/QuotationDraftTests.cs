using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Quotations;

public class QuotationDraftTests(AppFactory factory) : QuotationTestBase(factory)
{
    [Fact]
    public async Task Create_from_whole_project_uses_active_deliverables_and_service_prices()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var logo = await CreateDeliverableAsync(projectId, "Logo", LogoServiceId);
        await CreateDeliverableAsync(projectId, "Banner", BannerServiceId);
        var dropped = await CreateDeliverableAsync(projectId, "Poster bỏ");
        await MoveDeliverableAsync(dropped, "CANCELLED");

        var id = await CreateQuotationAsync(projectId);
        var q = await GetQuotationAsync(id);

        q.GetProperty("status").GetString().Should().Be("DRAFT");
        q.GetProperty("effectiveStatus").GetString().Should().Be("DRAFT");
        q.GetProperty("kind").GetString().Should().Be("INITIAL");
        q.GetProperty("number").ValueKind.Should().Be(JsonValueKind.Null);
        q.GetProperty("title").GetString().Should().Be("Báo giá Quotation Project");
        q.GetProperty("validityDays").GetInt32().Should().Be(7);       // settings default
        q.GetProperty("depositPercent").GetInt32().Should().Be(50);    // settings default
        var items = q.GetProperty("items").EnumerateArray().ToList();
        items.Select(i => i.GetProperty("description").GetString()).Should().Equal("Logo", "Banner");
        items[0].GetProperty("unitPrice").GetInt64().Should().Be(2_000_000);
        items[0].GetProperty("sourceType").GetString().Should().Be("DELIVERABLE");
        items[0].GetProperty("sourceId").GetGuid().Should().Be(logo);
        q.GetProperty("total").GetInt64().Should().Be(2_500_000);
        q.GetProperty("depositAmount").GetInt64().Should().Be(1_250_000);
        q.GetProperty("snapshot").ValueKind.Should().Be(JsonValueKind.Null);
        q.GetProperty("qrOptions").GetArrayLength().Should().Be(0);    // BR-75: no QR for a DRAFT
        (await ActivityOfAsync(id)).Should().ContainSingle(a => a.Action == "quotation.created" && !a.Visible);
    }

    [Fact]
    public async Task Create_for_selected_finished_deliverables_quotes_only_those()   // D1
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync("Dự án D1");
        var logo = await CreateDeliverableAsync(projectId, "Logo", LogoServiceId);
        var banner = await CreateDeliverableAsync(projectId, "Banner", BannerServiceId);
        await CreateDeliverableAsync(projectId, "Poster", BannerServiceId);
        await MoveDeliverableAsync(logo, "IN_PROGRESS", "IN_REVIEW", "APPROVED", "DELIVERED");
        await MoveDeliverableAsync(banner, "IN_PROGRESS", "IN_REVIEW", "APPROVED");

        var resp = await PostQuotationAsync(projectId, new { kind = "INITIAL", fromDeliverables = true, deliverableIds = new[] { logo, banner } });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await resp.Content.ReadFromJsonAsync<JsonElement>();
        created.GetProperty("warnings").GetArrayLength().Should().Be(0);
        var q = await GetQuotationAsync(created.GetProperty("id").GetGuid());
        q.GetProperty("kind").GetString().Should().Be("INITIAL");
        q.GetProperty("title").GetString().Should().Be("Báo giá hạng mục đã hoàn thành — Dự án D1");
        q.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("sourceId").GetGuid()).Should().Equal(logo, banner);
        q.GetProperty("subtotal").GetInt64().Should().Be(2_500_000);
    }

    [Fact]
    public async Task Create_for_unfinished_deliverable_returns_422()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var poster = await CreateDeliverableAsync(projectId, "Poster", BannerServiceId);

        var resp = await PostQuotationAsync(projectId, new { kind = "INITIAL", fromDeliverables = true, deliverableIds = new[] { poster } });

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("BUSINESS_RULE");
    }

    [Fact]
    public async Task Create_with_deliverable_of_another_project_returns_400()
    {
        await LoginAsync();
        var projectA = await CreateProjectAsync();
        var projectB = await CreateProjectAsync();
        var foreign = await CreateDeliverableAsync(projectB, "Logo", LogoServiceId);

        var resp = await PostQuotationAsync(projectA, new { kind = "INITIAL", fromDeliverables = true, deliverableIds = new[] { foreign } });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("deliverableIds", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Create_warns_when_a_deliverable_is_already_quoted()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        await CreateDeliverableAsync(projectId, "Logo", LogoServiceId);
        await CreateQuotationAsync(projectId);

        var resp = await PostQuotationAsync(projectId, new { kind = "INITIAL", fromDeliverables = true });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var warnings = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("warnings")
            .EnumerateArray().Select(w => w.GetString()).ToList();
        warnings.Should().ContainSingle().Which.Should().Contain("Logo");
    }

    [Fact]
    public async Task Create_additional_kind_is_rejected_until_revisions_exist()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();

        var resp = await PostQuotationAsync(projectId, new { kind = "ADDITIONAL", fromDeliverables = false });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("kind", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Create_for_unknown_project_returns_404()
    {
        await LoginAsync();

        var resp = await PostQuotationAsync(Guid.NewGuid(), new { kind = "INITIAL", fromDeliverables = true });

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_recalculates_money_like_AC_B1()
    {
        await LoginAsync();
        var (_, id) = await CreateSendableDraftAsync();
        var draft = await GetQuotationAsync(id);

        var resp = await PutDraftAsync(id, DraftBody(draft, discountAmount: 100_000, depositPercent: 50));

        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        var q = await resp.Content.ReadFromJsonAsync<JsonElement>();
        q.GetProperty("subtotal").GetInt64().Should().Be(2_500_000);
        q.GetProperty("total").GetInt64().Should().Be(2_400_000);
        q.GetProperty("depositAmount").GetInt64().Should().Be(1_200_000);
        q.GetProperty("rowVersion").GetUInt32().Should().NotBe(draft.GetProperty("rowVersion").GetUInt32());
        (await ActivityOfAsync(id)).Should().Contain(a => a.Action == "quotation.updated");
    }

    [Fact]
    public async Task Update_rounds_vat_half_up_like_AC_B2()
    {
        await LoginAsync();
        var (_, id) = await CreateSendableDraftAsync();
        var draft = await GetQuotationAsync(id);

        var resp = await PutDraftAsync(id, DraftBody(draft, vatPercent: 8, depositPercent: 0, items:
        [
            new { id = (Guid?)null, description = "Gói thiết kế", details = "", quantity = 1, unitPrice = 1_234_567L, sourceType = "MANUAL", sourceId = (Guid?)null },
        ]));

        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        var q = await resp.Content.ReadFromJsonAsync<JsonElement>();
        q.GetProperty("vatAmount").GetInt64().Should().Be(98_765);
        q.GetProperty("total").GetInt64().Should().Be(1_333_332);
        q.GetProperty("items").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Update_keeps_ids_of_existing_rows()
    {
        await LoginAsync();
        var (_, id) = await CreateSendableDraftAsync();
        var draft = await GetQuotationAsync(id);
        var before = draft.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();

        var q = await (await PutDraftAsync(id, DraftBody(draft))).Content.ReadFromJsonAsync<JsonElement>();

        q.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).Should().Equal(before);
    }

    [Fact]
    public async Task Update_with_discount_above_subtotal_returns_400()
    {
        await LoginAsync();
        var (_, id) = await CreateSendableDraftAsync();
        var draft = await GetQuotationAsync(id);

        var resp = await PutDraftAsync(id, DraftBody(draft, discountAmount: 9_999_999));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public async Task Update_with_out_of_range_vat_returns_field_error()
    {
        await LoginAsync();
        var (_, id) = await CreateSendableDraftAsync();
        var draft = await GetQuotationAsync(id);

        var resp = await PutDraftAsync(id, DraftBody(draft, vatPercent: 25));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("vatPercent", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Update_with_stale_row_version_returns_409()
    {
        await LoginAsync();
        var (_, id) = await CreateSendableDraftAsync();
        var draft = await GetQuotationAsync(id);
        (await PutDraftAsync(id, DraftBody(draft))).EnsureSuccessStatusCode();

        var stale = await PutDraftAsync(id, DraftBody(draft, discountAmount: 1_000));

        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await stale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task Delete_draft_removes_quotation_and_items()
    {
        await LoginAsync();
        var (_, id) = await CreateSendableDraftAsync();

        var resp = await Client.DeleteAsync($"/api/admin/quotations/{id}");

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.GetAsync($"/api/admin/quotations/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.QuotationItems.CountAsync(i => i.QuotationId == id)).Should().Be(0);
        (await ActivityOfAsync(id)).Should().Contain(a => a.Action == "quotation.deleted");
    }

    [Fact]
    public async Task List_filters_by_project_and_reports_effective_status()
    {
        await LoginAsync();
        var (projectA, a) = await CreateSendableDraftAsync();
        await CreateSendableDraftAsync();

        var list = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/quotations?projectId={projectA}");

        list.GetProperty("total").GetInt32().Should().Be(1);
        var item = list.GetProperty("items")[0];
        item.GetProperty("id").GetGuid().Should().Be(a);
        item.GetProperty("effectiveStatus").GetString().Should().Be("DRAFT");
        item.GetProperty("outstanding").GetInt64().Should().Be(0);
        item.GetProperty("projectName").GetString().Should().Be("Quotation Project");
        item.GetProperty("clientName").GetString().Should().StartWith("Quotation Client");
    }

    [Fact]
    public async Task Payment_filter_ignores_quotations_that_are_not_accepted()
    {
        await LoginAsync();
        var (projectA, _) = await CreateSendableDraftAsync();

        var list = await Client.GetFromJsonAsync<JsonElement>(
            $"/api/admin/quotations?projectId={projectA}&paymentStatus=UNPAID");

        list.GetProperty("total").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Get_unknown_quotation_returns_404()
    {
        await LoginAsync();

        (await Client.GetAsync($"/api/admin/quotations/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
