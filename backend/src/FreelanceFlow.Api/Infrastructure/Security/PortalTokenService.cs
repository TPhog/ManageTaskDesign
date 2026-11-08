using System.Security.Cryptography;
using System.Text;

namespace FreelanceFlow.Api.Infrastructure.Security;

/// <summary>Magic-link token checks (07-security §4). Tokens are never logged.</summary>
public static class PortalTokenService
{
    public const int TokenLength = 43; // base64url of 32 random bytes

    public static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    public static bool IsWellFormed(string? token) =>
        token is { Length: TokenLength } &&
        token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    public static bool Matches(byte[]? storedHash, byte[] candidateHash) =>
        storedHash is { Length: 32 } &&
        candidateHash.Length == 32 &&
        CryptographicOperations.FixedTimeEquals(storedHash, candidateHash);
}
