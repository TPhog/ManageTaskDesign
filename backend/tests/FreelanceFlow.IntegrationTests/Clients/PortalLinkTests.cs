using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Clients;

public class PortalLinkTests(AppFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Create_link_returns_url_once_enables_portal_and_stores_only_hash()
    {
        await LoginAsync();
        var clientId = await CreateClientAsync();

        var resp = await Client.PostAsync($"/api/admin/clients/{clientId}/portal-link", null);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var url = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!;
        url.Should().MatchRegex(@"^http://localhost/p/[A-Za-z0-9_-]{43}$");
        var token = url[(url.LastIndexOf('/') + 1)..];

        var detail = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/clients/{clientId}");
        detail.GetProperty("portalEnabled").GetBoolean().Should().BeTrue();
        detail.GetProperty("hasPortalLink").GetBoolean().Should().BeTrue();
        detail.GetProperty("portalTokenVersion").GetInt32().Should().Be(1);
        detail.ToString().Should().NotContain(token);

        (await StoredHashAsync(clientId)).Should().Equal(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        (await ActionsAsync(clientId)).Should().Contain("client.portal_link_created");
    }

    [Fact]
    public async Task Regenerate_link_replaces_hash_bumps_version_and_logs_regenerated()
    {
        await LoginAsync();
        var clientId = await CreateClientAsync();
        var first = await IssueAsync(clientId);

        var second = await IssueAsync(clientId);

        second.Should().NotBe(first);
        var detail = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/clients/{clientId}");
        detail.GetProperty("portalTokenVersion").GetInt32().Should().Be(2);
        (await StoredHashAsync(clientId)).Should().Equal(SHA256.HashData(Encoding.UTF8.GetBytes(second)));
        (await ActionsAsync(clientId)).Should().Contain("client.portal_link_regenerated");
    }

    [Fact]
    public async Task Revoke_link_clears_it_disables_portal_and_logs_revoked()
    {
        await LoginAsync();
        var clientId = await CreateClientAsync();
        await IssueAsync(clientId);

        var resp = await Client.PostAsync($"/api/admin/clients/{clientId}/portal/revoke", null);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/clients/{clientId}");
        detail.GetProperty("portalEnabled").GetBoolean().Should().BeFalse();
        detail.GetProperty("hasPortalLink").GetBoolean().Should().BeFalse();
        detail.GetProperty("portalTokenVersion").GetInt32().Should().Be(2);
        (await StoredHashAsync(clientId)).Should().BeNull();
        (await ActionsAsync(clientId)).Should().Contain("client.portal_link_revoked");
    }

    [Fact]
    public async Task Revoke_without_link_returns_409()
    {
        await LoginAsync();
        var clientId = await CreateClientAsync();

        var resp = await Client.PostAsync($"/api/admin/clients/{clientId}/portal/revoke", null);

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("BUSINESS_RULE");
    }

    [Fact]
    public async Task Create_link_for_archived_client_returns_409()
    {
        await LoginAsync();
        var clientId = await CreateClientAsync();
        (await Client.PostAsync($"/api/admin/clients/{clientId}/archive", null)).EnsureSuccessStatusCode();

        var resp = await Client.PostAsync($"/api/admin/clients/{clientId}/portal-link", null);

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("BUSINESS_RULE");
    }

    private async Task<Guid> CreateClientAsync()
    {
        var resp = await Client.PostAsJsonAsync("/api/admin/clients", new { name = "Portal Link Client " + Guid.NewGuid() });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<string> IssueAsync(Guid clientId)
    {
        var resp = await Client.PostAsync($"/api/admin/clients/{clientId}/portal-link", null);
        resp.EnsureSuccessStatusCode();
        var url = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!;
        return url[(url.LastIndexOf('/') + 1)..];
    }

    private async Task<byte[]?> StoredHashAsync(Guid clientId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Clients.AsNoTracking()
            .Where(c => c.Id == clientId)
            .Select(c => c.PortalTokenHash)
            .SingleAsync();
    }

    private async Task<List<string>> ActionsAsync(Guid clientId)
    {
        var page = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/clients/{clientId}/activity");
        return page.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("action").GetString()!)
            .ToList();
    }
}
