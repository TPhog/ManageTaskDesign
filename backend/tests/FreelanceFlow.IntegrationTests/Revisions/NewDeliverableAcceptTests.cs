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

    /// <summary>Started project, sent ADDITIONAL quotation: "Sticker" with the template service, "Menu A5" typed at 250.000.</summary>
    private async Task<(Guid ProjectId, Guid QuotationId, Guid ServiceId, List<Guid> ItemIds)> SentAdditionalAsync(string projectStatus)
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var serviceId = await CreateTemplateServiceAsync("Sticker " + Guid.NewGuid().ToString("N")[..6]);
        var projectId = await CreateProjectAsync();
        await SetProjectStatusAsync(projectId, projectStatus);
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
        return (projectId, quotationId, serviceId, ids);
    }

    [Fact]
    public async Task Accepting_turns_new_deliverable_items_into_deliverables_with_service_quota_and_template_tasks()   // AC-Q11
    {
        var (projectId, quotationId, serviceId, ids) = await SentAdditionalAsync("IN_PROGRESS");
        var before = await DeliverablesAsync(projectId);
        var pending = (await GetQuotationAsync(quotationId)).GetProperty("newDeliverables").EnumerateArray().ToList();
        pending.Select(n => n.GetProperty("revisionItemId").GetGuid()).Should().Equal(ids);
        pending.Should().OnlyContain(n => n.GetProperty("createdDeliverableId").ValueKind == JsonValueKind.Null);

        var accepted = await AcceptAsync(quotationId);

        accepted.StatusCode.Should().Be(HttpStatusCode.OK, await accepted.Content.ReadAsStringAsync());
        var created = (await DeliverablesAsync(projectId)).Skip(before.Count).ToList();
        created.Should().HaveCount(2);
        var sticker = created[0];
        sticker.GetProperty("serviceId").GetGuid().Should().Be(serviceId);
        sticker.GetProperty("includedRounds").GetInt32().Should().Be(2);
        sticker.GetProperty("extraRoundPrice").GetInt64().Should().Be(100_000);
        sticker.GetProperty("status").GetString().Should().Be("NOT_STARTED");
        var menu = created[1];
        menu.GetProperty("name").GetString().Should().Be("Thêm menu A5");
        menu.GetProperty("serviceId").ValueKind.Should().Be(JsonValueKind.Null);
        menu.GetProperty("includedRounds").GetInt32().Should().Be(0);

        var stickerId = sticker.GetProperty("id").GetGuid();
        (await GetItemAsync(projectId, ids[0])).GetProperty("createdDeliverableId").GetGuid().Should().Be(stickerId);
        (await GetItemAsync(projectId, ids[0])).GetProperty("createdDeliverableName").GetString().Should().Be(sticker.GetProperty("name").GetString());
        (await TasksOfAsync(stickerId)).Should().Equal(("Phác thảo", "TODO"), ("Hoàn thiện", "TODO"));
        (await TasksOfAsync(menu.GetProperty("id").GetGuid())).Should().BeEmpty();

        var log = await ActivityRowsAsync(stickerId);
        log.Should().ContainSingle(a => a.Action == "deliverable.created");
        log.Single(a => a.Action == "deliverable.created").Data.Should().Contain("quotation.accepted").And.Contain(quotationId.ToString());
        (await ActivityRowsAsync(quotationId)).Single(a => a.Action == "quotation.accepted").Data.Should().Contain(stickerId.ToString());
        (await GetQuotationAsync(quotationId)).GetProperty("newDeliverables").EnumerateArray()
            .Select(n => n.GetProperty("createdDeliverableId").GetGuid()).Should().Equal(stickerId, menu.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task In_a_project_not_started_the_template_waits_for_start_project()   // AC-Q12
    {
        var (projectId, quotationId, _, ids) = await SentAdditionalAsync("DRAFT");

        (await AcceptAsync(quotationId)).StatusCode.Should().Be(HttpStatusCode.OK);

        var stickerId = (await GetItemAsync(projectId, ids[0])).GetProperty("createdDeliverableId").GetGuid();
        (await TasksOfAsync(stickerId)).Should().BeEmpty();
        var preview = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}/start-preview");
        preview.EnumerateArray().Single(p => p.GetProperty("deliverableId").GetGuid() == stickerId)
            .GetProperty("titles").EnumerateArray().Select(t => t.GetString()).Should().Equal("Phác thảo", "Hoàn thiện");
    }

    [Fact]
    public async Task Rejected_quotations_create_nothing_and_a_converted_item_cannot_change_type()   // AC-Q13
    {
        var (projectId, quotationId, _, ids) = await SentAdditionalAsync("IN_PROGRESS");
        var count = (await DeliverablesAsync(projectId)).Count;
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
