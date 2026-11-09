using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Security;

/// <summary>
/// R2 M3: /api/public/* is the third endpoint group — anonymous, no CSRF check (CsrfMiddleware GUARD). Like the portal
/// RouteTable, a new public route must be added here on purpose, and it must be anonymous and rate-limited
/// (docs/07-security.md §4.5).
/// </summary>
public class PublicRouteGuardTests(AppFactory factory) : IntegrationTestBase(factory)
{
    private static readonly string[] PublicRoutes =
    [
        "GET /api/public/intake/{slug}",
        "POST /api/public/intake/{slug}",
    ];

    private List<RouteEndpoint> PublicEndpoints() =>
        Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/public", StringComparison.Ordinal) == true)
            .ToList();

    [Fact]
    public void Every_public_route_is_declared_anonymous_and_rate_limited()
    {
        var endpoints = PublicEndpoints();

        endpoints.SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["ANY"])
                .Select(m => $"{m} {e.RoutePattern.RawText!.TrimEnd('/')}"))
            .Should().BeEquivalentTo(PublicRoutes,
                "/api/public skips CSRF: every new route there must be reviewed and listed (docs/07-security.md §4.5)");
        endpoints.Should().OnlyContain(e => e.Metadata.GetMetadata<IAllowAnonymous>() != null, "public routes never need a session");
        endpoints.Should().OnlyContain(e => e.Metadata.GetMetadata<EnableRateLimitingAttribute>() != null,
            "every anonymous endpoint has a named rate-limit policy");
    }

    [Fact]
    public async Task The_csrf_exemption_is_segment_exact()
    {
        await LoginAsync();
        Client.DefaultRequestHeaders.Remove("X-CSRF-Token");   // signed-in designer, no CSRF token

        // a look-alike prefix is not exempt: the CSRF middleware refuses it before routing finds nothing
        (await Client.PostAsync("/api/publicity", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Client.PostAsync("/api/admin/clients", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        // the real segment is exempt: an unknown slug reaches the endpoint (no 403)
        (await Client.PostAsync($"/api/public/intake/{Guid.NewGuid():N}", null)).StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }
}
