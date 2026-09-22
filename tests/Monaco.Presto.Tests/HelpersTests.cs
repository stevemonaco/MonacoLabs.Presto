using Monaco.Presto.Core;

namespace Monaco.Presto.Tests;

public sealed class HelpersTests
{
    private sealed class TestClock : Clock;

    private static Timer Child(string id, int priority = 1) => new(new TimerParams { Id = id, Priority = priority }, null, 0);

    private static bool SortByPriority(Timer prev, Timer child) => prev._priority > child._priority;

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

    [Fact]
    public void AddChildAppendsToTheLinkedList()
    {
        var parent = new TestClock();
        var child1 = Child("1");
        var child2 = Child("2");
        var child3 = Child("3");

        Helpers.AddChild(parent, child1);

        Assert.Equal("1", parent._head!.Id);
        Assert.Equal("1", parent._tail!.Id);

        Helpers.AddChild(parent, child2);

        Assert.Equal("1", parent._head!.Id);
        Assert.Equal("2", parent._tail!.Id);
        Assert.Null(child1._prev);
        Assert.Equal("2", child1._next!.Id);
        Assert.Equal("1", child2._prev!.Id);
        Assert.Null(child2._next);

        Helpers.AddChild(parent, child3);

        Assert.Equal("1", parent._head!.Id);
        Assert.Equal("3", parent._tail!.Id);
        Assert.Equal("3", child2._next!.Id);
        Assert.Equal("2", child3._prev!.Id);
        Assert.Null(child3._next);
    }

    [Fact]
    public void AddChildInsertsByPriorityWhenSorted()
    {
        var parent = new TestClock();
        var child1 = Child("1", 999);
        var child2 = Child("2", 42);
        var child3 = Child("3", 100);

        Helpers.AddChild(parent, child1, SortByPriority);

        Assert.Equal("1", parent._head!.Id);
        Assert.Equal("1", parent._tail!.Id);

        Helpers.AddChild(parent, child2, SortByPriority);

        Assert.Equal("2", parent._head!.Id);
        Assert.Equal("1", parent._tail!.Id);
        Assert.Null(child2._prev);
        Assert.Equal("1", child2._next!.Id);
        Assert.Equal("2", child1._prev!.Id);
        Assert.Null(child1._next);

        Helpers.AddChild(parent, child3, SortByPriority);

        Assert.Equal("2", parent._head!.Id);
        Assert.Equal("1", parent._tail!.Id);
        Assert.Equal("3", child2._next!.Id);
        Assert.Equal("2", child3._prev!.Id);
        Assert.Equal("1", child3._next!.Id);
        Assert.Equal("3", child1._prev!.Id);
        Assert.Null(child1._next);
    }

    [Fact]
    public void RemoveChildUnlinksAndKeepsTheListIntact()
    {
        var parent = new TestClock();
        var child1 = Child("1");
        var child2 = Child("2");
        var child3 = Child("3");
        Helpers.AddChild(parent, child1);
        Helpers.AddChild(parent, child2);
        Helpers.AddChild(parent, child3);

        Helpers.RemoveChild(parent, child1);

        Assert.Null(child1._prev);
        Assert.Null(child1._next);
        Assert.Equal("2", parent._head!.Id);
        Assert.Equal("3", parent._tail!.Id);
        Assert.Null(child2._prev);
        Assert.Equal("3", child2._next!.Id);
        Assert.Equal("2", child3._prev!.Id);
        Assert.Null(child3._next);

        Helpers.RemoveChild(parent, child3);

        Assert.Null(child3._prev);
        Assert.Null(child3._next);
        Assert.Equal("2", parent._head!.Id);
        Assert.Equal("2", parent._tail!.Id);
        Assert.Null(child2._prev);
        Assert.Null(child2._next);
    }
}
