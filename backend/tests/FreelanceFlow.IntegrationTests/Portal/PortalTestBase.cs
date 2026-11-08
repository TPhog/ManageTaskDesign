using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Portal;

/// <summary>
/// Helpers for portal tests. Designer-side helpers use <see cref="IntegrationTestBase.Client"/> and need
/// <see cref="IntegrationTestBase.LoginAsync"/> first; portal sessions get their own HttpClient.
/// </summary>
public abstract class PortalTestBase(AppFactory factory) : IntegrationTestBase(factory)
{
    protected HttpClient CreateBareClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = false,
    });

    protected async Task<(Guid ClientId, string Token)> CreateClientWithPortalLinkAsync(string? name = null)
    {
        var resp = await Client.PostAsJsonAsync("/api/admin/clients", new
        {
            name = name ?? "Portal Client " + Guid.NewGuid(),
            notes = "SECRET-CLIENT-NOTE",
        });
        resp.EnsureSuccessStatusCode();
        var clientId = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        return (clientId, await IssueLinkAsync(clientId));
    }

    /// <summary>Creates or regenerates the client's magic link; returns the raw token.</summary>
    protected async Task<string> IssueLinkAsync(Guid clientId)
    {
        var resp = await Client.PostAsync($"/api/admin/clients/{clientId}/portal-link", null);
        resp.EnsureSuccessStatusCode();
        var url = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!;
        return url[(url.LastIndexOf('/') + 1)..];
    }

    protected Task<HttpResponseMessage> PostSessionAsync(string token) =>
        CreateBareClient().PostAsJsonAsync("/api/portal/session", new { token });

    /// <summary>
    /// Exchanges the token; the returned client sends ff_portal + ff_csrf, X-CSRF-Token and Origin.
    /// Pass <paramref name="portal"/> to open the session on another host (e.g. one with a fixed IClock).
    /// </summary>
    protected async Task<HttpClient> OpenPortalSessionAsync(string token, HttpClient? portal = null)
    {
        portal ??= CreateBareClient();
        var resp = await portal.PostAsJsonAsync("/api/portal/session", new { token });
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var cookies = resp.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]).ToList();
        var session = cookies.First(c => c.StartsWith("ff_portal=", StringComparison.Ordinal));
        var csrf = cookies.First(c => c.StartsWith("ff_csrf=", StringComparison.Ordinal));

        portal.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"{session}; {csrf}");
        portal.DefaultRequestHeaders.TryAddWithoutValidation("X-CSRF-Token", Uri.UnescapeDataString(csrf["ff_csrf=".Length..]));
        portal.DefaultRequestHeaders.TryAddWithoutValidation("Origin", "http://localhost");
        return portal;
    }

    protected async Task<Guid> CreateProjectAsync(Guid clientId, string name, string internalNotes = "SECRET-INTERNAL")
    {
        var resp = await Client.PostAsJsonAsync("/api/admin/projects",
            new { clientId, name, designType = "LOGO", internalNotes });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    protected async Task<Guid> CreateDeliverableAsync(Guid projectId, string name, int includedRounds = 2)
    {
        var resp = await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/deliverables",
            new { name, quantity = 1, includedRounds, extraRoundPrice = 0 });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    protected async Task MoveDeliverableAsync(Guid deliverableId, params string[] statuses)
    {
        foreach (var to in statuses)
            (await Client.PostAsJsonAsync($"/api/admin/deliverables/{deliverableId}/status", new { to }))
                .EnsureSuccessStatusCode();
    }

    protected async Task ChangeProjectStatusAsync(Guid projectId, object body) =>
        (await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/status", body)).EnsureSuccessStatusCode();

    protected async Task<Guid> GetLatestBriefIdAsync(Guid projectId) =>
        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}"))
            .GetProperty("brief").GetProperty("id").GetGuid();

    protected const string QuoteTitle = "Báo giá thiết kế logo";

    /// <summary>Sets the receiving account directly in settings (send needs one — AC-B17).</summary>
    protected async Task ConfigureBankAsync(string accountNo = "0123456789")
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.Settings.FirstAsync();
        settings.BankBin = "970436";
        settings.BankAccountNo = accountNo;
        settings.BankAccountName = "NGUYEN VAN A";
        await db.SaveChangesAsync();
    }

    /// <summary>DRAFT with one manual line (2.000.000 ₫ by default), deposit 50% and an internal note.</summary>
    protected async Task<Guid> CreateDraftQuotationAsync(
        Guid projectId, long unitPrice = 2_000_000, string internalNotes = "SECRET-INTERNAL-QUOTE")
    {
        var create = await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/quotations",
            new { kind = "INITIAL", fromDeliverables = false });
        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var draft = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/quotations/{id}");
        var put = await Client.PutAsJsonAsync($"/api/admin/quotations/{id}", new
        {
            title = QuoteTitle,
            validityDays = 7,
            discountAmount = 0L,
            vatPercent = 0,
            depositPercent = 50,
            terms = "Thanh toán trong 7 ngày",
            notesToClient = "Cảm ơn anh/chị",
            internalNotes,
            items = new[]
            {
                new { id = (Guid?)null, description = "Thiết kế logo", details = "3 phương án", quantity = 1, unitPrice,
                      sourceType = "MANUAL", sourceId = (Guid?)null },
            },
            rowVersion = draft.GetProperty("rowVersion").GetUInt32(),
        });
        put.StatusCode.Should().Be(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
        return id;
    }

    protected async Task SendQuotationAsync(Guid id)
    {
        var rowVersion = (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/quotations/{id}"))
            .GetProperty("rowVersion").GetUInt32();
        var resp = await Client.PostAsJsonAsync($"/api/admin/quotations/{id}/send", new { rowVersion });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
    }

    /// <summary>Draft + send; needs <see cref="ConfigureBankAsync"/> first.</summary>
    protected async Task<Guid> CreateSentQuotationAsync(Guid projectId, long unitPrice = 2_000_000)
    {
        var id = await CreateDraftQuotationAsync(projectId, unitPrice);
        await SendQuotationAsync(id);
        return id;
    }

    /// <summary>multipart body of POST /api/portal/projects/{id}/requests (docs/05 §4); a new instance per request.</summary>
    protected static MultipartFormDataContent RequestForm(string content, Guid? deliverableId = null)
    {
        var form = new MultipartFormDataContent { { new StringContent(content), "content" } };
        if (deliverableId is { } d) form.Add(new StringContent(d.ToString()), "deliverableId");
        return form;
    }

    /// <summary>
    /// Designer-created round (docs/05 §3.8); the first item then gets <paramref name="internalNotes"/> via PUT.
    /// Returns the RoundDto JSON of the create call.
    /// </summary>
    protected async Task<JsonElement> CreateRoundAsync(Guid projectId, Guid? deliverableId, string internalNotes,
        params (string Description, string Type, string Billing)[] items)
    {
        var resp = await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/rounds", new
        {
            deliverableId,
            receivedAt = (string?)null,
            items = items.Select(i => new { description = i.Description, type = i.Type, billing = i.Billing }).ToArray(),
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        var round = await resp.Content.ReadFromJsonAsync<JsonElement>();

        var first = round.GetProperty("items")[0];
        var put = await Client.PutAsJsonAsync($"/api/admin/revision-items/{first.GetProperty("id").GetGuid()}", new
        {
            description = first.GetProperty("description").GetString(),
            type = first.GetProperty("type").GetString(),
            billing = first.GetProperty("billing").GetString(),
            internalNotes,
            rowVersion = first.GetProperty("rowVersion").GetUInt32(),
        });
        put.StatusCode.Should().Be(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
        return round;
    }
}
