using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Media;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// The compositor-only registry of COMPOSITOR-PLAN §4.3 step 5, ported from upstream's <c>waapi/composition.js</c>: one
/// animation per composition property, replaced rather than composed, and reachable from <see cref="Presto.Remove"/>.
/// </summary>
public sealed class CompositorRegistryTests : CompositorTestBase
{
    [AvaloniaFact]
    public async Task ASecondAnimationOnOnePropertyCancelsTheFirst()
    {
        var bar = ShowBar();
        var first = bar.AnimateOnCompositor(new() { ["X"] = 40, Duration = 4000, Ease = "linear" });
        Assert.True(Pump(() => Extent(bar, Colors.Red)?.Left > 32), "the first animation never started");

        var second = bar.AnimateOnCompositor(new() { ["X"] = -20, Duration = 200, Ease = "linear" });
        var replaced = first.Then();

        Assert.True(Pump(() => Extent(bar, Colors.Red)?.Left == 10), "the pixels did not follow the second animation");
        Frames.Advance(5000);
        Assert.Same(second, await second.Then());
        Assert.False(first.Completed, "the replaced animation kept running");
        Assert.False(replaced.IsCompleted, "the replaced animation resolved its Then");
    }

    [AvaloniaFact]
    public void AnimationsOnDifferentPropertiesOfOneControlCoexist()
    {
        var bar = ShowBar();

        _ = bar.AnimateOnCompositor(new() { ["X"] = 40, Duration = 2000, Ease = "linear" });
        _ = bar.AnimateOnCompositor(new() { ["Rotate"] = 90, Duration = 2000, Ease = "linear" });

        Assert.True(
            Pump(() => Extent(bar, Colors.Red) is { Height: > 24 } turning && turning.Center.X > 52),
            "the translation and the rotation did not run at once");
    }

    [AvaloniaFact]
    public void RemoveCancelsACompositorAnimation()
    {
        var bar = ShowBar();
        _ = bar.AnimateOnCompositor(new() { ["X"] = 40, Duration = 2000, Ease = "linear" });
        Assert.True(Pump(() => Extent(bar, Colors.Red)?.Left > 32), "the translation never started");

        Presto.Remove(bar);
        var frozen = Extent(bar, Colors.Red);

        Assert.False(Pump(() => Extent(bar, Colors.Red) != frozen, rounds: 8), "the removed animation kept running");
    }

    [AvaloniaFact]
    public void RemovingOnePropertyLeavesTheOtherRunning()
    {
        var bar = ShowBar();
        _ = bar.AnimateOnCompositor(new() { ["X"] = 40, Duration = 2000, Ease = "linear" });
        _ = bar.AnimateOnCompositor(new() { ["Rotate"] = 90, Duration = 2000, Ease = "linear" });
        Assert.True(Pump(() => Extent(bar, Colors.Red)?.Center.X > 52), "the translation never started");

        PrestoAvalonia.Remove(bar, property: "X");
        Tick();
        var stopped = Extent(bar, Colors.Red)!.Value.Center.X;

        Assert.True(Pump(() => Extent(bar, Colors.Red)?.Height > 30), "removing X stopped the rotation too");
        Assert.InRange(Extent(bar, Colors.Red)!.Value.Center.X, stopped - 1, stopped + 1);
    }

    /// <summary>
    /// COMPOSITOR-PLAN §4.4: a layout transition and a compositor animation drive the same server property, so they replace
    /// each other through the registry whichever starts second.
    /// </summary>
    [AvaloniaFact]
    public void ACompositorAnimationTakesTheTranslationFromALayoutTransition()
    {
        var bar = ShowBar();
        _ = bar.AnimateLayout(60000, Eases.None);
        Canvas.SetTop(bar, 80);
        Tick();
        Assert.Equal(40, Extent(bar, Colors.Red)?.Top);

        _ = bar.AnimateOnCompositor(new() { ["Y"] = -20, Duration = 200, Ease = "linear" });

        Assert.True(Pump(() => Extent(bar, Colors.Red)?.Top == 60), "the held translation never followed the compositor animation");
    }

    /// <inheritdoc cref="ACompositorAnimationTakesTheTranslationFromALayoutTransition"/>
    [AvaloniaFact]
    public void ALayoutTransitionTakesTheTranslationFromACompositorAnimation()
    {
        var bar = ShowBar();
        var replaced = bar.AnimateOnCompositor(new() { ["Y"] = 40, Duration = 200, Ease = "linear" });
        Assert.True(Pump(() => Extent(bar, Colors.Red)?.Top > 48), "the compositor animation never started");

        _ = bar.AnimateLayout(60000, Eases.None);
        Canvas.SetTop(bar, 80);
        Tick();

        Assert.Equal(40, Extent(bar, Colors.Red)?.Top);
        Frames.Advance(5000);
        Assert.False(replaced.Completed, "the replaced compositor animation kept running");
    }

    [AvaloniaFact]
    public void RemoveEndsALayoutTransition()
    {
        var bar = ShowBar();
        _ = bar.AnimateLayout(2000, Eases.None);
        Canvas.SetTop(bar, 80);
        Assert.True(Pump(() => Extent(bar, Colors.Red)?.Top > 44, rounds: 60), "the move never started");

        Presto.Remove(bar);
        Tick();
        var frozen = Extent(bar, Colors.Red);

        Assert.InRange(frozen!.Value.Top, 41, 79);
        Assert.False(Pump(() => Extent(bar, Colors.Red) != frozen, rounds: 8), "the removed transition kept moving the control");
    }

#if DEBUG
    [AvaloniaFact]
    public void MixingAShorthandAndACompositorAnimationWarnsOnceEitherWayRound()
    {
        var shorthandFirst = ShowBar();
        var compositorFirst = ShowBar();
        using var warnings = new WarningSink();

        _ = shorthandFirst.Animate(new() { ["X"] = 40, Duration = 2000 });
        Frames.Advance(16);
        _ = shorthandFirst.AnimateOnCompositor(new() { ["Rotate"] = 90, Duration = 2000 });

        _ = compositorFirst.AnimateOnCompositor(new() { ["X"] = 40, Duration = 2000 });
        _ = compositorFirst.Animate(new() { ["Rotate"] = 90, Duration = 2000 });
        Frames.Advance(16);

        Assert.Equal(2, warnings.Messages.Count);
        Frames.Advance(160);
        Assert.Equal(2, warnings.Messages.Count);
    }

    /// <summary>The transition keeps the translation across later layout passes, so taking it over — and warning — happens once.</summary>
    [AvaloniaFact]
    public void MixingAShorthandAndALayoutTransitionWarnsOnceHoweverOftenLayoutMoves()
    {
        var bar = ShowBar();
        using var warnings = new WarningSink();

        _ = bar.Animate(new() { ["X"] = 40, Duration = 2000 });
        Frames.Advance(16);
        _ = bar.AnimateLayout(2000, Eases.None);

        Canvas.SetTop(bar, 60);
        Tick();
        Canvas.SetTop(bar, 80);
        Tick();

        Assert.Single(warnings.Messages);
    }

    [AvaloniaFact]
    public void AShorthandThatHasEndedDoesNotWarnWhenALayoutTransitionMoves()
    {
        var bar = ShowBar();
        using var warnings = new WarningSink();

        _ = bar.Animate(new() { ["X"] = 40, Duration = 200 });
        Frames.Advance(400);
        _ = bar.AnimateLayout(2000, Eases.None);
        Canvas.SetTop(bar, 60);
        Tick();

        Assert.Empty(warnings.Messages);
    }
#endif

    private sealed class WarningSink : ILogSink, IDisposable
    {
        private readonly ILogSink? _previous = Logger.Sink;

        public WarningSink() => Logger.Sink = this;

        public List<string> Messages { get; } = [];

        public bool IsEnabled(LogEventLevel level, string area) => level == LogEventLevel.Warning && area == LogArea.Animations;

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate) => Messages.Add(messageTemplate);

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues) =>
            Messages.Add(messageTemplate);

        public void Dispose() => Logger.Sink = _previous;
    }
}
