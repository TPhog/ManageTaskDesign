using System.Security.Cryptography;
using System.Text;
using FreelanceFlow.Api.Common;
using Microsoft.Extensions.Options;

namespace FreelanceFlow.Api.Infrastructure.Security;

public class CsrfMiddleware(RequestDelegate next, IOptions<AppSettings> settings)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        if (HttpMethods.IsGet(ctx.Request.Method) ||
            HttpMethods.IsHead(ctx.Request.Method) ||
            HttpMethods.IsOptions(ctx.Request.Method))
        {
            await next(ctx);
            return;
        }

        // Login and portal session set the CSRF cookie — no token yet.
        // /api/public/* (plan 13) is anonymous: there is no session to forge, and /f/* pages send "Origin: null" (no-referrer).
        if (ctx.Request.Path.StartsWithSegments("/api/auth/login") ||
            ctx.Request.Path.StartsWithSegments("/api/portal/session") ||
            ctx.Request.Path.StartsWithSegments("/api/public"))
        {
            await next(ctx);
            return;
        }

        // Origin/Referer check
        var publicBase = settings.Value.PublicBaseUrl?.TrimEnd('/');
        if (!string.IsNullOrEmpty(publicBase))
        {
            var origin = ctx.Request.Headers.Origin.FirstOrDefault();
            var referer = ctx.Request.Headers.Referer.FirstOrDefault();
            var fromOrigin = origin != null && string.Equals(origin.TrimEnd('/'), publicBase, StringComparison.OrdinalIgnoreCase);
            var fromReferer = referer != null && referer.StartsWith(publicBase, StringComparison.OrdinalIgnoreCase);
            if (!fromOrigin && !fromReferer)
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }

        // Double-submit token check
        var headerToken = ctx.Request.Headers["X-CSRF-Token"].FirstOrDefault();
        var cookieToken = ctx.Request.Cookies["ff_csrf"];
        if (string.IsNullOrEmpty(headerToken) || string.IsNullOrEmpty(cookieToken))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var headerBytes = Encoding.UTF8.GetBytes(headerToken);
        var cookieBytes = Encoding.UTF8.GetBytes(cookieToken);
        if (headerBytes.Length != cookieBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(headerBytes, cookieBytes))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(ctx);
    }
}
