using SmartIELTS.Domain.Common;
using Xunit;

namespace SmartIELTS.UnitTests;

public class AnswerNormalizerTests
{
    [Theory]
    [InlineData("Anderson", "anderson")]
    [InlineData("  ANDERSON  ", "anderson")]
    [InlineData("Anderson.", "anderson")]
    public void Normalize_is_case_and_punctuation_insensitive(string input, string expected)
        => Assert.Equal(expected, AnswerNormalizer.Normalize(input));

    [Fact]
    public void Normalize_collapses_whitespace()
        => Assert.Equal("credit card", AnswerNormalizer.Normalize("  Credit    Card "));

    [Theory]
    [InlineData("colour", "color")]
    [InlineData("theatre", "theater")]
    [InlineData("organise", "organize")]
    public void Normalize_maps_british_spelling_to_american(string input, string expected)
        => Assert.Equal(expected, AnswerNormalizer.Normalize(input));

    [Theory]
    [InlineData("three", "3")]
    [InlineData("Ten", "10")]
    public void Normalize_converts_number_words_to_digits(string input, string expected)
        => Assert.Equal(expected, AnswerNormalizer.Normalize(input));

    [Theory]
    [InlineData("Anderson", "Anderson")]
    [InlineData("anderson", "Anderson")]
    [InlineData("25 october", "25 October / October 25")]
    [InlineData("October 25", "25 October / October 25")]
    [InlineData("three", "3 / three")]
    [InlineData("3", "3 / three")]
    [InlineData("color", "color / colour")]
    [InlineData("colour", "color / colour")]
    [InlineData("Credit Card", "Credit Card")]
    public void IsMatch_accepts_valid_variants(string candidate, string correct)
        => Assert.True(AnswerNormalizer.IsMatch(candidate, correct));

    [Theory]
    [InlineData("", "Anderson")]
    [InlineData("   ", "Anderson")]
    [InlineData("John", "Anderson")]
    [InlineData("24 October", "25 October / October 25")]
    public void IsMatch_rejects_invalid_answers(string candidate, string correct)
        => Assert.False(AnswerNormalizer.IsMatch(candidate, correct));

    [Fact]
    public void IsMatch_ignores_spacing_when_requested()
    {
        Assert.False(AnswerNormalizer.IsMatch("NotGiven", "Not Given"));
        Assert.True(AnswerNormalizer.IsMatch("NotGiven", "Not Given", ignoreSpacing: true));
    }
}
