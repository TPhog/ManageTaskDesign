using FluentAssertions;
using FreelanceFlow.Api.Features.Search;

namespace FreelanceFlow.UnitTests.Features;

public class SearchQueryTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    [InlineData("  Nguyễn   Văn  A ", "Nguyễn Văn A")]
    public void Normalize_trims_and_collapses_whitespace(string? input, string expected) =>
        SearchQuery.Normalize(input).Should().Be(expected);

    [Fact]
    public void Like_wildcards_and_the_escape_char_are_escaped() =>
        SearchQuery.EscapeLike(@"50%_off\x").Should().Be(@"50\%\_off\\x");

    [Fact]
    public void Contains_and_prefix_patterns_wrap_the_escaped_term()
    {
        SearchQuery.ContainsPattern("a%b").Should().Be(@"%a\%b%");
        SearchQuery.PrefixPattern("a_b").Should().Be(@"a\_b%");
    }

    [Theory]
    [InlineData("BG-2026-0007", "BG20260007")]
    [InlineData("bg 2026 0007", "bg20260007")]
    [InlineData("2026-0007", "20260007")]
    public void Quotation_numbers_compare_without_dashes_and_spaces(string term, string compact) =>
        SearchQuery.CompactNumber(term).Should().Be(compact);

    [Fact]
    public void Number_patterns_are_escaped_and_skipped_when_nothing_is_left()
    {
        SearchQuery.NumberContainsPattern("2026-0007").Should().Be("%20260007%");
        SearchQuery.NumberExactPattern("BG-2026-0007").Should().Be("BG20260007");
        SearchQuery.NumberContainsPattern("1%").Should().Be(@"%1\%%");
        SearchQuery.HasNumberChars("- -").Should().BeFalse();
        SearchQuery.HasNumberChars("BG").Should().BeTrue();
    }

    // ---------- Excerpt (P14 DF-26: request search) ----------

    [Fact]
    public void Excerpt_keeps_short_content_whole() =>
        SearchQuery.Excerpt("Đổi màu logo", "mau").Should().Be("Đổi màu logo");

    [Fact]
    public void Excerpt_of_an_early_match_is_the_start_with_a_trailing_ellipsis()
    {
        var content = "Đổi màu logo " + new string('x', 300);
        SearchQuery.Excerpt(content, "doi mau").Should().Be(content[..119] + "…");
    }

    [Fact]
    public void Excerpt_centers_a_late_match_ignoring_case_and_accents()
    {
        var content = new string('a', 300) + " Đổi MÀU logo " + new string('b', 300);

        var excerpt = SearchQuery.Excerpt(content, "doi mau");

        excerpt.Should().HaveLength(120).And.StartWith("…").And.EndWith("…").And.Contain("Đổi MÀU");
        var at = excerpt.IndexOf("Đổi", StringComparison.Ordinal);
        at.Should().BeInRange(45, 60);   // roughly centered
    }

    [Fact]
    public void Excerpt_of_a_match_near_the_end_ends_with_the_content()
    {
        var content = new string('a', 300) + " cuối";
        var excerpt = SearchQuery.Excerpt(content, "cuoi");
        excerpt.Should().HaveLength(120).And.StartWith("…").And.EndWith(" cuối");
    }

    [Fact]
    public void Excerpt_without_a_findable_match_falls_back_to_the_start()
    {
        var content = new string('x', 300);
        SearchQuery.Excerpt(content, "zz").Should().Be(content[..119] + "…");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Excerpt_never_cuts_a_surrogate_pair(int pad)
    {
        var emojis = string.Concat(Enumerable.Repeat("😀", 150));
        foreach (var content in new[]
                 {
                     new string('x', pad) + emojis,                               // cut at the end
                     new string('x', pad) + emojis + " tìm " + emojis,            // cut on both sides
                     emojis + new string('x', pad) + " tìm",                      // cut at the start
                 })
        {
            var excerpt = SearchQuery.Excerpt(content, "tim");
            excerpt.Length.Should().BeLessThanOrEqualTo(120);
            WellFormed(excerpt).Should().BeTrue(excerpt);
        }
    }

    private static bool WellFormed(string s)
    {
        for (var i = 0; i < s.Length; i++)
        {
            if (char.IsHighSurrogate(s[i]))
            {
                if (i + 1 >= s.Length || !char.IsLowSurrogate(s[i + 1])) return false;
                i++;
            }
            else if (char.IsLowSurrogate(s[i])) return false;
        }
        return true;
    }
}
