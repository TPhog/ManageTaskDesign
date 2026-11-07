using System.Security.Claims;
using FreelanceFlow.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace FreelanceFlow.Api.Infrastructure.Security;

public static class DesignerStampValidator
{
    // Cache key: "stamp:{accountId}" → stamp string; TTL 5 minutes
    public static async Task ValidatePrincipal(CookieValidatePrincipalContext ctx)
    {
        var principal = ctx.Principal;
        if (principal is null) { ctx.RejectPrincipal(); return; }

        var accountIdStr = principal.FindFirstValue("DesignerAccountId");
        var stampInCookie = principal.FindFirstValue("security_stamp");
        if (accountIdStr is null || stampInCookie is null) { ctx.RejectPrincipal(); return; }

        if (!Guid.TryParse(accountIdStr, out var accountId)) { ctx.RejectPrincipal(); return; }

        var cache = ctx.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
        var cacheKey = $"stamp:{accountId}";

        if (!cache.TryGetValue(cacheKey, out string? cachedStamp))
        {
            var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var account = await db.DesignerAccounts
                .AsNoTracking()
                .Where(a => a.Id == accountId)
                .Select(a => new { a.SecurityStamp })
                .FirstOrDefaultAsync();

            if (account is null) { ctx.RejectPrincipal(); return; }

            cachedStamp = account.SecurityStamp;
            cache.Set(cacheKey, cachedStamp, TimeSpan.FromMinutes(5));
        }

        if (!string.Equals(cachedStamp, stampInCookie, StringComparison.Ordinal))
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync("Designer");
        }
    }
}
