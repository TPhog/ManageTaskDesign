using System.Globalization;
using System.Security.Claims;
using FreelanceFlow.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Infrastructure.Security;

public static class PortalSessionValidator
{
    // 07-security §4.2 step 4. Deliberately uncached (docs suggest 60s): BR-12 requires regenerate/revoke
    // to end old sessions immediately, and one indexed lookup per portal request is cheap.
    public static async Task ValidatePrincipal(CookieValidatePrincipalContext ctx)
    {
        var principal = ctx.Principal;
        var clientIdValue = principal?.FindFirstValue(PortalPrincipal.ClientIdClaim);
        var versionValue = principal?.FindFirstValue(PortalPrincipal.TokenVersionClaim);

        if (!Guid.TryParse(clientIdValue, out var clientId) ||
            !int.TryParse(versionValue, NumberStyles.None, CultureInfo.InvariantCulture, out var version))
        {
            await RejectAsync(ctx);
            return;
        }

        var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var active = await db.Clients.AsNoTracking().AnyAsync(c =>
            c.Id == clientId &&
            c.PortalEnabled &&
            c.ArchivedAt == null &&
            c.PortalTokenVersion == version);

        if (!active) await RejectAsync(ctx);
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext ctx)
    {
        ctx.RejectPrincipal();
        await ctx.HttpContext.SignOutAsync(PortalPrincipal.Scheme);
    }
}
