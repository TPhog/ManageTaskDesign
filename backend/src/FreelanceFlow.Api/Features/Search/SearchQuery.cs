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
}
