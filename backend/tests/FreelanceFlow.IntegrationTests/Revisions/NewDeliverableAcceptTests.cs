using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Revisions;

/// <summary>BR-48 (plan 09): an accepted NEW_DELIVERABLE line becomes a real deliverable, once, in the accept transaction.</summary>
public class NewDeliverableAcceptTests(AppFactory factory) : RevisionTestBase(factory)
{
    private async Task<Guid> CreateTemplateServiceAsync(string name)
    {
        var resp = await Client.PostAsJsonAsync("/api/admin/services", new
        {
            name, description = "", basePrice = 300_000, defaultIncludedRounds = 2, extraRoundPrice = 100_000,
            taskTemplate = new[] { "Phác thảo", "Hoàn thiện" },
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task SetProjectStatusAsync(Guid projectId, string status)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Projects.Where(p => p.Id == projectId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, status));
    }

    private async Task<List<(string Title, string Status)>> TasksOfAsync(Guid deliverableId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.Tasks.AsNoTracking().Where(t => t.DeliverableId == deliverableId).OrderBy(t => t.SortOrder)
            .Select(t => new { t.Title, t.Status }).ToListAsync()).Select(t => (t.Title, t.Status)).ToList();
    }

    private async Task<HttpResponseMessage> AcceptAsync(Guid quotationId)
    {
        var rowVersion = (await GetQuotationAsync(quotationId)).GetProperty("rowVersion").GetUInt32();
        return await Client.PostAsJsonAsync($"/api/admin/quotations/{quotationId}/accept", new { rowVersion });
    }

    private async Task<List<JsonElement>> DeliverablesAsync(Guid projectId) =>
        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}")).GetProperty("deliverables").EnumerateArray().ToList();

    /// <summary>Sent ADDITIONAL quotation in <paramref name="projectId"/>: "Thiết kế sticker" with the template service, "Thêm menu A5" typed at 250.000.</summary>
    private async Task<(Guid QuotationId, List<Guid> ItemIds)> SendAdditionalAsync(Guid projectId, Guid serviceId)
    {
        var ids = await TriageItemsAsync(projectId, null, ("Thiết kế sticker", "NEW_DELIVERABLE", null), ("Thêm menu A5", "NEW_DELIVERABLE", null));
        var resp = await PostFromRevisionsAsync(projectId, new
        {
            revisionItemIds = ids,
            newDeliverableServices = new object[]
            {
                new { revisionItemId = ids[0], serviceId = (Guid?)serviceId, unitPrice = (long?)null },
                new { revisionItemId = ids[1], serviceId = (Guid?)null, unitPrice = (long?)250_000 },
            },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        var quotationId = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await SendAsync(quotationId);
        return (quotationId, ids);
    }

    /// <summary>Project forced to <paramref name="projectStatus"/> with a sent ADDITIONAL quotation (see <see cref="SendAdditionalAsync"/>).</summary>
    private async Task<(Guid ProjectId, Guid QuotationId, Guid ServiceId, List<Guid> ItemIds)> SentAdditionalAsync(string projectStatus)
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var serviceId = await CreateTemplateServiceAsync("Sticker " + Guid.NewGuid().ToString("N")[..6]);
        var projectId = await CreateProjectAsync();
        await SetProjectStatusAsync(projectId, projectStatus);
        var (quotationId, ids) = await SendAdditionalAsync(projectId, serviceId);
        return (projectId, quotationId, serviceId, ids);
    }

    [Fact]
    public async Task Accepting_turns_new_deliverable_items_into_deliverables_with_service_quota_and_template_tasks()   // AC-Q11
    {
        var (projectId, quotationId, serviceId, ids) = await SentAdditionalAsync("IN_PROGRESS");
        var before = await DeliverablesAsync(projectId);
        var pending = (await GetQuotationAsync(quotationId)).GetProperty("newDeliverables").EnumerateArray().ToList();
        pending.Select(n => n.GetProperty("revisionItemId").GetGuid()).Should().Equal(ids);
        pending.Select(n => n.GetProperty("itemDescription").GetString()).Should().Equal("Thiết kế sticker", "Thêm menu A5");
        pending.Should().OnlyContain(n => n.GetProperty("createdDeliverableId").ValueKind == JsonValueKind.Null);

        var accepted = await AcceptAsync(quotationId);

        accepted.StatusCode.Should().Be(HttpStatusCode.OK, await accepted.Content.ReadAsStringAsync());
        var created = (await DeliverablesAsync(projectId)).Skip(before.Count).ToList();
        created.Should().HaveCount(2);
        var sticker = created[0];
        sticker.GetProperty("name").GetString().Should().Be("Thiết kế sticker");   // the item, not the line "Sticker …"
        sticker.GetProperty("serviceId").GetGuid().Should().Be(serviceId);
        sticker.GetProperty("includedRounds").GetInt32().Should().Be(2);
        sticker.GetProperty("extraRoundPrice").GetInt64().Should().Be(100_000);
        sticker.GetProperty("status").GetString().Should().Be("IN_PROGRESS");   // started project: like "Bắt đầu dự án"
        var menu = created[1];
        menu.GetProperty("name").GetString().Should().Be("Thêm menu A5");
        menu.GetProperty("serviceId").ValueKind.Should().Be(JsonValueKind.Null);
        menu.GetProperty("includedRounds").GetInt32().Should().Be(0);
        menu.GetProperty("status").GetString().Should().Be("IN_PROGRESS");

        var stickerId = sticker.GetProperty("id").GetGuid();
        var stickerItem = await GetItemAsync(projectId, ids[0]);
        stickerItem.GetProperty("createdDeliverableId").GetGuid().Should().Be(stickerId);
        stickerItem.GetProperty("createdDeliverableName").GetString().Should().Be("Thiết kế sticker");
        stickerItem.GetProperty("workStatus").GetString().Should().Be("DONE");
        (await GetItemAsync(projectId, ids[1])).GetProperty("workStatus").GetString().Should().Be("DONE");
        (await TasksOfAsync(stickerId)).Should().Equal(("Phác thảo", "TODO"), ("Hoàn thiện", "TODO"));
        (await TasksOfAsync(menu.GetProperty("id").GetGuid())).Should().BeEmpty();

        var log = await ActivityRowsAsync(stickerId);
        log.Should().ContainSingle(a => a.Action == "deliverable.created");
        log.Single(a => a.Action == "deliverable.created").Data.Should().Contain("quotation.accepted")
            .And.Contain(quotationId.ToString()).And.Contain("IN_PROGRESS");
        (await ActivityRowsAsync(ids[0])).Single(a => a.Action == "revision_item.work_status_changed").Data
            .Should().Contain("DONE").And.Contain(stickerId.ToString());
        (await ActivityRowsAsync(quotationId)).Single(a => a.Action == "quotation.accepted").Data.Should().Contain(stickerId.ToString());
        (await GetQuotationAsync(quotationId)).GetProperty("newDeliverables").EnumerateArray()
            .Select(n => n.GetProperty("createdDeliverableId").GetGuid()).Should().Equal(stickerId, menu.GetProperty("id").GetGuid());
    }

    [Theory]   // controller decision: ON_HOLD starts like IN_PROGRESS; not started / completed wait for the designer
    [InlineData("ON_HOLD", "IN_PROGRESS", 2)]
    [InlineData("QUOTED", "NOT_STARTED", 0)]
    [InlineData("COMPLETED", "NOT_STARTED", 0)]
    public async Task The_project_status_decides_whether_the_new_deliverable_starts(string projectStatus, string deliverableStatus, int tasks)
    {
        var (projectId, quotationId, _, ids) = await SentAdditionalAsync(projectStatus);

        (await AcceptAsync(quotationId)).StatusCode.Should().Be(HttpStatusCode.OK);

        var stickerId = (await GetItemAsync(projectId, ids[0])).GetProperty("createdDeliverableId").GetGuid();
        (await DeliverablesAsync(projectId)).Single(d => d.GetProperty("id").GetGuid() == stickerId)
            .GetProperty("status").GetString().Should().Be(deliverableStatus);
        (await TasksOfAsync(stickerId)).Should().HaveCount(tasks);
    }

    [Fact]
    public async Task In_a_project_not_started_the_template_waits_for_start_project()   // AC-Q12
    {
        var (projectId, quotationId, _, ids) = await SentAdditionalAsync("DRAFT");

        (await AcceptAsync(quotationId)).StatusCode.Should().Be(HttpStatusCode.OK);

        var stickerId = (await GetItemAsync(projectId, ids[0])).GetProperty("createdDeliverableId").GetGuid();
        (await TasksOfAsync(stickerId)).Should().BeEmpty();
        (await DeliverablesAsync(projectId)).Single(d => d.GetProperty("id").GetGuid() == stickerId)
            .GetProperty("status").GetString().Should().Be("NOT_STARTED");
        var preview = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}/start-preview");
        preview.EnumerateArray().Single(p => p.GetProperty("deliverableId").GetGuid() == stickerId)
            .GetProperty("titles").EnumerateArray().Select(t => t.GetString()).Should().Equal("Phác thảo", "Hoàn thiện");
    }

    [Fact]
    public async Task In_a_cancelled_project_the_accept_records_the_skip_and_creates_nothing()   // controller decision
    {
        var (projectId, quotationId, _, ids) = await SentAdditionalAsync("IN_PROGRESS");
        await SetProjectStatusAsync(projectId, "CANCELLED");   // cancelled after the quotation was sent

        (await AcceptAsync(quotationId)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await DeliverablesAsync(projectId)).Should().BeEmpty();
        foreach (var id in ids)
        {
            var item = await GetItemAsync(projectId, id);
            item.GetProperty("createdDeliverableId").ValueKind.Should().Be(JsonValueKind.Null);
            item.GetProperty("workStatus").GetString().Should().Be("TODO");
        }
        (await ActivityRowsAsync(quotationId)).Single(a => a.Action == "quotation.accepted").Data
            .Should().Contain("PROJECT_CANCELLED").And.Contain(ids[0].ToString());
    }

    [Fact]
    public async Task A_new_deliverable_started_by_accept_reaches_submit_review_on_the_flow_card()   // controller decision 1
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var serviceId = await CreateTemplateServiceAsync("Sticker " + Guid.NewGuid().ToString("N")[..6]);
        var projectId = await CreateProjectAsync();
        await CreateDeliverableAsync(projectId, "Logo", LogoServiceId);
        var briefId = (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}")).GetProperty("brief").GetProperty("id").GetGuid();
        (await Client.PutAsJsonAsync($"/api/admin/briefs/{briefId}", new { content = "Brief" })).EnsureSuccessStatusCode();
        (await Client.PostAsync($"/api/admin/briefs/{briefId}/confirm", null)).EnsureSuccessStatusCode();
        var initial = await CreateQuotationAsync(projectId);
        await SendAsync(initial);
        (await AcceptAsync(initial)).StatusCode.Should().Be(HttpStatusCode.OK);
        var projectRowVersion = (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}")).GetProperty("rowVersion").GetUInt32();
        (await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/start", new { rowVersion = projectRowVersion, tasks = Array.Empty<object>() }))
            .EnsureSuccessStatusCode();

        var (quotationId, ids) = await SendAdditionalAsync(projectId, serviceId);
        (await AcceptAsync(quotationId)).StatusCode.Should().Be(HttpStatusCode.OK);
        var stickerId = (await GetItemAsync(projectId, ids[0])).GetProperty("createdDeliverableId").GetGuid();

        async Task<List<JsonElement>> ActionsAsync() =>
            (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}/flow")).GetProperty("actions").EnumerateArray().ToList();
        (await ActionsAsync()).Should().NotContain(a => a.GetProperty("kind").GetString() == "SUBMIT_REVIEW");

        var taskIds = (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks?projectId={projectId}&pageSize=100"))
            .GetProperty("items").EnumerateArray()
            .Where(t => t.GetProperty("deliverableId").ValueKind != JsonValueKind.Null && t.GetProperty("deliverableId").GetGuid() == stickerId)
            .Select(t => t.GetProperty("id").GetGuid()).ToList();
        taskIds.Should().HaveCount(2);
        for (var i = 0; i < taskIds.Count; i++)
            (await Client.PostAsJsonAsync($"/api/admin/tasks/{taskIds[i]}/move", new { status = "DONE", sortOrder = 1024.0 * (i + 1) }))
                .EnsureSuccessStatusCode();

        var sticker = (await DeliverablesAsync(projectId)).Single(d => d.GetProperty("id").GetGuid() == stickerId);
        sticker.GetProperty("progressPercent").GetInt32().Should().Be(100);
        var submit = (await ActionsAsync()).Single(a => a.GetProperty("kind").GetString() == "SUBMIT_REVIEW");
        submit.GetProperty("label").GetString().Should().Be("Gửi duyệt: Thiết kế sticker");
        submit.GetProperty("target").GetProperty("deliverableId").GetGuid().Should().Be(stickerId);
    }

    [Fact]
    public async Task Two_concurrent_accepts_create_the_deliverables_once()   // xmin on quotations + revision_items
    {
        var (projectId, quotationId, _, ids) = await SentAdditionalAsync("IN_PROGRESS");
        var rowVersion = (await GetQuotationAsync(quotationId)).GetProperty("rowVersion").GetUInt32();

        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            Client.PostAsJsonAsync($"/api/admin/quotations/{quotationId}/accept", new { rowVersion })));

        responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
        (await DeliverablesAsync(projectId)).Should().HaveCount(2);
        var stickerId = (await GetItemAsync(projectId, ids[0])).GetProperty("createdDeliverableId").GetGuid();
        (await TasksOfAsync(stickerId)).Should().HaveCount(2);
    }

    [Fact]
    public async Task Rejected_quotations_create_nothing_and_a_converted_item_cannot_change_type()   // AC-Q13
    {
        var (projectId, quotationId, _, ids) = await SentAdditionalAsync("IN_PROGRESS");
        var count = (await DeliverablesAsync(projectId)).Count;
        var onSentLine = await PutItemAsync(projectId, ids[0], "CHARGEABLE", type: "SCOPE_CHANGE");   // BR-61 lock extended
        onSentLine.StatusCode.Should().Be(HttpStatusCode.Conflict, await onSentLine.Content.ReadAsStringAsync());
        (await onSentLine.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("BILLING_LOCKED");
        var rowVersion = (await GetQuotationAsync(quotationId)).GetProperty("rowVersion").GetUInt32();
        (await Client.PostAsJsonAsync($"/api/admin/quotations/{quotationId}/reject", new { reason = "Khách chưa cần", rowVersion }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await DeliverablesAsync(projectId)).Should().HaveCount(count);
        (await GetItemAsync(projectId, ids[0])).GetProperty("createdDeliverableId").ValueKind.Should().Be(JsonValueKind.Null);

        var (projectB, quotationB, _, idsB) = await SentAdditionalAsync("IN_PROGRESS");
        (await AcceptAsync(quotationB)).StatusCode.Should().Be(HttpStatusCode.OK);
        var afterFirst = (await DeliverablesAsync(projectB)).Count;
        (await AcceptAsync(quotationB)).StatusCode.Should().Be(HttpStatusCode.Conflict);   // ACCEPTED → ACCEPTED is not a transition
        (await DeliverablesAsync(projectB)).Should().HaveCount(afterFirst);

        var retype = await PutItemAsync(projectB, idsB[0], "CHARGEABLE", type: "MINOR");
        retype.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await retype.Content.ReadAsStringAsync());
        (await retype.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("BUSINESS_RULE");
    }
}
