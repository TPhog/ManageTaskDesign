using System.Net;
using System.Text.Json;

namespace FreelanceFlow.IntegrationTests.Revisions;

public class RequestsTests(AppFactory factory) : RevisionTestBase(factory)
{
    private async Task<int> NewCountAsync() =>
        (await Client.GetFromJsonAsync<JsonElement>("/api/admin/requests/new-count")).GetProperty("count").GetInt32();

    [Fact]
    public async Task Designer_records_a_request_on_behalf_of_the_client()   // REQ-02, REQ-03 badge
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var logo = await CreateQuotaDeliverableAsync(projectId, "Logo", 3);
        var countBefore = await NewCountAsync();

        var detail = await CreateRequestAsync(projectId, "  Đổi màu chữ sang xanh navy  ", logo);

        var request = detail.GetProperty("request");
        request.GetProperty("status").GetString().Should().Be("NEW");
        request.GetProperty("content").GetString().Should().Be("Đổi màu chữ sang xanh navy");
        request.GetProperty("source").GetString().Should().Be("ZALO");
        request.GetProperty("createdBy").GetString().Should().Be("DESIGNER");
        request.GetProperty("deliverableName").GetString().Should().Be("Logo");
        request.GetProperty("clientName").GetString().Should().StartWith("Quotation Client");
        request.GetProperty("rowVersion").GetUInt32().Should().BeGreaterThan(0);
        request.GetProperty("attachments").GetArrayLength().Should().Be(0);
        detail.GetProperty("rounds").GetArrayLength().Should().Be(0);
        (await NewCountAsync()).Should().Be(countBefore + 1);
        (await ActivityRowsAsync(IdOf(detail))).Should().ContainSingle(a => a.Action == "request.created" && !a.Visible);
    }

    [Theory]
    [InlineData("", "ZALO", "content")]
    [InlineData("Đổi màu", "PORTAL", "source")]
    public async Task Create_request_validates_content_and_source(string content, string source, string field)
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();

        var resp = await PostRequestAsync(projectId, new { content, source });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty(field, out _).Should().BeTrue();
    }

    [Fact]
    public async Task Create_request_rejects_a_deliverable_of_another_project_and_cancelled_projects()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var foreign = await CreateQuotaDeliverableAsync(await CreateProjectAsync("Other"), "Logo", 3);

        (await PostRequestAsync(projectId, new { content = "Sửa", source = "EMAIL", deliverableId = foreign }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PostRequestAsync(Guid.NewGuid(), new { content = "Sửa", source = "EMAIL" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/status", new { to = "CANCELLED", reason = "Khách huỷ" }))
            .EnsureSuccessStatusCode();
        (await PostRequestAsync(projectId, new { content = "Sửa", source = "PHONE" }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task List_filters_by_status_and_project()   // REQ-03
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var newId = IdOf(await CreateRequestAsync(projectId, "Mới"));
        var declinedId = IdOf(await CreateRequestAsync(projectId, "Sẽ từ chối"));
        (await DeclineAsync(declinedId, "Ngoài phạm vi")).StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/requests?status=NEW&projectId={projectId}");

        page.GetProperty("total").GetInt32().Should().Be(1);
        var item = page.GetProperty("items")[0];
        item.GetProperty("id").GetGuid().Should().Be(newId);
        item.GetProperty("projectId").GetGuid().Should().Be(projectId);
        var declined = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/requests?status=DECLINED&projectId={projectId}");
        declined.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).Should().Equal(declinedId);
        (await Client.GetAsync("/api/admin/requests?status=OPEN")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.GetAsync($"/api/admin/requests/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Decline_requires_a_reason_and_is_shown_to_the_client()   // BR-52, AC-R4 (admin side)
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var id = IdOf(await CreateRequestAsync(projectId));

        (await Client.PostAsJsonAsync($"/api/admin/requests/{id}/decline", new { reason = " ", rowVersion = await RequestRowVersionAsync(id) }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var resp = await DeclineAsync(id, "Ngoài phạm vi hợp đồng");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var request = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("request");
        request.GetProperty("status").GetString().Should().Be("DECLINED");
        request.GetProperty("declineReason").GetString().Should().Be("Ngoài phạm vi hợp đồng");
        (await ActivityRowsAsync(id)).Should().ContainSingle(a =>
            a.Action == "request.declined" && a.Visible && a.Message == "Yêu cầu bị từ chối: Ngoài phạm vi hợp đồng");
        var again = await DeclineAsync(id, "Lần nữa");
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);   // BR-50
        (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("INVALID_TRANSITION");
    }

    [Fact]
    public async Task Triage_with_null_billing_puts_one_round_in_quota()   // AC-Q1, BR-51
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var logo = await CreateQuotaDeliverableAsync(projectId, "Logo", 3);
        var requestId = IdOf(await CreateRequestAsync(projectId, "5 chỗ cần sửa", logo));

        var resp = await PostTriageAsync(requestId,
            Round(logo, Enumerable.Range(1, 5).Select(i => ($"Sửa {i}", "MINOR", (string?)null)).ToArray()));

        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        var detail = await resp.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("request").GetProperty("status").GetString().Should().Be("TRIAGED");
        detail.GetProperty("request").GetProperty("triagedAt").ValueKind.Should().Be(JsonValueKind.String);
        var round = detail.GetProperty("rounds").EnumerateArray().Single();
        round.GetProperty("roundNo").GetInt32().Should().Be(1);
        round.GetProperty("deliverableName").GetString().Should().Be("Logo");
        round.GetProperty("sourceRequestId").GetGuid().Should().Be(requestId);
        round.GetProperty("counted").GetBoolean().Should().BeTrue();
        var items = round.GetProperty("items").EnumerateArray().ToList();
        items.Should().HaveCount(5).And.OnlyContain(i =>
            i.GetProperty("billing").GetString() == "IN_QUOTA" && i.GetProperty("workStatus").GetString() == "TODO");
        items[0].GetProperty("suggestionReason").GetString().Should().Be("Logo đã dùng 0/3 vòng → Trong quota");
        items[1].GetProperty("suggestionReason").GetString().Should().Be("Vòng này đã tính quota của Logo — không tốn thêm vòng");
        (await UsedRoundsAsync(projectId, logo)).Should().Be(1);
        (await ActivityRowsAsync(requestId)).Should().Contain(a =>
            a.Action == "request.triaged" && a.Visible && a.Message == "Yêu cầu của bạn đã được tiếp nhận");
        (await ActivityRowsAsync(round.GetProperty("id").GetGuid())).Should().ContainSingle(a =>
            a.Action == "revision_round.created" && a.Data.Contains("\"usedRoundsBefore\": 0") && a.Data.Contains("\"usedRoundsAfter\": 1"));
    }

    [Fact]
    public async Task Exhausted_quota_suggests_chargeable_and_keeps_used_rounds()   // AC-Q2, rounds counted in sequence
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var logo = await CreateQuotaDeliverableAsync(projectId, "Logo", 3);
        var first = await TriageNewRequestAsync(projectId,
            Round(logo, ("R1", "MINOR", null)), Round(logo, ("R2", "MINOR", null)), Round(logo, ("R3", "MINOR", null)));
        first.GetProperty("rounds").EnumerateArray().Select(r => r.GetProperty("roundNo").GetInt32()).Should().Equal(1, 2, 3);
        (await UsedRoundsAsync(projectId, logo)).Should().Be(3);

        var detail = await TriageNewRequestAsync(projectId, Round(logo, ("Đổi bố cục", "MAJOR", null)));

        var round = detail.GetProperty("rounds")[0];
        round.GetProperty("roundNo").GetInt32().Should().Be(4);
        round.GetProperty("counted").GetBoolean().Should().BeFalse();
        var item = round.GetProperty("items")[0];
        item.GetProperty("billing").GetString().Should().Be("CHARGEABLE");
        item.GetProperty("suggestionReason").GetString().Should().Be("Logo đã dùng 3/3 vòng → Tính phí 200.000 ₫/vòng");
        (await UsedRoundsAsync(projectId, logo)).Should().Be(3);
    }

    [Fact]
    public async Task One_round_uses_one_quota_round_and_free_fix_uses_none()   // AC-Q3, AC-Q4, BR-42/43
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var logo = await CreateQuotaDeliverableAsync(projectId, "Logo", 3);

        await TriageNewRequestAsync(projectId,
            Round(logo, ("A", "MINOR", "IN_QUOTA"), ("B", "MAJOR", "IN_QUOTA"), ("C", "MINOR", "CHARGEABLE")));
        (await UsedRoundsAsync(projectId, logo)).Should().Be(1);

        await TriageNewRequestAsync(projectId, Round(logo, ("Lỗi chính tả do designer", "MINOR", "FREE_FIX")));
        (await UsedRoundsAsync(projectId, logo)).Should().Be(1);
    }

    [Fact]
    public async Task Project_level_round_rejects_in_quota_items()   // AC-Q5, BR-41
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var requestId = IdOf(await CreateRequestAsync(projectId));

        var rejected = await PostTriageAsync(requestId, Round(null, ("Sửa footer", "MINOR", "IN_QUOTA")));

        rejected.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await rejected.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("BUSINESS_RULE");
        (await GetRequestAsync(requestId)).GetProperty("request").GetProperty("status").GetString().Should().Be("NEW");

        var ok = await PostTriageAsync(requestId, Round(null, ("Sửa footer", "MINOR", null)));
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        var round = (await ok.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rounds")[0];
        round.GetProperty("deliverableId").ValueKind.Should().Be(JsonValueKind.Null);
        round.GetProperty("roundNo").GetInt32().Should().Be(1);
        round.GetProperty("items")[0].GetProperty("billing").GetString().Should().Be("CHARGEABLE");
    }

    [Fact]
    public async Task Triage_splits_one_request_across_deliverables()   // BR-40, BR-51
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var logo = await CreateQuotaDeliverableAsync(projectId, "Logo", 3);
        var banner = await CreateQuotaDeliverableAsync(projectId, "Banner", 2);

        var detail = await TriageNewRequestAsync(projectId,
            Round(logo, ("Logo to hơn", "MINOR", null)), Round(banner, ("Banner đổi nền", "MINOR", null)));

        detail.GetProperty("rounds").EnumerateArray().Select(r => (r.GetProperty("deliverableName").GetString(), r.GetProperty("roundNo").GetInt32()))
            .Should().BeEquivalentTo(new[] { ("Logo", 1), ("Banner", 1) });
        (await UsedRoundsAsync(projectId, logo)).Should().Be(1);
        (await UsedRoundsAsync(projectId, banner)).Should().Be(1);
    }

    [Fact]
    public async Task Triaged_request_cannot_be_triaged_again()   // AC-R3
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var requestId = IdOf(await CreateRequestAsync(projectId));
        (await PostTriageAsync(requestId, Round(null, ("A", "MINOR", null)))).StatusCode.Should().Be(HttpStatusCode.OK);

        var again = await PostTriageAsync(requestId, Round(null, ("B", "MINOR", null)));

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("INVALID_TRANSITION");
    }

    [Fact]
    public async Task Triage_and_decline_reject_a_stale_row_version()   // xmin on client_requests
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var requestId = IdOf(await CreateRequestAsync(projectId));
        var stale = await RequestRowVersionAsync(requestId);
        await TouchRowAsync("client_requests", requestId);

        var triage = await Client.PostAsJsonAsync($"/api/admin/requests/{requestId}/triage",
            new { rounds = new[] { Round(null, ("A", "MINOR", null)) }, rowVersion = stale });
        var decline = await Client.PostAsJsonAsync($"/api/admin/requests/{requestId}/decline", new { reason = "Không", rowVersion = stale });

        triage.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await triage.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("CONCURRENCY_CONFLICT");
        decline.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var detail = await GetRequestAsync(requestId);
        detail.GetProperty("request").GetProperty("status").GetString().Should().Be("NEW");
        detail.GetProperty("rounds").GetArrayLength().Should().Be(0);   // nothing of the failed triage was saved
    }

    [Fact]
    public async Task Triage_validates_rounds_items_and_deliverables()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var requestId = IdOf(await CreateRequestAsync(projectId));
        var foreign = await CreateQuotaDeliverableAsync(await CreateProjectAsync("Other"), "Logo", 3);

        var empty = await Client.PostAsJsonAsync($"/api/admin/requests/{requestId}/triage",
            new { rounds = Array.Empty<object>(), rowVersion = await RequestRowVersionAsync(requestId) });
        var badItem = await PostTriageAsync(requestId, Round(null, ("", "HUGE", "FREE")));
        var foreignRound = await PostTriageAsync(requestId, Round(foreign, ("A", "MINOR", null)));

        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        badItem.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = (await badItem.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        foreach (var key in new[] { "rounds[0].items[0].description", "rounds[0].items[0].type", "rounds[0].items[0].billing" })
            errors.TryGetProperty(key, out _).Should().BeTrue(key);
        foreignRound.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await foreignRound.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public async Task Suggest_billing_previews_without_saving()   // docs/05 §3.7.1, docs/08 §2.3
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var logo = await CreateQuotaDeliverableAsync(projectId, "Logo", 1);
        await TriageNewRequestAsync(projectId, Round(logo, ("R1", "MINOR", null)));

        var resp = await Client.PostAsJsonAsync("/api/admin/revisions/suggest-billing",
            new { deliverableId = logo, items = new[] { new { type = "MINOR" }, new { type = "NEW_DELIVERABLE" } } });
        var projectLevel = await Client.PostAsJsonAsync("/api/admin/revisions/suggest-billing",
            new { deliverableId = (Guid?)null, items = new[] { new { type = "MAJOR" } } });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()
            .Select(s => (s.GetProperty("billing").GetString(), s.GetProperty("reason").GetString()))
            .Should().Equal(("CHARGEABLE", "Logo đã dùng 1/1 vòng → Tính phí 200.000 ₫/vòng"), ("CHARGEABLE", "Hạng mục mới luôn tính phí"));
        (await projectLevel.Content.ReadFromJsonAsync<JsonElement>())[0].GetProperty("reason").GetString()
            .Should().Be("Vòng sửa cấp dự án không dùng quota");
        (await UsedRoundsAsync(projectId, logo)).Should().Be(1);
        (await Client.PostAsJsonAsync("/api/admin/revisions/suggest-billing", new { deliverableId = logo, items = Array.Empty<object>() }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Deliverable_update_reports_the_same_used_rounds_as_project_detail()   // single used_rounds query, BR-44
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var logo = await CreateQuotaDeliverableAsync(projectId, "Logo", 3);
        await TriageNewRequestAsync(projectId, Round(logo, ("A", "MINOR", null)));

        var put = await Client.PutAsJsonAsync($"/api/admin/deliverables/{logo}",
            new { name = "Logo", quantity = 1, includedRounds = 0, extraRoundPrice = 200_000, dueDate = (string?)null, sortOrder = 10 });

        put.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await put.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("deliverable").GetProperty("usedRounds").GetInt32().Should().Be(1);
        body.GetProperty("deliverable").GetProperty("remainingRounds").GetInt32().Should().Be(0);
        body.GetProperty("warnings").GetArrayLength().Should().Be(1);
        (await UsedRoundsAsync(projectId, logo)).Should().Be(1);
    }
}
