using System.Security.Cryptography;
using FreelanceFlow.Api.Features.Auth;

namespace FreelanceFlow.Api.Infrastructure.Security;

/// <summary>Double-submit CSRF cookie (07-security §3): readable by JS, echoed in X-CSRF-Token.</summary>
public static class CsrfCookie
{
    public const string Name = "ff_csrf";

    public static void Issue(HttpContext ctx) =>
        ctx.Response.Cookies.Append(Name, Base64Url.Encode(RandomNumberGenerator.GetBytes(16)), new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
        });
}
