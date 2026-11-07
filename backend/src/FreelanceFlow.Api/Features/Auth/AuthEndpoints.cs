using System.Security.Claims;
using System.Security.Cryptography;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Auth;
using FreelanceFlow.Api.Domain.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/login", Login)
           .RequireRateLimiting("login")
           .AllowAnonymous();

        app.MapPost("/api/auth/logout", (Delegate)Logout)
           .AllowAnonymous();

        app.MapGet("/api/auth/me", Me)
           .RequireAuthorization("DesignerOnly");

        app.MapPost("/api/admin/account/password", ChangePassword)
           .RequireAuthorization("DesignerOnly");
    }

    private static async Task<IResult> Login(
        [FromBody] LoginRequest req,
        HttpContext ctx,
        AppDbContext db,
        IPasswordHasher<DesignerAccount> hasher,
        IClock clock)
    {
        var account = await db.DesignerAccounts
            .FirstOrDefaultAsync(a => a.Username == req.Username.ToLowerInvariant());

        if (account is null)
            return Results.Unauthorized();

        if (account.IsLockedOut(clock.UtcNow))
        {
            var retryAfter = (int)Math.Ceiling((account.LockoutUntil!.Value - clock.UtcNow).TotalSeconds);
            ctx.Response.Headers["Retry-After"] = retryAfter.ToString();
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        if (!account.VerifyPassword(req.Password, hasher))
        {
            account.RecordFailedLogin(clock.UtcNow);
            await db.SaveChangesAsync();
            return Results.Unauthorized();
        }

        account.ClearFailedLogins();
        await db.SaveChangesAsync();

        var claims = new[]
        {
            new Claim("DesignerAccountId", account.Id.ToString()),
            new Claim(ClaimTypes.Name, account.Username),
            new Claim("security_stamp", account.SecurityStamp),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Designer"));
        await ctx.SignInAsync("Designer", principal);

        // Set CSRF cookie (not HttpOnly so JS can read it)
        var csrfToken = Base64Url.Encode(RandomNumberGenerator.GetBytes(16));
        ctx.Response.Cookies.Append("ff_csrf", csrfToken, new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
        });

        return Results.NoContent();
    }

    private static async Task<IResult> Logout(HttpContext ctx)
    {
        await ctx.SignOutAsync("Designer");
        ctx.Response.Cookies.Delete("ff_csrf");
        return Results.NoContent();
    }

    private static IResult Me(ClaimsPrincipal user)
    {
        var username = user.FindFirstValue(ClaimTypes.Name) ?? "";
        return Results.Ok(new { username });
    }

    private static async Task<IResult> ChangePassword(
        [FromBody] ChangePasswordRequest req,
        ClaimsPrincipal user,
        HttpContext ctx,
        AppDbContext db,
        IPasswordHasher<DesignerAccount> hasher,
        IClock clock)
    {
        if (req.NewPassword.Length < 12)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["newPassword"] = ["Mật khẩu mới phải ít nhất 12 ký tự."]
            });

        var accountId = Guid.Parse(user.FindFirstValue("DesignerAccountId")!);
        var account = await db.DesignerAccounts.FindAsync(accountId);
        if (account is null) return Results.Unauthorized();

        if (!account.VerifyPassword(req.CurrentPassword, hasher))
            return Results.Unauthorized();

        account.ChangePassword(hasher.HashPassword(account, req.NewPassword));
        await db.SaveChangesAsync();

        // Re-sign to update security_stamp in cookie
        var claims = new[]
        {
            new Claim("DesignerAccountId", account.Id.ToString()),
            new Claim(ClaimTypes.Name, account.Username),
            new Claim("security_stamp", account.SecurityStamp),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Designer"));
        await ctx.SignInAsync("Designer", principal);

        return Results.NoContent();
    }
}

internal sealed record LoginRequest(string Username, string Password);
internal sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

// Simple base64url encoder (no dependency needed)
internal static class Base64Url
{
    public static string Encode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
