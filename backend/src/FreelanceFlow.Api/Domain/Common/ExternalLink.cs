using System.Text.Json;

namespace FreelanceFlow.Api.Domain.Common;

/// <summary>P15 D6: an external link (Figma, Drive…) on a task or a deliverable. Only deliverable links may reach the portal.</summary>
public sealed record ExternalLink(string Title, string Url, bool VisibleToClient = false);

public static class ExternalLinks
{
    public const int MaxCount = 10, MaxTitle = 100, MaxUrl = 2000;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Trims, accepts absolute http/https URLs without user info or inner whitespace only (same rule as the delivery link)
    /// and stores the canonical <see cref="Uri.AbsoluteUri"/>; fills an empty title with the host and forces
    /// VisibleToClient=false when <paramref name="allowClientVisible"/> is false (task links never reach the portal).
    /// </summary>
    public static IReadOnlyList<ExternalLink> Normalize(IEnumerable<ExternalLink>? links, bool allowClientVisible)
    {
        var list = links?.ToList() ?? [];
        if (list.Count > MaxCount)
            throw new DomainException("VALIDATION_FAILED", $"Tối đa {MaxCount} liên kết");

        var result = new List<ExternalLink>(list.Count);
        foreach (var link in list)
        {
            var url = link.Url?.Trim() ?? "";
            if (url.Length > MaxUrl
                || url.Any(char.IsWhiteSpace)
                || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https")
                || !string.IsNullOrEmpty(uri.UserInfo)
                || uri.AbsoluteUri.Length > MaxUrl)
                throw new DomainException("VALIDATION_FAILED", "Liên kết phải là địa chỉ http hoặc https hợp lệ");

            var title = link.Title?.Trim() ?? "";
            if (title.Length == 0) title = uri.Host;
            if (title.Length > MaxTitle)
                throw new DomainException("VALIDATION_FAILED", $"Tên liên kết tối đa {MaxTitle} ký tự");

            result.Add(new ExternalLink(title, uri.AbsoluteUri, allowClientVisible && link.VisibleToClient));
        }
        return result;
    }

    public static IReadOnlyList<ExternalLink> Parse(string json) =>
        JsonSerializer.Deserialize<List<ExternalLink>>(json, Json) ?? [];

    public static string Serialize(IReadOnlyList<ExternalLink> links) => JsonSerializer.Serialize(links, Json);
}
