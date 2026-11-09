using SmartIELTS.Domain.Common;
using Xunit;

namespace SmartIELTS.UnitTests;

public class BandScoreCalculatorTests
{
    [Theory]
    [InlineData(40, 9.0)]
    [InlineData(39, 9.0)]
    [InlineData(37, 8.5)]
    [InlineData(35, 8.0)]
    [InlineData(30, 7.0)]
    [InlineData(26, 6.5)]
    [InlineData(23, 6.0)]
    [InlineData(0, 2.0)]
    public void Listening_raw_score_maps_to_expected_band(int raw, double expected)
        => Assert.Equal(expected, BandScoreCalculator.ConvertListeningRawToBand(raw));

    [Theory]
    [InlineData(40, 9.0)]
    [InlineData(35, 8.0)]
    [InlineData(33, 7.5)]
    [InlineData(30, 7.0)]
    [InlineData(27, 6.5)]
    [InlineData(23, 6.0)]
    [InlineData(4, 2.5)]
    [InlineData(0, 2.0)]
    public void Reading_raw_score_maps_to_expected_band(int raw, double expected)
        => Assert.Equal(expected, BandScoreCalculator.ConvertReadingRawToBand(raw));

    [Theory]
    [InlineData(5, 5, 40)]
    [InlineData(4, 5, 32)]
    [InlineData(3, 5, 24)]
    [InlineData(0, 5, 0)]
    [InlineData(5, 40, 5)]
    public void Short_sections_are_scaled_to_full_test(int correct, int total, int expected)
        => Assert.Equal(expected, BandScoreCalculator.ScaleToFullTest(correct, total));

    [Fact]
    public void Full_marks_on_a_five_question_section_reaches_band_9()
    {
        Assert.Equal(9.0, BandScoreCalculator.ConvertListeningRawToBandForSection(5, 5));
        Assert.Equal(9.0, BandScoreCalculator.ConvertReadingRawToBandForSection(5, 5));
    }

    [Fact]
    public void Empty_section_does_not_divide_by_zero()
        => Assert.Equal(0, BandScoreCalculator.ScaleToFullTest(0, 0));

    [Theory]
    [InlineData(6.0, 6.0)]
    [InlineData(6.1, 6.0)]
    [InlineData(6.25, 6.5)]
    [InlineData(6.5, 6.5)]
    [InlineData(6.75, 7.0)]
    [InlineData(6.9, 7.0)]
    public void Round_to_half_band_follows_ielts_rules(double input, double expected)
        => Assert.Equal(expected, BandScoreCalculator.RoundToHalfBand(input));

    [Fact]
    public void Overall_band_is_average_rounded_to_half()
        => Assert.Equal(7.0, BandScoreCalculator.CalculateOverallBand(7.5, 7.0, 6.5, 7.0));

    [Fact]
    public void Overall_band_rounds_up_from_point_seventy_five()
        => Assert.Equal(7.0, BandScoreCalculator.CalculateOverallBand(7.5, 7.0, 6.5, 6.5));
}
