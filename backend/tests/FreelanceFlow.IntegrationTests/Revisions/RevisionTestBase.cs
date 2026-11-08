using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.IntegrationTests.Quotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Revisions;

/// <summary>Designer-side helpers for request/revision tests; call LoginAsync first. Quotation helpers come from QuotationTestBase.</summary>
public abstract class RevisionTestBase(AppFactory factory) : QuotationTestBase(factory)
{
    /// <summary>Deliverable without a service: explicit quota and price per extra round.</summary>
    protected async Task<Guid> CreateQuotaDeliverableAsync(Guid projectId, string name, int includedRounds, long extraRoundPrice = 200_000)
    {
        var resp = await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/deliverables",
            new { name, quantity = 1, includedRounds, extraRoundPrice });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    protected Task<HttpResponseMessage> PostRequestAsync(Guid projectId, object body) =>
        Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/requests", body);

    /// <summary>Request recorded by the designer (source ZALO); returns the RequestDetailDto.</summary>
    protected async Task<JsonElement> CreateRequestAsync(Guid projectId, string content = "Sửa logo", Guid? deliverableId = null)
    {
        var resp = await PostRequestAsync(projectId, new { content, source = "ZALO", deliverableId });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    protected static Guid IdOf(JsonElement requestDetail) => requestDetail.GetProperty("request").GetProperty("id").GetGuid();

    protected async Task<JsonElement> GetRequestAsync(Guid requestId) =>
        await Client.GetFromJsonAsync<JsonElement>($"/api/admin/requests/{requestId}");

    protected async Task<uint> RequestRowVersionAsync(Guid requestId) =>
        (await GetRequestAsync(requestId)).GetProperty("request").GetProperty("rowVersion").GetUInt32();

    /// <summary>One round body; items are (description, type, billing-or-null).</summary>
    protected static object Round(Guid? deliverableId, params (string Description, string Type, string? Billing)[] items) => new
    {
        deliverableId,
        receivedAt = (string?)null,
        items = items.Select(i => new { description = i.Description, type = i.Type, billing = i.Billing }).ToArray(),
    };

    protected async Task<HttpResponseMessage> PostTriageAsync(Guid requestId, params object[] rounds) =>
        await Client.PostAsJsonAsync($"/api/admin/requests/{requestId}/triage",
            new { rounds, rowVersion = await RequestRowVersionAsync(requestId) });

    /// <summary>Records a request and triages it into <paramref name="rounds"/>; returns the RequestDetailDto.</summary>
    protected async Task<JsonElement> TriageNewRequestAsync(Guid projectId, params object[] rounds)
    {
        var requestId = IdOf(await CreateRequestAsync(projectId));
        var resp = await PostTriageAsync(requestId, rounds);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Triages a new request into one round of <paramref name="items"/>; returns the item ids in order.</summary>
    protected async Task<List<Guid>> TriageItemsAsync(Guid projectId, Guid? deliverableId, params (string Description, string Type, string? Billing)[] items) =>
        ItemsOfRounds(await TriageNewRequestAsync(projectId, Round(deliverableId, items)))
            .Select(i => i.GetProperty("id").GetGuid()).ToList();

    protected static List<JsonElement> ItemsOfRounds(JsonElement detail) =>
        detail.GetProperty("rounds").EnumerateArray().SelectMany(r => r.GetProperty("items").EnumerateArray()).ToList();

    protected async Task<HttpResponseMessage> DeclineAsync(Guid requestId, string reason) =>
        await Client.PostAsJsonAsync($"/api/admin/requests/{requestId}/decline",
            new { reason, rowVersion = await RequestRowVersionAsync(requestId) });

    /// <summary>usedRounds of a deliverable as GET /api/admin/projects/{id} reports it.</summary>
    protected async Task<int> UsedRoundsAsync(Guid projectId, Guid deliverableId)
    {
        var project = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}");
        return project.GetProperty("deliverables").EnumerateArray()
            .Single(d => d.GetProperty("id").GetGuid() == deliverableId).GetProperty("usedRounds").GetInt32();
    }

    protected async Task<JsonElement> GetRevisionsAsync(Guid projectId) =>
        await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}/revisions");

    /// <summary>Current state (rowVersion included) of one revision item from GET …/revisions.</summary>
    protected async Task<JsonElement> GetItemAsync(Guid projectId, Guid itemId)
    {
        var rev = await GetRevisionsAsync(projectId);
        var rounds = rev.GetProperty("deliverables").EnumerateArray()
            .SelectMany(d => d.GetProperty("rounds").EnumerateArray())
            .Concat(rev.GetProperty("projectRounds").EnumerateArray());
        return rounds.SelectMany(r => r.GetProperty("items").EnumerateArray())
            .Single(i => i.GetProperty("id").GetGuid() == itemId);
    }

    protected async Task<JsonElement> CreateRoundAsync(Guid projectId, object round)
    {
        var resp = await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/rounds", round);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    protected async Task<HttpResponseMessage> WorkStatusAsync(Guid projectId, Guid itemId, string to, string? overrideReason = null)
    {
        var rowVersion = (await GetItemAsync(projectId, itemId)).GetProperty("rowVersion").GetUInt32();
        return await Client.PostAsJsonAsync($"/api/admin/revision-items/{itemId}/work-status", new { to, overrideReason, rowVersion });
    }

    protected async Task<HttpResponseMessage> PutItemAsync(Guid projectId, Guid itemId, string billing, string? type = null)
    {
        var current = await GetItemAsync(projectId, itemId);
        return await Client.PutAsJsonAsync($"/api/admin/revision-items/{itemId}", new
        {
            description = current.GetProperty("description").GetString(),
            type = type ?? current.GetProperty("type").GetString(),
            billing,
            internalNotes = current.GetProperty("internalNotes").GetString(),
            rowVersion = current.GetProperty("rowVersion").GetUInt32(),
        });
    }

    protected Task<HttpResponseMessage> PostFromRevisionsAsync(Guid projectId, object body) =>
        Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/quotations/from-revisions", body);

    protected async Task<List<(string Action, bool Visible, string? Message, string Data)>> ActivityRowsAsync(Guid entityId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.ActivityLogs.AsNoTracking()
            .Where(a => a.EntityId == entityId)
            .OrderBy(a => a.Id)
            .Select(a => new { a.Action, a.VisibleToClient, a.ClientMessage, a.Data })
            .ToListAsync();
        return rows.Select(a => (a.Action, a.VisibleToClient, a.ClientMessage, a.Data)).ToList();
    }

    /// <summary>Bumps xmin of a row behind the API's back (another tab saved first).</summary>
    protected async Task TouchRowAsync(string table, Guid id)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync("UPDATE " + table + " SET updated_at = updated_at WHERE id = {0}", id);
    }
}
