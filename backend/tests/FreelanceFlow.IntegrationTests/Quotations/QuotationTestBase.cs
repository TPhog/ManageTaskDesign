using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Quotations;

/// <summary>Designer-side helpers for quotation tests; call <see cref="IntegrationTestBase.LoginAsync"/> first.</summary>
public abstract class QuotationTestBase(AppFactory factory) : IntegrationTestBase(factory)
{
    /// <summary>Seeded by InitialCreate: "Thiết kế Logo" 2.000.000 ₫ and "Thiết kế Banner" 500.000 ₫. Never modify them in tests.</summary>
    protected static readonly Guid LogoServiceId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    protected static readonly Guid BannerServiceId = Guid.Parse("11111111-0000-0000-0000-000000000002");

    protected async Task<Guid> CreateProjectAsync(string name = "Quotation Project")
    {
        var c = await Client.PostAsJsonAsync("/api/admin/clients", new { name = "Quotation Client " + Guid.NewGuid() });
        c.EnsureSuccessStatusCode();
        var clientId = (await c.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var p = await Client.PostAsJsonAsync("/api/admin/projects", new { clientId, name, designType = "BRANDING" });
        p.EnsureSuccessStatusCode();
        return (await p.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    /// <summary>With a service the quota fields are copied from it; without one, 2 free rounds.</summary>
    protected async Task<Guid> CreateDeliverableAsync(Guid projectId, string name, Guid? serviceId = null)
    {
        var resp = await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/deliverables", serviceId is null
            ? new { name, quantity = 1, serviceId, includedRounds = (int?)2 }
            : new { name, quantity = 1, serviceId, includedRounds = (int?)null });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    protected async Task MoveDeliverableAsync(Guid deliverableId, params string[] statuses)
    {
        foreach (var to in statuses)
            (await Client.PostAsJsonAsync($"/api/admin/deliverables/{deliverableId}/status", new { to }))
                .EnsureSuccessStatusCode();
    }

    protected Task<HttpResponseMessage> PostQuotationAsync(Guid projectId, object body) =>
        Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/quotations", body);

    protected async Task<Guid> CreateQuotationAsync(Guid projectId, Guid[]? deliverableIds = null)
    {
        var resp = await PostQuotationAsync(projectId, new { kind = "INITIAL", fromDeliverables = true, deliverableIds });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    protected async Task<JsonElement> GetQuotationAsync(Guid id) =>
        await Client.GetFromJsonAsync<JsonElement>($"/api/admin/quotations/{id}");

    protected Task<HttpResponseMessage> PutDraftAsync(Guid id, object body) =>
        Client.PutAsJsonAsync($"/api/admin/quotations/{id}", body);

    /// <summary>PUT body echoing the draft's lines (or <paramref name="items"/>) with the given money settings.</summary>
    protected static object DraftBody(JsonElement detail, long discountAmount = 0, int vatPercent = 0,
        int depositPercent = 50, IEnumerable<object>? items = null) => new
    {
        title = detail.GetProperty("title").GetString(),
        validityDays = detail.GetProperty("validityDays").GetInt32(),
        discountAmount,
        vatPercent,
        depositPercent,
        terms = "",
        notesToClient = "",
        internalNotes = "",
        items = items ?? ItemsOf(detail),
        rowVersion = detail.GetProperty("rowVersion").GetUInt32(),
    };

    protected static IEnumerable<object> ItemsOf(JsonElement detail) =>
        detail.GetProperty("items").EnumerateArray().Select(i => (object)new
        {
            id = i.GetProperty("id").GetGuid(),
            description = i.GetProperty("description").GetString(),
            details = i.GetProperty("details").GetString(),
            quantity = i.GetProperty("quantity").GetInt32(),
            unitPrice = i.GetProperty("unitPrice").GetInt64(),
            sourceType = i.GetProperty("sourceType").GetString(),
            sourceId = i.GetProperty("sourceId").ValueKind == JsonValueKind.Null ? (Guid?)null : i.GetProperty("sourceId").GetGuid(),
        }).ToList();

    /// <summary>Project with Logo (2.000.000) + Banner (500.000) and an INITIAL draft from them: total 2.500.000, deposit 50%.</summary>
    protected async Task<(Guid ProjectId, Guid QuotationId)> CreateSendableDraftAsync()
    {
        var projectId = await CreateProjectAsync();
        await CreateDeliverableAsync(projectId, "Logo", LogoServiceId);
        await CreateDeliverableAsync(projectId, "Banner", BannerServiceId);
        return (projectId, await CreateQuotationAsync(projectId));
    }

    /// <summary>Sets (or with nulls clears) the receiving account directly in settings.</summary>
    protected async Task ConfigureBankAsync(string? bin = "970436", string? accountNo = "0123456789")
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.Settings.FirstAsync();
        settings.BankBin = bin;
        settings.BankAccountNo = accountNo;
        settings.BankAccountName = bin is null ? null : "NGUYEN VAN A";
        await db.SaveChangesAsync();
    }

    protected async Task<JsonElement> SendAsync(Guid id)
    {
        var rowVersion = (await GetQuotationAsync(id)).GetProperty("rowVersion").GetUInt32();
        var resp = await Client.PostAsJsonAsync($"/api/admin/quotations/{id}/send", new { rowVersion });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    protected async Task<List<(string Action, bool Visible, string? Message)>> ActivityOfAsync(Guid quotationId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.ActivityLogs.AsNoTracking()
            .Where(a => a.EntityId == quotationId)
            .OrderBy(a => a.Id)
            .Select(a => new { a.Action, a.VisibleToClient, a.ClientMessage })
            .ToListAsync();
        return rows.Select(a => (a.Action, a.VisibleToClient, a.ClientMessage)).ToList();
    }
}
