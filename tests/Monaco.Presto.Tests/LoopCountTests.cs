namespace Monaco.Presto.Tests;

public sealed class LoopCountTests
{
    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(3, false)]
    [InlineData(true, true)]
    public void IsInfiniteFollowsTheImplicitConversion(object value, bool expectedIsInfinite)
    {
        var loop = value is bool infinite ? (LoopCount)infinite : (LoopCount)(int)value;

        Assert.Equal(expectedIsInfinite, loop.IsInfinite);
    }
}
