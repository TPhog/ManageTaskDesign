using System.Net;
using System.Text.Json;

namespace FreelanceFlow.IntegrationTests.Revisions;

public class RevisionsTests(AppFactory factory) : RevisionTestBase(factory)
{
    // activity_logs.data is jsonb: Postgres re-serialises it with ": " and ", " separators
    private static string Compact(string json) => json.Replace("\": ", "\":").Replace(", \"", ",\"");

    [Fact]
    public async Task Project_revisions_group_rounds_by_deliverable_with_quota()   // docs/08 …/revisions
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var logo = await CreateQuotaDeliverableAsync(projectId, "Logo", 3);
        await CreateQuotaDeliverableAsync(projectId, "Banner", 2);
        await TriageNewRequestAsync(projectId, Round(logo, ("Logo to hơn", "MINOR", null)), Round(null, ("Sửa footer", "MINOR", null)));

        var rev = await GetRevisionsAsync(projectId);

        var deliverables = rev.GetProperty("deliverables").EnumerateArray().ToList();
        deliverables.Select(d => d.GetProperty("name").GetString()).Should().Equal("Logo", "Banner");
        var logoDto = deliverables[0];
        logoDto.GetProperty("usedRounds").GetInt32().Should().Be(1);
        logoDto.GetProperty("remainingRounds").GetInt32().Should().Be(2);
        logoDto.GetProperty("extraRoundPrice").GetInt64().Should().Be(200_000);
        var item = logoDto.GetProperty("rounds")[0].GetProperty("items")[0];
        item.GetProperty("billing").GetString().Should().Be("IN_QUOTA");
        item.GetProperty("rowVersion").GetUInt32().Should().BeGreaterThan(0);
        item.GetProperty("quotation").ValueKind.Should().Be(JsonValueKind.Null);
        item.GetProperty("tasks").GetArrayLength().Should().Be(0);
        item.GetProperty("suggestedBilling").ValueKind.Should().Be(JsonValueKind.Null);
        deliverables[1].GetProperty("rounds").GetArrayLength().Should().Be(0);
        rev.GetProperty("projectRounds").EnumerateArray().Single().GetProperty("deliverableId").ValueKind.Should().Be(JsonValueKind.Null);
        (await Client.GetAsync($"/api/admin/projects/{Guid.NewGuid()}/revisions")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Manual_round_continues_numbering_and_added_item_follows_round_quota()   // REV-01, POST …/rounds, …/items
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var logo = await CreateQuotaDeliverableAsync(projectId, "Logo", 1);
        await TriageNewRequestAsync(projectId, Round(logo, ("A", "MINOR", null)));   // round 1 uses 1/1

        var round2 = await CreateRoundAsync(projectId, Round(logo, ("B", "MINOR", null)));

        round2.GetProperty("roundNo").GetInt32().Should().Be(2);
        round2.GetProperty("sourceRequestId").ValueKind.Should().Be(JsonValueKind.Null);
        round2.GetProperty("items")[0].GetProperty("billing").GetString().Should().Be("CHARGEABLE");
        round2.GetProperty("items")[0].GetProperty("suggestionReason").GetString()
            .Should().Be("Logo đã dùng 1/1 vòng → Tính phí 200.000 ₫/vòng");

        var round1Id = (await GetRevisionsAsync(projectId)).GetProperty("deliverables")[0].GetProperty("rounds")[0].GetProperty("id").GetGuid();
        var added = await Client.PostAsJsonAsync($"/api/admin/rounds/{round1Id}/items",
            new { description = "C", type = "MINOR", billing = (string?)null });

        added.StatusCode.Should().Be(HttpStatusCode.Created, await added.Content.ReadAsStringAsync());
        var item = await added.Content.ReadFromJsonAsync<JsonElement>();
        item.GetProperty("billing").GetString().Should().Be("IN_QUOTA");
        item.GetProperty("suggestionReason").GetString().Should().Be("Vòng này đã tính quota của Logo — không tốn thêm vòng");
        item.GetProperty("sortOrder").GetInt32().Should().Be(20);
        (await UsedRoundsAsync(projectId, logo)).Should().Be(1);
        (await Client.PostAsJsonAsync($"/api/admin/rounds/{Guid.NewGuid()}/items", new { description = "C", type = "MINOR" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Cancelling_the_only_in_quota_item_gives_the_round_back_with_audit()   // AC-Q6, BR-45
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var logo = await CreateQuotaDeliverableAsync(projectId, "Logo", 3);
        var itemId = (await TriageItemsAsync(projectId, logo, ("A", "MINOR", null)))[0];
        (await UsedRoundsAsync(projectId, logo)).Should().Be(1);

        var resp = await WorkStatusAsync(projectId, itemId, "CANCELLED");

        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("workStatus").GetString().Should().Be("CANCELLED");
        (await UsedRoundsAsync(projectId, logo)).Should().Be(0);
        (await ActivityRowsAsync(itemId)).Should().ContainSingle(a => a.Action == "revision_item.work_status_changed"
            && Compact(a.Data).Contains("\"usedRoundsBefore\":1") && Compact(a.Data).Contains("\"usedRoundsAfter\":0"));
    }

    [Fact]
    public async Task Billing_change_moves_quota_and_is_audited_with_before_and_after()   // BR-45
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var logo = await CreateQuotaDeliverableAsync(projectId, "Logo", 3);
        var itemId = (await TriageItemsAsync(projectId, logo, ("A", "MINOR", null)))[0];

        var resp = await PutItemAsync(projectId, itemId, "FREE_FIX");

        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("billing").GetString().Should().Be("FREE_FIX");
        (await UsedRoundsAsync(projectId, logo)).Should().Be(0);
        (await ActivityRowsAsync(itemId)).Should().ContainSingle(a => a.Action == "revision_item.updated"
            && Compact(a.Data).Contains("\"before\":{\"type\":\"MINOR\",\"billing\":\"IN_QUOTA\"}")
            && Compact(a.Data).Contains("\"usedRoundsAfter\":0"));
    }

    [Fact]
    public async Task Chargeable_item_without_quotation_hits_scope_guard_until_overridden()   // AC-S1, AC-S2, BR-60
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var logo = await CreateQuotaDeliverableAsync(projectId, "Logo", 0);
        var itemId = (await TriageItemsAsync(projectId, logo, ("Thêm tagline", "MAJOR", null)))[0];

        var noReason = await WorkStatusAsync(projectId, itemId, "IN_PROGRESS");
        var shortReason = await WorkStatusAsync(projectId, itemId, "IN_PROGRESS", "gấp");
        var overridden = await WorkStatusAsync(projectId, itemId, "IN_PROGRESS", "Khách đã đồng ý qua Zalo");

        noReason.StatusCode.Should().Be((HttpStatusCode)428);
        (await noReason.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("SCOPE_GUARD");
        shortReason.StatusCode.Should().Be((HttpStatusCode)428);
        overridden.StatusCode.Should().Be(HttpStatusCode.OK, await overridden.Content.ReadAsStringAsync());
        var item = await overridden.Content.ReadFromJsonAsync<JsonElement>();
        item.GetProperty("workStatus").GetString().Should().Be("IN_PROGRESS");
        item.GetProperty("scopeOverrideReason").GetString().Should().Be("Khách đã đồng ý qua Zalo");
        (await ActivityRowsAsync(itemId)).Should().ContainSingle(a =>
            a.Action == "revision_item.scope_override" && a.Data.Contains("Khách đã đồng ý qua Zalo"));
    }

    [Fact]
    public async Task In_quota_item_goes_to_done_without_reason_and_invalid_moves_conflict()   // AC-S6
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var logo = await CreateQuotaDeliverableAsync(projectId, "Logo", 3);
        var itemId = (await TriageItemsAsync(projectId, logo, ("A", "MINOR", null)))[0];

        (await WorkStatusAsync(projectId, itemId, "DONE")).StatusCode.Should().Be(HttpStatusCode.Conflict);   // TODO → DONE not in BR-60 diagram
        (await WorkStatusAsync(projectId, itemId, "IN_PROGRESS")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await WorkStatusAsync(projectId, itemId, "DONE")).StatusCode.Should().Be(HttpStatusCode.OK);
        var cancel = await WorkStatusAsync(projectId, itemId, "CANCELLED");

        cancel.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await cancel.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("INVALID_TRANSITION");
        (await Client.PostAsJsonAsync($"/api/admin/revision-items/{itemId}/work-status", new { to = "LATER", rowVersion = 1u }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Project_level_item_cannot_become_in_quota()   // AC-Q5 via PUT, BR-41
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var itemId = (await TriageItemsAsync(projectId, null, ("Sửa footer", "MINOR", null)))[0];

        var resp = await PutItemAsync(projectId, itemId, "IN_QUOTA");

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("BUSINESS_RULE");
    }

    [Fact]
    public async Task Stale_row_version_on_a_revision_item_returns_409()   // xmin on revision_items
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var logo = await CreateQuotaDeliverableAsync(projectId, "Logo", 3);
        var itemId = (await TriageItemsAsync(projectId, logo, ("A", "MINOR", null)))[0];
        var stale = (await GetItemAsync(projectId, itemId)).GetProperty("rowVersion").GetUInt32();
        await TouchRowAsync("revision_items", itemId);

        var put = await Client.PutAsJsonAsync($"/api/admin/revision-items/{itemId}",
            new { description = "A2", type = "MINOR", billing = "IN_QUOTA", internalNotes = "", rowVersion = stale });
        var move = await Client.PostAsJsonAsync($"/api/admin/revision-items/{itemId}/work-status",
            new { to = "IN_PROGRESS", rowVersion = stale });

        put.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await put.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("CONCURRENCY_CONFLICT");
        move.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await GetItemAsync(projectId, itemId)).GetProperty("description").GetString().Should().Be("A");
    }

    [Fact]
    public async Task Item_update_validates_fields()
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var itemId = (await TriageItemsAsync(projectId, null, ("A", "MINOR", null)))[0];

        var resp = await Client.PutAsJsonAsync($"/api/admin/revision-items/{itemId}",
            new { description = "A", type = "HUGE", billing = (string?)null, internalNotes = "", rowVersion = 1u });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        errors.TryGetProperty("type", out _).Should().BeTrue();
        errors.TryGetProperty("billing", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Task_created_from_an_item_is_linked_both_ways()   // REV-06
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var logo = await CreateQuotaDeliverableAsync(projectId, "Logo", 3);
        var itemId = (await TriageItemsAsync(projectId, logo, ("Đổi màu chữ sang xanh navy", "MINOR", null)))[0];

        var resp = await Client.PostAsJsonAsync($"/api/admin/revision-items/{itemId}/tasks", new { });

        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        var task = await resp.Content.ReadFromJsonAsync<JsonElement>();
        task.GetProperty("title").GetString().Should().Be("Đổi màu chữ sang xanh navy");
        task.GetProperty("revisionItemId").GetGuid().Should().Be(itemId);
        task.GetProperty("deliverableId").GetGuid().Should().Be(logo);
        task.GetProperty("status").GetString().Should().Be("TODO");
        var linked = (await GetItemAsync(projectId, itemId)).GetProperty("tasks").EnumerateArray().Single();
        linked.GetProperty("id").GetGuid().Should().Be(task.GetProperty("id").GetGuid());
        (await ActivityRowsAsync(task.GetProperty("id").GetGuid())).Should().ContainSingle(a =>
            a.Action == "task.created" && a.Data.Contains(itemId.ToString()));
        (await Client.PostAsJsonAsync($"/api/admin/revision-items/{itemId}/tasks", new { priority = "SOON" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
