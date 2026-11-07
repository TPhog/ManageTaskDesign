using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FreelanceFlow.IntegrationTests;

public abstract class IntegrationTestBase : IClassFixture<AppFactory>
{
    protected readonly AppFactory Factory;
    protected readonly HttpClient Client;

    protected IntegrationTestBase(AppFactory factory)
    {
        Factory = factory;
        // Cookies are attached by hand: ff_csrf is issued with Secure=true, which a
        // CookieContainer never sends to the test server's http:// base address.
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
    }

    /// <summary>Logs in; afterwards every request on <see cref="Client"/> is authenticated and CSRF-valid.</summary>
    protected async Task LoginAsync()
    {
        var resp = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            username = AppFactory.TestUsername,
            password = AppFactory.TestPassword,
        });
        resp.EnsureSuccessStatusCode();

        var cookies = resp.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]).ToList();
        var admin = cookies.First(c => c.StartsWith("ff_admin=", StringComparison.Ordinal));
        var csrf = cookies.First(c => c.StartsWith("ff_csrf=", StringComparison.Ordinal));

        SetDefaultHeader("Cookie", $"{admin}; {csrf}");
        SetDefaultHeader("X-CSRF-Token", Uri.UnescapeDataString(csrf["ff_csrf=".Length..]));
        SetDefaultHeader("Origin", "http://localhost");
    }

    private void SetDefaultHeader(string name, string value)
    {
        Client.DefaultRequestHeaders.Remove(name);
        Client.DefaultRequestHeaders.TryAddWithoutValidation(name, value);
    }
}
