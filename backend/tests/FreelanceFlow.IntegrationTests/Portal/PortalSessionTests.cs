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
}
