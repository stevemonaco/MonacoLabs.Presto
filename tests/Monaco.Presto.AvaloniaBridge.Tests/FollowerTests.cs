using Avalonia;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Monaco.Presto.AvaloniaBridge.Bindings;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>Drives the value-driven follower directly, without a binding (GAPS-PLAN §1).</summary>
public sealed class FollowerTests : BridgeTestBase
{
    private static readonly FollowParams Linear100 = new() { Duration = 100, Ease = "linear" };

    [AvaloniaFact]
    public void TheFirstValueSnaps()
    {
        var follower = new Follower(typeof(double), new(), 0d);

        follower.Target(100d);

        Assert.Equal(100d, follower.Value);
    }

    [AvaloniaFact]
    public void AnimateInitialEasesTheFirstValueFromThePreBindingValue()
    {
        var follower = new Follower(typeof(double), Linear100 with { AnimateInitial = true }, 20d);

        follower.Target(120d);
        Frames.Advance(48, 16);

        Assert.InRange((double)follower.Value!, 50, 90);
        Frames.Advance(64, 16);
        Assert.Equal(120d, follower.Value);
    }

    [AvaloniaFact]
    public void AChangeEasesOneEventPerFrameAndLandsExactly()
    {
        var follower = new Follower(typeof(double), new(), null);
        var events = 0;
        follower.PropertyChanged += (_, _) => events++;
        follower.Target(0d);
        follower.Target(100.3d);
        events = 0;

        Frames.Advance(16);
        Assert.Equal(1, events);
        Assert.InRange((double)follower.Value!, 0.001, 100);
        Frames.Advance(2000, 16);

        Assert.Equal(100.3d, follower.Value);
    }

    [AvaloniaFact]
    public void AColorAndABrushEaseAndLandExactly()
    {
        var color = new Follower(typeof(Color), new(), null);
        var brush = new Follower(typeof(IBrush), new(), null);
        color.Target(Colors.Red);
        brush.Target(Brushes.Red);

        color.Target(Colors.Blue);
        brush.Target(Brushes.Blue);
        Frames.Advance(64, 16);

        Assert.NotEqual(Colors.Blue, color.Value);
        Assert.NotEqual(Colors.Blue, Assert.IsAssignableFrom<ISolidColorBrush>(brush.Value).Color);
        Frames.Advance(2000, 16);
        Assert.Equal(Colors.Blue, color.Value);
        Assert.Equal(Colors.Blue, Assert.IsAssignableFrom<ISolidColorBrush>(brush.Value).Color);
    }

    [AvaloniaTheory]
    [InlineData("Gradient")]
    [InlineData("Point")]
    [InlineData("Size")]
    [InlineData("Rect")]
    [InlineData("Vector")]
    [InlineData("RelativePoint")]
    [InlineData("BoxShadows")]
    public void EachChannelTypeEasesAndLandsExactly(string kind)
    {
        (Type type, object from, object to) = kind switch
        {
            "Gradient" => (typeof(IBrush), (object)Gradient(Colors.Red, Colors.Blue), (object)Gradient(Colors.Green, Colors.Yellow)),
            "Point" => (typeof(Point), new Point(0, 0), new Point(10, 20)),
            "Size" => (typeof(Size), new Size(0, 0), new Size(10, 20)),
            "Rect" => (typeof(Rect), new Rect(0, 0, 10, 10), new Rect(10, 20, 30, 40)),
            "Vector" => (typeof(Vector), new Vector(0, 0), new Vector(10, 20)),
            "RelativePoint" => (typeof(RelativePoint), new RelativePoint(0, 0, RelativeUnit.Relative), new RelativePoint(1, 1, RelativeUnit.Relative)),
            _ => (typeof(BoxShadows), new BoxShadows(new BoxShadow { Blur = 0, Color = Colors.Black }), new BoxShadows(new BoxShadow { OffsetX = 10, Blur = 8, Color = Colors.Black })),
        };
        var follower = new Follower(type, Linear100, null);
        follower.Target(from);

        follower.Target(to);
        Frames.Advance(48, 16);

        Assert.NotEqual(Key(from), Key(follower.Value));
        Assert.NotEqual(Key(to), Key(follower.Value));
        Frames.Advance(2000, 16);
        Assert.Equal(Key(to), Key(follower.Value));
    }

    [AvaloniaFact]
    public void AnIntEasesThroughWholeNumbers()
    {
        var follower = new Follower(typeof(int), Linear100, null);
        follower.Target(0);

        follower.Target(10);
        Frames.Advance(48, 16);

        Assert.InRange(Assert.IsType<int>(follower.Value), 1, 9);
    }

    [AvaloniaFact]
    public void ASpringRetargetKeepsTheVelocityOfADouble()
    {
        var follower = new Follower(typeof(double), new(), null);
        follower.Target(0d);
        follower.Target(100d);
        Frames.Advance(80, 5);

        var (before, after) = StepsAround(() => (double)follower.Value!, () => follower.Target(120d));

        Assert.True(before > .2, $"moving {before} per ms before the retarget");
        Assert.Equal(before, after, before * .1);
    }

    [AvaloniaFact]
    public void ASpringRetargetKeepsTheVelocityOfEachThicknessChannel()
    {
        var follower = new Follower(typeof(Thickness), new(), null);
        follower.Target(new Thickness(0));
        follower.Target(new Thickness(100, 0, 50, 0));
        Frames.Advance(80, 5);

        var left = StepsAround(() => ((Thickness)follower.Value!).Left, () => follower.Target(new Thickness(120, 0, 60, 0)));

        Assert.True(left.Before > .2, $"moving {left.Before} per ms before the retarget");
        Assert.Equal(left.Before, left.After, left.Before * .1);
        Assert.Equal(0, ((Thickness)follower.Value!).Top);
    }

    [AvaloniaFact]
    public void ADurationEaseRestartsFromTheDisplayedValueWithItsFullDuration()
    {
        var follower = new Follower(typeof(double), Linear100, null);
        follower.Target(0d);
        follower.Target(100d);
        Frames.Advance(48, 16);
        var displayed = (double)follower.Value!;

        follower.Target(0d);
        Frames.Advance(80, 16);

        Assert.InRange((double)follower.Value!, displayed * .01, displayed * .15);
        Frames.Advance(32, 16);
        Assert.Equal(0d, follower.Value);
    }

    [AvaloniaFact]
    public void ValuesThatCannotBeEasedPassThrough()
    {
        var error = new BindingNotification(new InvalidOperationException(), BindingErrorType.Error);

        AssertPassesThrough(typeof(string), "a", "b");
        AssertPassesThrough(typeof(HorizontalAlignment), HorizontalAlignment.Left, HorizontalAlignment.Right);
        AssertPassesThrough(typeof(bool), false, true);
        AssertPassesThrough(typeof(IBrush), Brushes.Red, null);
        AssertPassesThrough(typeof(double), 1d, AvaloniaProperty.UnsetValue);
        AssertPassesThrough(typeof(double), 1d, error);
        AssertPassesThrough(typeof(object), 1d, new Thickness(2));
        AssertPassesThrough(typeof(IBrush), Brushes.Red, new LinearGradientBrush { GradientStops = { new(Colors.Blue, 0), new(Colors.Green, 1) } });

        var follower = new Follower(typeof(double), new(), null);
        follower.Target(1d);
        follower.Target(null);
        Assert.Equal(AvaloniaProperty.UnsetValue, follower.Value);
    }

    [AvaloniaFact]
    public void AValueIsConvertedToThePropertyType()
    {
        var follower = new Follower(typeof(double), new(), null);

        follower.Target("12");

        Assert.Equal(12d, follower.Value);
    }

    [AvaloniaFact]
    public void ReducedMotionSnapsUnlessWhenReducedGivesATiming()
    {
        Presto.Engine.Defaults.ReducedMotion = true;
        var snapping = new Follower(typeof(double), new(), null);
        var easing = new Follower(typeof(double), new() { WhenReduced = Presto.Build().Duration(100).Ease("linear") }, null);
        snapping.Target(0d);
        easing.Target(0d);

        snapping.Target(100d);
        easing.Target(100d);
        Frames.Advance(48, 16);

        Assert.Equal(100d, snapping.Value);
        Assert.InRange((double)easing.Value!, 30, 70);
    }

    [AvaloniaFact]
    public void ASnapRequestLandsAtOnce()
    {
        var follower = new Follower(typeof(double), new(), null);
        follower.Target(0d);
        follower.Target(100d);
        Frames.Advance(32, 16);

        follower.Target(50d, snap: true);
        Frames.Advance(32, 16);

        Assert.Equal(50d, follower.Value);
    }

    [AvaloniaFact]
    public void StopLandsOnTheLatestTargetAndStopsTicking()
    {
        var follower = new Follower(typeof(double), new(), null);
        follower.Target(0d);
        follower.Target(100d);
        Frames.Advance(32, 16);

        follower.Stop();
        var events = 0;
        follower.PropertyChanged += (_, _) => events++;
        Frames.Advance(64, 16);

        Assert.Equal(100d, follower.Value);
        Assert.Equal(0, events);
    }

    [AvaloniaFact]
    public void AFollowedDoubleAllocatesOnlyItsBoxPerFrame()
    {
        var follower = new Follower(typeof(double), new() { Duration = 100000, Ease = "linear" }, null);
        follower.PropertyChanged += (_, _) => _ = follower.Value;
        follower.Target(0d);
        follower.Target(100000d);

        Assert.True(BytesPerTick() <= 24, $"{BytesPerTick()} B per tick");
    }

    [AvaloniaFact]
    public void EachTimingSourceResolves()
    {
        var durationEase = FollowTiming.Resolve(new() { Duration = 300, Ease = "linear" }, reduced: false);
        Assert.Equal(300, durationEase.Duration!.Value.AsDouble);
        Assert.Equal("linear", durationEase.Ease!.Value.Raw);
        Assert.False(durationEase.PreserveVelocity);

        AssertSpring(FollowTiming.Resolve(new() { Spring = "Snappy" }, false), .15, 300);
        AssertSpring(FollowTiming.Resolve(new() { Spring = "0.2,400" }, false), .2, 400);
        AssertSpring(FollowTiming.Resolve(new() { Bounce = .3, Duration = 200 }, false), .3, 200);
        AssertSpring(FollowTiming.Resolve(new(), false), 0, 500);
        AssertSpring(FollowTiming.Resolve(new() { Ease = Springs.Bouncy }, false), .35, 500);

        var builder = FollowTiming.Resolve(new() { Builder = Presto.Build().Duration(250).Ease("outQuad") }, false);
        Assert.Equal(250, builder.Duration!.Value.AsDouble);
        Assert.Equal("outQuad", builder.Ease!.Value.Raw);

        var token = FollowTiming.Resolve(new() { Token = MotionTokens.Expressive }, false);
        Assert.Equal(MotionTokens.Expressive.Medium, token.Duration!.Value.AsDouble);
        Assert.Equal(MotionTokens.Expressive.Ease.Raw, token.Ease!.Value.Raw);

        var reduced = FollowTiming.Resolve(new() { WhenReduced = Presto.Build().Duration(80) }, reduced: true);
        Assert.Equal(80, reduced.Duration!.Value.AsDouble);
    }

    [AvaloniaFact]
    public void ConflictingTimingSourcesThrow()
    {
        Assert.Throws<ArgumentException>(() => FollowTiming.Resolve(new() { Builder = Presto.Build(), Duration = 100 }, false));
        Assert.Throws<ArgumentException>(() => FollowTiming.Resolve(new() { Spring = "Gentle", Bounce = .2 }, false));
        Assert.Throws<ArgumentException>(() => FollowTiming.Resolve(new() { Bounce = .2, Ease = "linear" }, false));
        Assert.Throws<ArgumentException>(() => FollowTiming.Resolve(new() { Ease = Springs.Gentle, Duration = 100 }, false));
        Assert.Throws<FormatException>(() => new Follower(typeof(double), new() { Spring = "Wobbly" }, null));
    }

    [AvaloniaFact]
    public void SpringPresetsParseByNameOrBounceAndDuration()
    {
        AssertSpring(FollowTiming.Resolve(new() { Spring = " stiff " }, false), 0, 250);
        AssertSpring(FollowTiming.Resolve(new() { Spring = "Bouncy" }, false), .35, 500);
        AssertSpring(FollowTiming.Resolve(new() { Spring = "0.1, 350" }, false), .1, 350);
        Assert.Throws<FormatException>(() => FollowTiming.Resolve(new() { Spring = "0.1" }, false));
        Assert.Throws<FormatException>(() => FollowTiming.Resolve(new() { Spring = "bouncy,300" }, false));
    }

    private static LinearGradientBrush Gradient(Color start, Color end) => new() { GradientStops = { new(start, 0), new(end, 1) } };

    private static object? Key(object? value) =>
        value is IGradientBrush gradient ? string.Join(",", gradient.GradientStops.Select(stop => $"{stop.Color}@{stop.Offset}")) : value;

    private static void AssertSpring(AnimatableProperty timing, double bounce, double duration)
    {
        var spring = Assert.IsType<Spring>(timing.Ease!.Value.Raw);
        Assert.Equal((bounce, duration), (spring.Bounce, spring.Duration));
        Assert.Null(timing.Duration);
        Assert.True(timing.PreserveVelocity);
    }

    private static void AssertPassesThrough(Type type, object? first, object? second)
    {
        var follower = new Follower(type, new(), null);
        follower.Target(first);
        follower.Target(second);
        Assert.Equal(second, follower.Value);
    }

    private (double Before, double After) StepsAround(Func<double> read, Action retarget)
    {
        // Resume rebases the start 12 ms back, as upstream does, so the frame after a retarget covers 17 ms.
        const double resumed = 12;
        var previous = read();
        Frames.Advance(5);
        var current = read();
        retarget();
        Frames.Advance(5);
        return ((current - previous) / 5, (read() - current) / (5 + resumed));
    }
}
