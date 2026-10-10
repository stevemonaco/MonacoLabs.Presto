using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Diagnostics;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto.Tests;

/// <summary>Covers AvaloniaObjectTargetAdapter, adapting tests/suites/targets.test.js to AvaloniaProperty targets.</summary>
public sealed class TargetAdapterTests : BridgeTestBase
{
    [AvaloniaFact]
    public void NamedPropertiesAnimateToCompletion()
    {
        var border = new Border { Opacity = 0, Width = 10 };

        var animation = border.Animate(new() { ["Opacity"] = 1, ["Width"] = 100, Duration = 100, Ease = "linear" });
        RunUntil(() => animation.Completed);

        Assert.Equal(1, border.Opacity);
        Assert.Equal(100, border.Width);
    }

    [AvaloniaFact]
    public void AttachedPropertiesResolveThroughTheirOwnerType()
    {
        var canvas = new Canvas();
        var child = new Border();
        canvas.Children.Add(child);
        Canvas.SetLeft(child, 0);

        var animation = child.Animate(new() { ["Canvas.Left"] = 100, Duration = 100, Ease = "linear", Autoplay = false });
        animation.Seek(animation.Duration * .5);

        Assert.Equal(50, Canvas.GetLeft(child));
    }

    [AvaloniaFact]
    public void TypedAndNamedPropertiesMixInOneInitializer()
    {
        var border = new Border { Opacity = 0, Width = 0 };

        var animation = border.Animate(new()
        {
            [Visual.OpacityProperty] = .5,
            ["Width"] = 100,
            Duration = 100,
            Autoplay = false,
        });
        animation.Seek(animation.Duration);

        Assert.Equal(.5, border.Opacity);
        Assert.Equal(100, border.Width);
    }

    [AvaloniaFact]
    public void GetAnimationsFiltersByAvaloniaProperty()
    {
        var border = new Border { Opacity = 0, Width = 0 };

        var fading = border.Animate(new() { ["Opacity"] = 1, Duration = 100 });
        var widening = border.Animate(new() { [Layoutable.WidthProperty] = 100, Duration = 100 });

        Assert.Same(fading, Assert.Single(PrestoAvalonia.GetAnimations(border, Visual.OpacityProperty)));
        Assert.Same(widening, Assert.Single(PrestoAvalonia.GetAnimations(border, Layoutable.WidthProperty)));
    }

    [AvaloniaFact]
    public void PropertiesWithACodecAnimateThroughIt()
    {
        var border = new Border { Background = Brushes.Red };

        var animation = border.Animate(new()
        {
            ["Margin"] = TweenValue.Of(new Thickness(10, 20, 30, 40)),
            ["Background"] = "#0000ff",
            Duration = 100,
            Ease = "linear",
            Autoplay = false,
        });
        animation.Seek(animation.Duration * .5);

        Assert.Equal(new Thickness(5, 10, 15, 20), border.Margin);
        animation.Seek(animation.Duration);
        Assert.Equal(Colors.Blue, Assert.IsAssignableFrom<ISolidColorBrush>(border.Background).Color);
    }

    [AvaloniaFact]
    public void AnimatedValuesLandAtAnimationPriorityAndRevertUncoversTheLocalValue()
    {
        var border = new Border { Opacity = .25 };
        var animation = border.Animate(new() { ["Opacity"] = 1, Duration = 100 });

        // Every frame writes at animation priority, so reverting only restores the local value if the writes replaced each other.
        RunUntil(() => animation.Completed);

        Assert.Equal(1, border.Opacity);
        Assert.Equal(BindingPriority.Animation, border.GetDiagnostic(Visual.OpacityProperty).Priority);

        animation.Revert();

        Assert.Equal(.25, border.Opacity);
        Assert.False(border.IsAnimating(Visual.OpacityProperty));
    }

    [AvaloniaTheory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void AnOvershootingEaseNeverWritesOpacityOutsideZeroToOne(double from, double to)
    {
        var border = new Border { Opacity = from };
        Canvas.SetLeft(border, from);
        var written = new List<double>();
        border.PropertyChanged += (_, e) =>
        {
            if (e.Property == Visual.OpacityProperty) written.Add((double)e.NewValue!);
        };

        var animation = border.Animate(new() { ["Opacity"] = to, ["Canvas.Left"] = to, Duration = 100, Ease = Eases.OutBack(), Autoplay = false });
        var furthest = 0d;
        for (var time = 0d; time <= 100; time += 5)
        {
            animation.Seek(time);
            furthest = Math.Max(furthest, Math.Abs(Canvas.GetLeft(border) - .5));
        }

        Assert.All(written, opacity => Assert.InRange(opacity, 0, 1));
        Assert.Contains(to, written);
        Assert.True(furthest > .5, "only Opacity is clamped, so Canvas.Left should overshoot");
    }

    [AvaloniaFact]
    public void RevertRestoresADirectPropertyToTheValueItHadBeforeTheAnimation()
    {
        var target = new DirectTarget { Value = 25 };
        var animation = target.Animate(new() { [DirectTarget.ValueProperty] = TweenValue.FromTo(50, 100), Duration = 100, Autoplay = false });
        animation.Seek(animation.Duration);

        Assert.Equal(100, target.Value);

        animation.Revert();

        Assert.Equal(25, target.Value);
    }

    [AvaloniaFact]
    public void APanelsChildrenFlattenIntoOneTargetPerControl()
    {
        var panel = new StackPanel();
        for (var i = 0; i < 3; i++) panel.Children.Add(new Border { Opacity = 0 });

        var animation = Presto.Animate(panel.Children, new AvaloniaAnimationParams { ["Opacity"] = 1, Duration = 100, Autoplay = false });
        animation.Seek(animation.Duration);

        Assert.Equal(3, animation.Targets.Count);
        Assert.All(panel.Children, child => Assert.Equal(1, child.Opacity));
    }

    [AvaloniaFact]
    public void AnUnknownPropertyNameThrows()
    {
        var error = Assert.Throws<ArgumentException>(() => new Border().Animate(new() { ["Nope"] = 1 }));

        Assert.Contains(nameof(Border), error.Message);
        Assert.Contains("Nope", error.Message);
    }
}

/// <summary>A target whose animated property is direct, which no write can leave at animation priority.</summary>
internal sealed class DirectTarget : AvaloniaObject
{
    public static readonly DirectProperty<DirectTarget, double> ValueProperty =
        AvaloniaProperty.RegisterDirect<DirectTarget, double>(nameof(Value), o => o.Value, (o, v) => o.Value = v);

    private double _value;

    public double Value
    {
        get => _value;
        set => SetAndRaise(ValueProperty, ref _value, value);
    }
}
