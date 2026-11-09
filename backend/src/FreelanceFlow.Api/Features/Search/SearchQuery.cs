using System.Globalization;
using System.Text;

namespace FreelanceFlow.Api.Features.Search;

/// <summary>Pure helpers for GET /api/admin/search. LIKE patterns are escaped so user input never acts as a wildcard.</summary>
internal static class SearchQuery
{
    public const int MinLength = 2;
    public const int MaxLength = 100;
    public const int GroupLimit = 5;

    /// <summary>Escape character passed to ILIKE … ESCAPE.</summary>
    public const string EscapeChar = "\\";

    public static string Normalize(string? q) =>
        string.Join(' ', (q ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static string EscapeLike(string text) =>
        text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    public static string ContainsPattern(string term) => $"%{EscapeLike(term)}%";

    public static string PrefixPattern(string term) => $"{EscapeLike(term)}%";

    /// <summary>"BG-2026-0007", "bg 2026 0007" and "BG20260007" all compare as the number without dashes and spaces.</summary>
    public static string CompactNumber(string term) =>
        new(term.Where(ch => ch != '-' && !char.IsWhiteSpace(ch)).ToArray());

    public static bool HasNumberChars(string term) => CompactNumber(term).Length > 0;

    public static string NumberContainsPattern(string term) => $"%{EscapeLike(CompactNumber(term))}%";

    public static string NumberExactPattern(string term) => EscapeLike(CompactNumber(term));

    /// <summary>Phone numbers are compared without spaces, dots and dashes on both sides ("0901234" finds "0901 234 567").</summary>
    public static string CompactPhone(string term) =>
        new(term.Where(ch => ch is not ('-' or '.') && !char.IsWhiteSpace(ch)).ToArray());

    public static bool HasPhoneChars(string term) => CompactPhone(term).Length > 0;

    public static string PhoneContainsPattern(string term) => $"%{EscapeLike(CompactPhone(term))}%";

    /// <summary>
    /// P14 DF-26: at most <paramref name="max"/> characters of <paramref name="content"/>, centered on the first match of
    /// <paramref name="term"/> (case- and accent-insensitive, like the unaccent ILIKE that found it; falls back to the start),
    /// with "…" where text was cut. Never splits a surrogate pair.
    /// </summary>
    public static string Excerpt(string content, string term, int max = 120)
    {
        if (content.Length <= max) return content;

        var (folded, origin) = Fold(content);
        var needle = Fold(term).Text;
        var at = needle.Length == 0 ? -1 : folded.IndexOf(needle, StringComparison.Ordinal);
        var matchStart = at < 0 ? 0 : origin[at];
        var matchEnd = at < 0 ? 0 : origin[at + needle.Length - 1] + 1;

        var body = max - 2;   // room for "…" on both sides
        var start = Math.Clamp(matchStart - Math.Max(0, body - (matchEnd - matchStart)) / 2, 0, content.Length - body);
        var end = start + body;
        if (start == 0) end = max - 1;
        else if (end == content.Length) start = content.Length - (max - 1);

        if (start > 0 && char.IsLowSurrogate(content[start])) start++;
        if (end < content.Length && char.IsLowSurrogate(content[end])) end--;
        return (start > 0 ? "…" : "") + content[start..end] + (end < content.Length ? "…" : "");
    }

    /// <summary>Lower-case, diacritics removed (đ → d); origin[i] = index in <paramref name="text"/> of folded char i.</summary>
    private static (string Text, List<int> Origin) Fold(string text)
    {
        var sb = new StringBuilder(text.Length);
        var origin = new List<int>(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var part = char.IsSurrogate(c) ? c.ToString()
                : c is 'đ' or 'Đ' ? "d"
                : c.ToString().Normalize(NormalizationForm.FormD);
            foreach (var ch in part)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                sb.Append(char.ToLowerInvariant(ch));
                origin.Add(i);
            }
        }
        return (sb.ToString(), origin);
    }
}
