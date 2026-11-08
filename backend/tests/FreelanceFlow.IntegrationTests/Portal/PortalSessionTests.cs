using System.Net;
using System.Text.Json;

namespace FreelanceFlow.IntegrationTests.Portal;

public class PortalSessionTests(AppFactory factory) : PortalTestBase(factory)
{
    [Fact]
    public async Task Valid_token_opens_session_and_me_returns_client_name()
    {
        await LoginAsync();
        var name = "Khách Portal " + Guid.NewGuid();
        var (_, token) = await CreateClientWithPortalLinkAsync(name);
        var portal = await OpenPortalSessionAsync(token);

        var me = await portal.GetFromJsonAsync<JsonElement>("/api/portal/me");

        me.GetProperty("clientName").GetString().Should().Be(name);
        me.GetProperty("designer").GetProperty("displayName").GetString().Should().NotBeNull();
    }

    [Fact]
    public async Task Studio_contact_is_public_and_carries_only_the_contact_fields()   // QA C-04/C-14/C-15
    {
        var bare = CreateBareClient();

        var resp = await bare.GetAsync("/api/portal/studio");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("displayName", "contactPhone", "contactEmail");
    }

    [Fact]
    public async Task Studio_contact_is_rate_limited_per_ip()
    {
        using var limited = Factory.WithWebHostBuilder(b => b.UseSetting("RateLimit:PortalStudioPerMinute", "3"));
        var bare = limited.CreateClient();

        for (var i = 0; i < 3; i++)
            (await bare.GetAsync("/api/portal/studio")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await bare.GetAsync("/api/portal/studio")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Unknown_or_malformed_token_returns_generic_401_without_cookie()
    {
        foreach (var token in new[] { new string('A', 43), "not-a-token", "" })
        {
            var resp = await PostSessionAsync(token);

            resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"token '{token}'");
            var problem = await resp.Content.ReadFromJsonAsync<JsonElement>();
            problem.GetProperty("title").GetString().Should().Be("Liên kết không hợp lệ hoặc đã hết hạn");
            problem.GetProperty("code").GetString().Should().Be("UNAUTHENTICATED");
            resp.Headers.TryGetValues("Set-Cookie", out var setCookies);
            (setCookies ?? Enumerable.Empty<string>())
                .Should().NotContain(c => c.StartsWith("ff_portal=", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Regenerated_link_invalidates_old_token_and_old_session()
    {
        await LoginAsync();
        var (clientId, oldToken) = await CreateClientWithPortalLinkAsync();
        var oldSession = await OpenPortalSessionAsync(oldToken);

        var newToken = await IssueLinkAsync(clientId); // BR-12

        (await PostSessionAsync(oldToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await oldSession.GetAsync("/api/portal/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var newSession = await OpenPortalSessionAsync(newToken);
        (await newSession.GetAsync("/api/portal/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Revoked_link_rejects_token_and_open_session()
    {
        await LoginAsync();
        var (clientId, token) = await CreateClientWithPortalLinkAsync();
        var session = await OpenPortalSessionAsync(token);

        (await Client.PostAsync($"/api/admin/clients/{clientId}/portal/revoke", null)).EnsureSuccessStatusCode();

        (await PostSessionAsync(token)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await session.GetAsync("/api/portal/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Disabled_portal_or_archived_client_ends_session()
    {
        await LoginAsync();
        var (disabledId, disabledToken) = await CreateClientWithPortalLinkAsync();
        var disabledSession = await OpenPortalSessionAsync(disabledToken);
        var (archivedId, archivedToken) = await CreateClientWithPortalLinkAsync();
        var archivedSession = await OpenPortalSessionAsync(archivedToken);

        (await Client.PostAsync($"/api/admin/clients/{disabledId}/portal/disable", null)).EnsureSuccessStatusCode();
        (await Client.PostAsync($"/api/admin/clients/{archivedId}/archive", null)).EnsureSuccessStatusCode(); // BR-11

        (await disabledSession.GetAsync("/api/portal/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await PostSessionAsync(disabledToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await archivedSession.GetAsync("/api/portal/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await PostSessionAsync(archivedToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Designer_cookie_does_not_open_portal_and_portal_cookie_does_not_open_admin()
    {
        await LoginAsync();
        (await Client.GetAsync("/api/portal/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var (_, token) = await CreateClientWithPortalLinkAsync();
        var portal = await OpenPortalSessionAsync(token);

        (await portal.GetAsync("/api/admin/clients")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await portal.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_requires_csrf_and_clears_portal_cookie()
    {
        await LoginAsync();
        var (_, token) = await CreateClientWithPortalLinkAsync();
        var portal = await OpenPortalSessionAsync(token);

        using var withoutCsrf = new HttpRequestMessage(HttpMethod.Post, "/api/portal/logout");
        withoutCsrf.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", portal.DefaultRequestHeaders.GetValues("Cookie")));
        (await CreateBareClient().SendAsync(withoutCsrf)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var resp = await portal.PostAsync("/api/portal/logout", null);

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
        resp.Headers.GetValues("Set-Cookie").Should().Contain(c => c.StartsWith("ff_portal=;", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Logout_with_correct_origin_but_missing_or_wrong_csrf_token_returns_403()
    {
        await LoginAsync();
        var (_, token) = await CreateClientWithPortalLinkAsync();
        var portal = await OpenPortalSessionAsync(token);
        var cookie = string.Join("; ", portal.DefaultRequestHeaders.GetValues("Cookie"));

        foreach (var csrfHeader in new string?[] { null, "wrong-token-value" })
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/portal/logout");
            req.Headers.TryAddWithoutValidation("Cookie", cookie);
            req.Headers.TryAddWithoutValidation("Origin", "http://localhost");
            if (csrfHeader is not null) req.Headers.TryAddWithoutValidation("X-CSRF-Token", csrfHeader);

            (await CreateBareClient().SendAsync(req)).StatusCode.Should().Be(HttpStatusCode.Forbidden, $"header '{csrfHeader}'");
        }
    }

    [Fact]
    public async Task Session_cookie_is_persistent_and_forwarded_https_makes_cookies_secure()
    {
        await LoginAsync();
        var (_, token) = await CreateClientWithPortalLinkAsync();

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/portal/session")
        {
            Content = JsonContent.Create(new { token }),
        };
        req.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        var resp = await CreateBareClient().SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var portalCookie = resp.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("ff_portal=", StringComparison.Ordinal));
        portalCookie.Should().ContainEquivalentOf("expires=");
        portalCookie.Should().ContainEquivalentOf("; secure");
    }

    [Fact]
    public async Task Me_reissues_csrf_cookie_only_when_missing()
    {
        await LoginAsync();
        var (_, token) = await CreateClientWithPortalLinkAsync();
        var portal = await OpenPortalSessionAsync(token);
        var cookies = portal.DefaultRequestHeaders.GetValues("Cookie").Single().Split("; ");
        var sessionOnly = cookies.First(c => c.StartsWith("ff_portal=", StringComparison.Ordinal));

        using var without = new HttpRequestMessage(HttpMethod.Get, "/api/portal/me");
        without.Headers.TryAddWithoutValidation("Cookie", sessionOnly);
        var resp = await CreateBareClient().SendAsync(without);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Headers.GetValues("Set-Cookie").Should().Contain(c => c.StartsWith("ff_csrf=", StringComparison.Ordinal));

        var withCsrf = await portal.GetAsync("/api/portal/me");
        withCsrf.Headers.TryGetValues("Set-Cookie", out var set);
        (set ?? Enumerable.Empty<string>()).Should().NotContain(c => c.StartsWith("ff_csrf=", StringComparison.Ordinal));
    }
}
