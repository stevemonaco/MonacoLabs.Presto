using Monaco.Presto.Core;

namespace Monaco.Presto.Tests;

public sealed class HelpersTests
{
    [Theory]
    [InlineData(2.5, 3)]
    [InlineData(-2.5, -2)]
    [InlineData(0.49999, 0)]
    [InlineData(0.49999999999999994, 0)]
    [InlineData(-0.4, 0)]
    public void RoundWithZeroDecimalsUsesJavaScriptSemantics(double value, double expected) =>
        Assert.Equal(expected, Helpers.Round(value, 0));

    [Fact]
    public void RoundToDecimals() => Assert.Equal(1.2346, Helpers.Round(1.23456, 4));

    [Fact]
    public void RoundWithNegativeDecimalsReturnsValueUnchanged() => Assert.Equal(1.23456, Helpers.Round(1.23456, -1));

    [Fact]
    public void ClampClampsToBounds()
    {
        Assert.Equal(0, Helpers.Clamp(-1, 0, 1));
        Assert.Equal(1, Helpers.Clamp(2, 0, 1));
        Assert.Equal(0.5, Helpers.Clamp(0.5, 0, 1));
    }

    [Fact]
    public void ClampInfinityReplacesInfinities()
    {
        Assert.Equal(Constants.MaxValue, Helpers.ClampInfinity(double.PositiveInfinity));
        Assert.Equal(-Constants.MaxValue, Helpers.ClampInfinity(double.NegativeInfinity));
        Assert.Equal(42, Helpers.ClampInfinity(42));
    }

    [Fact]
    public void NormalizeTimeClampsZeroToMinValue() => Assert.Equal(Constants.MinValue, Helpers.NormalizeTime(0));

    [Fact]
    public void NormalizeTimeClampsInfinityToMaxValue() =>
        Assert.Equal(Constants.MaxValue, Helpers.NormalizeTime(double.PositiveInfinity));

    [Fact]
    public void NormalizeTimeRoundsToElevenDecimals() =>
        Assert.Equal(1.12345678901, Helpers.NormalizeTime(1.123456789012345));
}
