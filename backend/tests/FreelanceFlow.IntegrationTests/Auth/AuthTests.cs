using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace FreelanceFlow.IntegrationTests.Auth;

// Requires Docker — these tests use Testcontainers.PostgreSql
public class AuthTests(AppFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Login_correct_credentials_returns_204_with_cookies()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = AppFactory.TestEmail,
            password = AppFactory.TestPassword,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.Should().ContainKey("Set-Cookie");

        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        cookies.Should().Contain(c => c.StartsWith("ff_admin="));
        cookies.Should().Contain(c => c.StartsWith("ff_csrf="));
    }

    [Fact]
    public async Task Login_wrong_password_returns_401()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = AppFactory.TestEmail,
            password = "wrongpassword",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_unauthenticated_returns_401()
    {
        var response = await Client.GetAsync("/api/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_after_login_returns_email()
    {
        // Login first to get cookies
        var loginResp = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = AppFactory.TestEmail,
            password = AppFactory.TestPassword,
        });
        loginResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Extract cookies from login response and add to next request
        var cookies = loginResp.Headers.GetValues("Set-Cookie").ToList();
        var adminCookie = cookies.FirstOrDefault(c => c.StartsWith("ff_admin="))
            ?.Split(';')[0];
        var csrfCookie = cookies.FirstOrDefault(c => c.StartsWith("ff_csrf="))
            ?.Split(';')[0];

        adminCookie.Should().NotBeNullOrEmpty();
        csrfCookie.Should().NotBeNullOrEmpty();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add("Cookie", $"{adminCookie}; {csrfCookie}");
        var meResp = await Client.SendAsync(request);

        meResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await meResp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("email").GetString().Should().Be(AppFactory.TestEmail);
    }

    [Fact]
    public async Task Logout_returns_204()
    {
        // Login first
        var loginResp = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = AppFactory.TestEmail,
            password = AppFactory.TestPassword,
        });
        loginResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var cookies = loginResp.Headers.GetValues("Set-Cookie").ToList();
        var adminCookie = cookies.FirstOrDefault(c => c.StartsWith("ff_admin="))?.Split(';')[0];
        var csrfCookie = cookies.FirstOrDefault(c => c.StartsWith("ff_csrf="))?.Split(';')[0];
        var csrfToken = csrfCookie?.Split('=')[1];

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Add("Cookie", $"{adminCookie}; {csrfCookie}");
        // Logout doesn't require CSRF (it uses POST but no body — can still pass token)
        if (!string.IsNullOrEmpty(csrfToken))
            request.Headers.Add("X-CSRF-Token", csrfToken);
        request.Headers.Add("Origin", "http://localhost");

        var logoutResp = await Client.SendAsync(request);
        logoutResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ChangePassword_without_csrf_returns_403()
    {
        // Login first
        var loginResp = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = AppFactory.TestEmail,
            password = AppFactory.TestPassword,
        });
        loginResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var cookies = loginResp.Headers.GetValues("Set-Cookie").ToList();
        var adminCookie = cookies.FirstOrDefault(c => c.StartsWith("ff_admin="))?.Split(';')[0];

        // POST without CSRF token — should be 403
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/account/password");
        request.Headers.Add("Cookie", adminCookie);
        request.Headers.Add("Origin", "http://localhost");
        request.Content = JsonContent.Create(new { currentPassword = AppFactory.TestPassword, newPassword = "newpassword456" });

        var resp = await Client.SendAsync(request);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
