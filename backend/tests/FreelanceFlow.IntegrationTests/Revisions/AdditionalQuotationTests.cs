using System.Net;
using System.Text.Json;

namespace FreelanceFlow.IntegrationTests.Revisions;

public class AdditionalQuotationTests(AppFactory factory) : RevisionTestBase(factory)
{
    private static Guid IdFrom(JsonElement created) => created.GetProperty("id").GetGuid();

    private async Task<Guid> CreateAdditionalAsync(Guid projectId, object body)
    {
        var resp = await PostFromRevisionsAsync(projectId, body);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        return IdFrom(await resp.Content.ReadFromJsonAsync<JsonElement>());
    }

    [Fact]
    public async Task Rejected_items_detail_is_capped_at_five_truncated_descriptions()   // B9
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var logo = await CreateQuotaDeliverableAsync(projectId, "Logo", 10);
        var longText = new string('x', 200);
        var ids = await TriageItemsAsync(projectId, logo,
            Enumerable.Range(1, 7).Select(i => ($"{i}-{longText}", "MINOR", (string?)"IN_QUOTA")).ToArray());

        var resp = await PostFromRevisionsAsync(projectId, new { revisionItemIds = ids });

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var detail = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString()!;
        detail.Should().EndWith("… và 2 mục khác");
        System.Text.RegularExpressions.Regex.Matches(detail, "-x{10}").Count.Should().Be(5);
        detail.Should().NotContain(new string('x', 81));
    }

    [Fact]
    public async Task From_revisions_builds_round_and_new_deliverable_lines_and_links_every_item()   // AC-Q8, BR-46
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var logo = await CreateQuotaDeliverableAsync(projectId, "Logo", 3);
        await TriageNewRequestAsync(projectId,
            Round(logo, ("R1", "MINOR", null)), Round(logo, ("R2", "MINOR", null)), Round(logo, ("R3", "MINOR", null)));   // 3/3
        var ids = await TriageItemsAsync(projectId, logo,
            ("Đổi màu", "MINOR", null), ("Đổi font", "MINOR", null), ("Căn lề", "MINOR", null),
            ("Banner Facebook", "NEW_DELIVERABLE", null));

        var quotationId = await CreateAdditionalAsync(projectId, new
        {
            revisionItemIds = ids,
            newDeliverableServices = new[] { new { revisionItemId = ids[3], serviceId = BannerServiceId } },
        });

        var q = await GetQuotationAsync(quotationId);
        q.GetProperty("kind").GetString().Should().Be("ADDITIONAL");
        q.GetProperty("status").GetString().Should().Be("DRAFT");
        q.GetProperty("title").GetString().Should().Be("Báo giá phát sinh — Quotation Project");
        var lines = q.GetProperty("items").EnumerateArray().ToList();
        lines.Select(l => (l.GetProperty("description").GetString(), l.GetProperty("unitPrice").GetInt64()))
            .Should().Equal(("Vòng sửa thứ 4 — Logo", 200_000L), ("Thiết kế Banner", 500_000L));
        lines.Should().OnlyContain(l => l.GetProperty("sourceType").GetString() == "REVISION");
        q.GetProperty("total").GetInt64().Should().Be(700_000);
        foreach (var id in ids)
        {
            var link = (await GetItemAsync(projectId, id)).GetProperty("quotation");
            link.GetProperty("id").GetGuid().Should().Be(quotationId);
            link.GetProperty("status").GetString().Should().Be("DRAFT");
        }
        (await GetItemAsync(projectId, ids[0])).GetProperty("quotationItemId").GetGuid().Should().Be(lines[0].GetProperty("id").GetGuid());
        (await GetItemAsync(projectId, ids[3])).GetProperty("quotationItemId").GetGuid().Should().Be(lines[1].GetProperty("id").GetGuid());
        (await ActivityOfAsync(quotationId)).Should().ContainSingle(a => a.Action == "quotation.created");
    }

    [Fact]
    public async Task From_revisions_rejects_items_that_cannot_be_quoted()   // BR-46
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var logo = await CreateQuotaDeliverableAsync(projectId, "Logo", 3);
        var inQuota = (await TriageItemsAsync(projectId, logo, ("A", "MINOR", null)))[0];
        var chargeable = await TriageItemsAsync(projectId, null, ("Footer", "MINOR", null), ("Header", "MINOR", null));

        var notChargeable = await PostFromRevisionsAsync(projectId, new { revisionItemIds = new[] { inQuota } });
        var empty = await PostFromRevisionsAsync(projectId, new { revisionItemIds = Array.Empty<Guid>() });
        var unknown = await PostFromRevisionsAsync(projectId, new { revisionItemIds = new[] { Guid.NewGuid() } });
        var serviceOnMinor = await PostFromRevisionsAsync(projectId, new
        {
            revisionItemIds = new[] { chargeable[1] },
            newDeliverableServices = new[] { new { revisionItemId = chargeable[1], serviceId = BannerServiceId } },
        });
        await CreateAdditionalAsync(projectId, new { revisionItemIds = new[] { chargeable[0] } });
        var alreadyQuoted = await PostFromRevisionsAsync(projectId, new { revisionItemIds = new[] { chargeable[0] } });

        notChargeable.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await notChargeable.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("BUSINESS_RULE");
        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        unknown.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        serviceOnMinor.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        alreadyQuoted.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await PostFromRevisionsAsync(Guid.NewGuid(), new { revisionItemIds = new[] { chargeable[1] } }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deleting_the_additional_draft_unlinks_its_items()   // AC-B18
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var ids = await TriageItemsAsync(projectId, null, ("Footer", "MINOR", null));
        var quotationId = await CreateAdditionalAsync(projectId, new { revisionItemIds = ids });

        (await Client.DeleteAsync($"/api/admin/quotations/{quotationId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var item = await GetItemAsync(projectId, ids[0]);
        item.GetProperty("quotationItemId").ValueKind.Should().Be(JsonValueKind.Null);
        item.GetProperty("quotation").ValueKind.Should().Be(JsonValueKind.Null);
        (await PostFromRevisionsAsync(projectId, new { revisionItemIds = ids })).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Sending_a_new_version_moves_links_and_dropped_lines_unlink()   // AC-B6, BR-47
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var projectId = await CreateProjectAsync();
        var logo = await CreateQuotaDeliverableAsync(projectId, "Logo", 0);
        var roundItem = (await TriageItemsAsync(projectId, logo, ("Đổi màu", "MINOR", null)))[0];
        var scopeItem = (await TriageItemsAsync(projectId, null, ("Bản tiếng Anh", "SCOPE_CHANGE", null)))[0];
        var v1 = await CreateAdditionalAsync(projectId, new { revisionItemIds = new[] { roundItem, scopeItem } });
        await SendAsync(v1);

        var nv = await Client.PostAsync($"/api/admin/quotations/{v1}/new-version", null);
        nv.StatusCode.Should().Be(HttpStatusCode.OK, await nv.Content.ReadAsStringAsync());
        var v2 = await nv.Content.ReadFromJsonAsync<JsonElement>();
        var v2Id = v2.GetProperty("id").GetGuid();
        var roundLine = v2.GetProperty("items").EnumerateArray()
            .Single(l => l.GetProperty("description").GetString()!.StartsWith("Vòng sửa thứ 1"));
        var put = await PutDraftAsync(v2Id, DraftBody(v2, items: new object[]
        {
            new
            {
                id = roundLine.GetProperty("id").GetGuid(),
                description = roundLine.GetProperty("description").GetString(),
                details = roundLine.GetProperty("details").GetString(),
                quantity = 1,
                unitPrice = 250_000L,
                sourceType = "REVISION",
                sourceId = roundLine.GetProperty("sourceId").GetGuid(),
            },
        }));
        put.StatusCode.Should().Be(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
        (await GetItemAsync(projectId, roundItem)).GetProperty("quotation").GetProperty("version").GetInt32()
            .Should().Be(1, "links stay on v1 until v2 is sent (BR-47)");

        await SendAsync(v2Id);

        var moved = await GetItemAsync(projectId, roundItem);
        moved.GetProperty("quotation").GetProperty("id").GetGuid().Should().Be(v2Id);
        moved.GetProperty("quotation").GetProperty("version").GetInt32().Should().Be(2);
        moved.GetProperty("quotation").GetProperty("status").GetString().Should().Be("SENT");
        moved.GetProperty("quotationItemId").GetGuid().Should().Be(roundLine.GetProperty("id").GetGuid());
        (await GetItemAsync(projectId, scopeItem)).GetProperty("quotationItemId").ValueKind.Should().Be(JsonValueKind.Null);
        (await GetQuotationAsync(v1)).GetProperty("status").GetString().Should().Be("SUPERSEDED");
        (await ActivityOfAsync(v2Id)).Should().ContainSingle(a => a.Action == "quotation.revision_links_moved");
    }

    [Fact]
    public async Task Sending_a_new_version_leaves_items_linked_to_another_quotation_alone()   // BR-47 scope
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var projectId = await CreateProjectAsync();
        var ids = await TriageItemsAsync(projectId, null, ("Footer", "MINOR", null), ("Header", "MINOR", null));
        var qA = await CreateAdditionalAsync(projectId, new { revisionItemIds = new[] { ids[0] } });
        var qB = await CreateAdditionalAsync(projectId, new { revisionItemIds = new[] { ids[1] } });
        await SendAsync(qA);
        var before = (await GetItemAsync(projectId, ids[1])).GetProperty("quotationItemId").GetGuid();

        var nv = await Client.PostAsync($"/api/admin/quotations/{qA}/new-version", null);
        nv.StatusCode.Should().Be(HttpStatusCode.OK, await nv.Content.ReadAsStringAsync());
        var v2Id = (await nv.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await SendAsync(v2Id);

        var other = await GetItemAsync(projectId, ids[1]);
        other.GetProperty("quotationItemId").GetGuid().Should().Be(before);
        other.GetProperty("quotation").GetProperty("id").GetGuid().Should().Be(qB);
        (await GetItemAsync(projectId, ids[0])).GetProperty("quotation").GetProperty("id").GetGuid().Should().Be(v2Id);
    }

    [Fact]
    public async Task Accepted_quotation_lifts_scope_guard_and_sent_quotation_locks_billing()   // AC-S3, AC-S4, AC-S5
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var projectId = await CreateProjectAsync();
        var logo = await CreateQuotaDeliverableAsync(projectId, "Logo", 0);
        var itemId = (await TriageItemsAsync(projectId, logo, ("Thêm tagline", "MAJOR", null)))[0];
        var quotationId = await CreateAdditionalAsync(projectId, new { revisionItemIds = new[] { itemId } });
        await SendAsync(quotationId);

        (await WorkStatusAsync(projectId, itemId, "IN_PROGRESS")).StatusCode.Should().Be((HttpStatusCode)428);   // AC-S3
        var locked = await PutItemAsync(projectId, itemId, "IN_QUOTA");
        locked.StatusCode.Should().Be(HttpStatusCode.Conflict);   // AC-S5
        (await locked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("BILLING_LOCKED");

        var rowVersion = (await GetQuotationAsync(quotationId)).GetProperty("rowVersion").GetUInt32();
        (await Client.PostAsJsonAsync($"/api/admin/quotations/{quotationId}/accept", new { rowVersion }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var started = await WorkStatusAsync(projectId, itemId, "IN_PROGRESS");   // AC-S4
        started.StatusCode.Should().Be(HttpStatusCode.OK, await started.Content.ReadAsStringAsync());
        (await started.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("scopeOverrideReason").ValueKind
            .Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Concurrent_from_revisions_on_the_same_items_creates_one_draft()   // xmin on revision_items
    {
        await LoginAsync();
        var projectId = await CreateProjectAsync();
        var ids = await TriageItemsAsync(projectId, null, ("Footer", "MINOR", null), ("Header", "MINOR", null));

        var responses = await Task.WhenAll(Enumerable.Range(0, 2)
            .Select(_ => PostFromRevisionsAsync(projectId, new { revisionItemIds = ids })));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        responses.Should().ContainSingle(r =>
            r.StatusCode == HttpStatusCode.Conflict || r.StatusCode == HttpStatusCode.UnprocessableEntity);
        var list = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/quotations?projectId={projectId}");
        list.GetProperty("total").GetInt32().Should().Be(1, "the losing draft is rolled back with its transaction");
    }
}
