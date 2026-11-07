using System.Net;
using System.Net.Http.Json;

namespace FreelanceFlow.IntegrationTests.Services;

public class ServicesTests(AppFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task GetServices_Unauthenticated_Returns401()
    {
        var resp = await Client.GetAsync("/api/admin/services");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task GetServices_Authenticated_ReturnsSeededServices()
    {
        var (adminCookie, csrfCookie, _) = await GetAuthCookiesAsync();
        var req = AuthRequest(HttpMethod.Get, "/api/admin/services", adminCookie, csrfCookie);
        var resp = await Client.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        var items = await resp.Content.ReadFromJsonAsync<List<ServiceItem>>();
        Assert.NotNull(items);
        Assert.True(items.Count >= 4, $"Expected ≥4 seeded services, got {items.Count}");
    }

    [Fact]
    public async Task GetServices_IncludeInactiveFalse_OnlyReturnsActive()
    {
        var (adminCookie, csrfCookie, _) = await GetAuthCookiesAsync();
        var req = AuthRequest(HttpMethod.Get, "/api/admin/services?includeInactive=false", adminCookie, csrfCookie);
        var resp = await Client.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        var items = await resp.Content.ReadFromJsonAsync<List<ServiceItem>>();
        Assert.NotNull(items);
        Assert.All(items, s => Assert.True(s.Active));
    }

    [Fact]
    public async Task CreateService_ValidData_Returns201()
    {
        var (adminCookie, csrfCookie, csrfToken) = await GetAuthCookiesAsync();
        var body = new { name = "Test Service " + Guid.NewGuid(), description = "desc", basePrice = 500000L, defaultIncludedRounds = 2, extraRoundPrice = 100000L };
        var req = PostRequest("/api/admin/services", body, adminCookie, csrfCookie, csrfToken);
        var resp = await Client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var created = await resp.Content.ReadFromJsonAsync<ServiceItem>();
        Assert.NotNull(created);
        Assert.Equal(500000L, created.BasePrice);
    }

    [Fact]
    public async Task CreateService_DuplicateName_Returns409()
    {
        var (adminCookie, csrfCookie, csrfToken) = await GetAuthCookiesAsync();
        var name = "Duplicate " + Guid.NewGuid();
        var body = new { name, description = "", basePrice = 100000L, defaultIncludedRounds = 1, extraRoundPrice = 50000L };

        var req1 = PostRequest("/api/admin/services", body, adminCookie, csrfCookie, csrfToken);
        var first = await Client.SendAsync(req1);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var req2 = PostRequest("/api/admin/services", body, adminCookie, csrfCookie, csrfToken);
        var second = await Client.SendAsync(req2);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task UpdateService_ToggleActive_Returns200()
    {
        var (adminCookie, csrfCookie, csrfToken) = await GetAuthCookiesAsync();
        var name = "ToDeactivate " + Guid.NewGuid();

        var createReq = PostRequest("/api/admin/services",
            new { name, description = "", basePrice = 100000L, defaultIncludedRounds = 1, extraRoundPrice = 50000L },
            adminCookie, csrfCookie, csrfToken);
        var created = await (await Client.SendAsync(createReq)).Content.ReadFromJsonAsync<ServiceItem>();
        Assert.NotNull(created);

        var updateReq = PutRequest($"/api/admin/services/{created.Id}",
            new { name, description = "", basePrice = 100000L, defaultIncludedRounds = 1, extraRoundPrice = 50000L, active = false },
            adminCookie, csrfCookie, csrfToken);
        var updateResp = await Client.SendAsync(updateReq);
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        var updated = await updateResp.Content.ReadFromJsonAsync<ServiceItem>();
        Assert.NotNull(updated);
        Assert.False(updated.Active);
    }

    // ── helpers ──

    private async Task<(string AdminCookie, string CsrfCookie, string CsrfToken)> GetAuthCookiesAsync()
    {
        var resp = await Client.PostAsJsonAsync("/api/auth/login",
            new { email = AppFactory.TestEmail, password = AppFactory.TestPassword });
        resp.EnsureSuccessStatusCode();

        var cookies = resp.Headers.GetValues("Set-Cookie").ToList();
        var admin = cookies.FirstOrDefault(c => c.StartsWith("ff_admin="))?.Split(';')[0] ?? "";
        var csrf = cookies.FirstOrDefault(c => c.StartsWith("ff_csrf="))?.Split(';')[0] ?? "";
        var token = csrf.Contains('=') ? csrf.Split('=', 2)[1] : "";
        return (admin, csrf, token);
    }

    private static HttpRequestMessage AuthRequest(HttpMethod method, string url, string adminCookie, string csrfCookie)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("Cookie", $"{adminCookie}; {csrfCookie}");
        return req;
    }

    private static HttpRequestMessage PostRequest(string url, object body, string adminCookie, string csrfCookie, string csrfToken)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        req.Headers.Add("Cookie", $"{adminCookie}; {csrfCookie}");
        req.Headers.Add("X-CSRF-Token", csrfToken);
        req.Headers.Add("Origin", "http://localhost");
        return req;
    }

    private static HttpRequestMessage PutRequest(string url, object body, string adminCookie, string csrfCookie, string csrfToken)
    {
        var req = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(body) };
        req.Headers.Add("Cookie", $"{adminCookie}; {csrfCookie}");
        req.Headers.Add("X-CSRF-Token", csrfToken);
        req.Headers.Add("Origin", "http://localhost");
        return req;
    }

    private record ServiceItem(Guid Id, string Name, string Description, long BasePrice, int DefaultIncludedRounds, long ExtraRoundPrice, bool Active, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
}
