using System.Security.Claims;

namespace FreelanceFlow.Api.Infrastructure.Security;

/// <summary>
/// The signed-in portal client. Portal handlers take it from DI ([FromServices]) — a clientId is never
/// read from the route or body (07-security §4.4).
/// </summary>
public sealed record PortalPrincipal(Guid ClientId)
{
    public const string Scheme = "Portal";
    public const string Policy = "PortalClient";
    public const string CookieName = "ff_portal";
    public const string ClientIdClaim = "portal_client_id";
    public const string TokenVersionClaim = "portal_token_version";

    public static PortalPrincipal FromUser(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClientIdClaim), out var clientId)
            ? new PortalPrincipal(clientId)
            : throw new InvalidOperationException("PortalPrincipal requested outside a portal session");
}
