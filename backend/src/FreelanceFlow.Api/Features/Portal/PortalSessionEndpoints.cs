using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Features.Auth;
using FreelanceFlow.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Portal;

public static class PortalSessionEndpoints
{
    public static void MapPortalSessionEndpoints(this IEndpointRouteBuilder app)
    {
        // Public; CsrfMiddleware already skips this path (it is what issues ff_csrf).
        app.MapPost("/api/portal/session", CreateSession)
           .RequireRateLimiting("portal-session")
           .AllowAnonymous();

        // (Delegate) cast: without it the Task<IResult>(HttpContext) method group binds to the
        // RequestDelegate overload and the result is never executed (same as /api/auth/logout).
        app.MapPost("/api/portal/logout", (Delegate)Logout)
           .AllowAnonymous();

        app.MapGet("/api/portal/me", Me)
           .RequireAuthorization(PortalPrincipal.Policy);
    }

    // 07-security §4.2. Request body (the token) must never be logged.
    private static async Task<IResult> CreateSession(
        [FromBody] PortalSessionRequest req,
        HttpContext ctx,
        AppDbContext db)
    {
        if (!PortalTokenService.IsWellFormed(req.Token)) return InvalidLink();

        var hash = PortalTokenService.Hash(req.Token!);
        var client = await db.Clients.AsNoTracking()
            .Where(c => c.PortalTokenHash == hash && c.PortalEnabled && c.ArchivedAt == null)
            .Select(c => new { c.Id, c.PortalTokenHash, c.PortalTokenVersion })
            .FirstOrDefaultAsync();

        if (client is null || !PortalTokenService.Matches(client.PortalTokenHash, hash)) return InvalidLink();

        var claims = new[]
        {
            new Claim(PortalPrincipal.ClientIdClaim, client.Id.ToString()),
            new Claim(PortalPrincipal.TokenVersionClaim, client.PortalTokenVersion.ToString(CultureInfo.InvariantCulture)),
        };
        await ctx.SignInAsync(PortalPrincipal.Scheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, PortalPrincipal.Scheme)));

        // Double-submit CSRF cookie for later portal POSTs (07-security §3) — same shape as designer login.
        ctx.Response.Cookies.Append("ff_csrf", Base64Url.Encode(RandomNumberGenerator.GetBytes(16)), new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
        });

        return Results.NoContent();
    }

    // Keeps ff_csrf: the designer may be signed in on the same browser.
    private static async Task<IResult> Logout(HttpContext ctx)
    {
        await ctx.SignOutAsync(PortalPrincipal.Scheme);
        return Results.NoContent();
    }

    private static async Task<IResult> Me([FromServices] PortalPrincipal principal, AppDbContext db)
    {
        var clientName = await db.Clients.AsNoTracking()
            .Where(c => c.Id == principal.ClientId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync();
        if (clientName is null) return Results.NotFound();

        var settings = await db.Settings.AsNoTracking().FirstOrDefaultAsync();
        return Results.Ok(new PortalMeDto(clientName, new PortalDesignerDto(
            settings?.DisplayName ?? "",
            null, // logo file serving arrives with M3
            settings?.ContactPhone,
            settings?.ContactEmail)));
    }

    private static IResult InvalidLink() => Results.Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Liên kết không hợp lệ hoặc đã hết hạn",
        extensions: new Dictionary<string, object?> { ["code"] = "UNAUTHENTICATED" });
}
