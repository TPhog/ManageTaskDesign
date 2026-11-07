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
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
    }

    protected async Task LoginAsync()
    {
        // POST login — CSRF is not checked for /api/auth/login (see CsrfMiddleware)
        var resp = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = AppFactory.TestEmail,
            password = AppFactory.TestPassword,
        });
        resp.EnsureSuccessStatusCode();

        // Extract ff_csrf cookie and set it as a header for subsequent mutating requests
        if (resp.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            var csrfCookie = setCookies
                .Select(c => c.Split(';')[0])
                .FirstOrDefault(c => c.StartsWith("ff_csrf=", StringComparison.OrdinalIgnoreCase));
            if (csrfCookie != null)
            {
                var csrfValue = csrfCookie["ff_csrf=".Length..];
                Client.DefaultRequestHeaders.Remove("X-CSRF-Token");
                Client.DefaultRequestHeaders.Add("X-CSRF-Token", Uri.UnescapeDataString(csrfValue));
            }
        }
    }
}
