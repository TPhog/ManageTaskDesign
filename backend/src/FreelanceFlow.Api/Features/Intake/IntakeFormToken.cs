using System.Globalization;
using System.Security.Cryptography;
using FreelanceFlow.Api.Domain.Common;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;

namespace FreelanceFlow.Api.Features.Intake;

/// <summary>A valid form token: its random id (single use) and how long ago the form was served.</summary>
public sealed record IntakeFormTicket(string Id, double AgeSeconds);

/// <summary>
/// BR-138: the public form carries a token signed with ASP.NET Data Protection = "{slug}|{issuedAtUnixMs}|{id}".
/// It proves when the form was served (minimum fill time), dies with the slug ("Tạo link mới") or after 24 h, and is
/// accepted for ONE stored submission: the id is remembered for the token lifetime (in memory — one API instance).
/// Age is measured with IClock, so tests control it. The token is never logged.
/// </summary>
public sealed class IntakeFormToken(IDataProtectionProvider provider, IClock clock, IMemoryCache cache)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);
    private const int MaxTokenLength = 1000;
    private readonly IDataProtector _protector = provider.CreateProtector("FreelanceFlow.PublicIntake.FormToken.v1");
    private readonly Lock _gate = new();

    public string Issue(string slug)
    {
        var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        return _protector.Protect(
            $"{slug}|{clock.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}|{id}");
    }

    /// <summary>null when the token is malformed, of another slug, expired or already used.</summary>
    public IntakeFormTicket? Read(string? token, string slug)
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
        if (parts.Length != 3 || parts[0] != slug || parts[2].Length == 0
            || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var issuedMs)) return null;
        var age = clock.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(issuedMs);
        if (age < TimeSpan.Zero || age > Lifetime || cache.TryGetValue(UsedKey(parts[2]), out _)) return null;
        return new IntakeFormTicket(parts[2], age.TotalSeconds);
    }

    /// <summary>Marks the token used; false when another request used it first (atomic).</summary>
    public bool TryConsume(string id)
    {
        lock (_gate)
        {
            if (cache.TryGetValue(UsedKey(id), out _)) return false;
            cache.Set(UsedKey(id), true, Lifetime);
            return true;
        }
    }

    private static string UsedKey(string id) => "intake-form-token-used:" + id;
}
