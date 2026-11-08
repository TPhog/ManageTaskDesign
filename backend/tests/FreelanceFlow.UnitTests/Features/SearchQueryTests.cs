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
}
