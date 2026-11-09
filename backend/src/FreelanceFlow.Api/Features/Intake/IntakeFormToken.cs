using System.Globalization;
using System.Security.Cryptography;
using FreelanceFlow.Api.Domain.Common;
using Microsoft.AspNetCore.DataProtection;

namespace FreelanceFlow.Api.Features.Intake;

/// <summary>
/// BR-138: the public form carries a token signed with ASP.NET Data Protection = "{slug}|{issuedAtUnixMs}".
/// It proves when the form was served (minimum fill time) and dies with the slug ("Tạo link mới") or after 24 h.
/// Age is measured with IClock, so tests control it. The token is never logged.
/// </summary>
public sealed class IntakeFormToken(IDataProtectionProvider provider, IClock clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);
    private const int MaxTokenLength = 1000;
    private readonly IDataProtector _protector = provider.CreateProtector("FreelanceFlow.PublicIntake.FormToken.v1");

    public string Issue(string slug) =>
        _protector.Protect($"{slug}|{clock.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}");

    /// <summary>Seconds since the form was served, or null when the token is malformed, of another slug or expired.</summary>
    public double? SecondsSinceIssued(string? token, string slug)
    {
        if (string.IsNullOrEmpty(token) || token.Length > MaxTokenLength) return null;
        string payload;
        try
        {
            payload = _protector.Unprotect(token);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }
        var parts = payload.Split('|');
        if (parts.Length != 2 || parts[0] != slug
            || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var issuedMs)) return null;
        var age = clock.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(issuedMs);
        return age < TimeSpan.Zero || age > Lifetime ? null : age.TotalSeconds;
    }
}
