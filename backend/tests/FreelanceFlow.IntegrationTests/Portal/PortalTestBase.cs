using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

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

    /// <summary>Exchanges the token; the returned client sends ff_portal + ff_csrf, X-CSRF-Token and Origin.</summary>
    protected async Task<HttpClient> OpenPortalSessionAsync(string token)
    {
        var portal = CreateBareClient();
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
}
