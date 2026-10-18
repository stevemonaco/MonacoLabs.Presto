using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto.Tests;

/// <summary>Member types with no codec tween discretely through <see cref="DiscreteCodec"/>, switching by a <see cref="DiscreteRule"/>.</summary>
public sealed class DiscreteValuesTests : EngineTestBase
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ABoolSwitchesHalfwayByDefault(bool from, bool to)
    {
        var target = new Toggle { On = from };
        var animation = Animate(target, "On", to);

        animation.Seek(49);
        Assert.Equal(from, target.On);
        animation.Seek(50);
        Assert.Equal(to, target.On);
    }

    [Theory]
    [InlineData(DiscreteRule.AtStart, false, true, 0, false)]
    [InlineData(DiscreteRule.AtStart, false, true, 1, true)]
    [InlineData(DiscreteRule.AtEnd, false, true, 99, false)]
    [InlineData(DiscreteRule.AtEnd, false, true, 100, true)]
    [InlineData(DiscreteRule.TrueThroughout, false, true, 1, true)]
    [InlineData(DiscreteRule.TrueThroughout, true, false, 99, true)]
    [InlineData(DiscreteRule.TrueThroughout, true, false, 100, false)]
    [InlineData(DiscreteRule.FalseThroughout, true, false, 1, false)]
    [InlineData(DiscreteRule.FalseThroughout, false, true, 99, false)]
    [InlineData(DiscreteRule.FalseThroughout, false, true, 100, true)]
    public void EachRuleSwitchesAtItsOwnPoint(DiscreteRule rule, bool from, bool to, double time, bool expected)
    {
        var target = new Toggle { On = from };
        var animation = Animate(target, "On", new TweenParams { To = TweenValue.Of(to), Discrete = rule });

        animation.Seek(time);

        Assert.Equal(expected, target.On);
    }

    [Fact]
    public void ThroughoutRulesFallBackToTheMidpointForAnEnum()
    {
        var target = new Toggle();
        var animation = Animate(target, "Side", new TweenParams { To = TweenValue.Of(Side.Right), Discrete = DiscreteRule.TrueThroughout });

        animation.Seek(49);
        Assert.Equal(Side.Left, target.Side);
        animation.Seek(50);
        Assert.Equal(Side.Right, target.Side);
    }

    [Fact]
    public void SeekingAndReversingCrossTheSwitchBothWays()
    {
        var target = new Toggle();
        var animation = Animate(target, "Side", Side.Right);

        animation.Seek(80);
        Assert.Equal(Side.Right, target.Side);
        animation.Seek(20);
        Assert.Equal(Side.Left, target.Side);

        animation.Seek(80);
        animation.Reverse();
        Frames.Advance(16);
        Assert.Equal(Side.Right, target.Side);
        RunUntil(() => animation.IterationTime < 50);
        Assert.Equal(Side.Left, target.Side);
    }

    [Fact]
    public void KeyframesSwitchHalfwayThroughEachSegment()
    {
        var target = new Toggle();
        var animation = Presto.Animate(target, new()
        {
            ["Side"] = TweenValue.Keyframes(TweenValue.Of(Side.Left), TweenValue.Of(Side.Center), TweenValue.Of(Side.Right)),
            Duration = 200,
            Ease = "linear",
            Autoplay = false,
        });

        (double Time, Side Side)[] expected = [(40, Side.Left), (60, Side.Center), (140, Side.Center), (160, Side.Right), (40, Side.Left)];
        foreach (var (time, side) in expected)
        {
            animation.Seek(time);
            Assert.Equal(side, target.Side);
        }
    }

    [Fact]
    public void RevertRestoresTheOriginalValue()
    {
        var target = new Toggle { Side = Side.Center };
        var animation = Animate(target, "Side", Side.Right);
        animation.Seek(100);

        animation.Revert();

        Assert.Equal(Side.Center, target.Side);
    }

    [Fact]
    public void SetWritesABoolAndAnEnum()
    {
        var target = new Toggle();

        Presto.Set(target, new() { ["On"] = TweenValue.Of(true), ["Side"] = TweenValue.Of(Side.Right) });

        Assert.True(target.On);
        Assert.Equal(Side.Right, target.Side);
    }

    [Theory]
    [InlineData("+=1")]
    [InlineData("-=1")]
    [InlineData("*=2")]
    public void RelativeValuesThrow(string value) =>
        Assert.Throws<ArgumentException>(() => Animate(new Toggle(), "Side", value));

    [Theory]
    [InlineData("On")]
    [InlineData("Side")]
    public void AValueNeitherOfTheTypeNorANumberThrows(string property)
    {
        Assert.Throws<ArgumentException>(() => Animate(new Toggle(), property, "abc"));
        Assert.Throws<ArgumentException>(() => Animate(new Toggle(), property, TweenValue.Of(new object())));
    }

    [Fact]
    public void AdditiveThrows() =>
        Assert.Throws<ArgumentException>(() => Presto.Animate(new Toggle(), new() { ["On"] = TweenValue.Of(true), Composition = Composition.Additive }));

    [Fact]
    public void AnAnimatableRetargetSnaps()
    {
        var target = new Toggle();
        var animatable = Presto.CreateAnimatable(target, new() { ["Side"] = 100 });

        animatable.Set("Side", (double)Side.Right);
        Frames.Advance(16);

        Assert.Equal(Side.Right, target.Side);
    }

    [Fact]
    public void RefreshReReadsAFunctionValue()
    {
        var target = new Toggle();
        var to = Side.Center;
        var animation = Animate(target, "Side", new FunctionValue((_, _, _) => TweenValue.Of(to)));

        to = Side.Right;
        animation.Refresh().Seek(100);

        Assert.Equal(Side.Right, target.Side);
    }

    private static Animation Animate(Toggle target, string property, TweenValue to) =>
        Presto.Animate(target, new() { [property] = to, Duration = 100, Ease = "linear", Autoplay = false });

    private static Animation Animate(Toggle target, string property, Side to) => Animate(target, property, TweenValue.Of(to));

    private static Animation Animate(Toggle target, string property, bool to) => Animate(target, property, TweenValue.Of(to));

    private enum Side
    {
        Left,
        Center,
        Right,
    }

    private sealed class Toggle
    {
        public bool On { get; set; }
        public Side Side { get; set; }
    }
}
