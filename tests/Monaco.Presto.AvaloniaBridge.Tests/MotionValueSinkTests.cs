using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Diagnostics;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>The bridge's <see cref="MotionValue"/> sinks: <see cref="Follow.Bind(AvaloniaObject, AvaloniaProperty, MotionValue, FollowParams?)"/>, <c>{presto:Follow}</c> and the <see cref="Follow"/> shorthands (GAPS-PLAN §2.2).</summary>
public sealed class MotionValueSinkTests : BridgeTestBase
{
    private static Border Unsized() => new() { Height = 4, HorizontalAlignment = HorizontalAlignment.Left };

    private static Border Styled()
    {
        var border = Unsized();
        border.Styles.Add(new Style(x => x.OfType<Border>()) { Setters = { new Setter(Layoutable.WidthProperty, 7d) } });
        Show(border);
        return border;
    }

    private static T Part<T>(Visual visual) where T : Transform =>
        Assert.Single(Assert.IsType<TransformGroup>(visual.RenderTransform).Children.OfType<T>());

    private static AttachedProperty<object?> Shorthand(string name) => name switch
    {
        "X" => Follow.XProperty,
        "Y" => Follow.YProperty,
        "Scale" => Follow.ScaleProperty,
        "ScaleX" => Follow.ScaleXProperty,
        "ScaleY" => Follow.ScaleYProperty,
        "Rotate" => Follow.RotateProperty,
        _ => Follow.BlurProperty,
    };

    private static double Shown(Visual visual, string name) => name switch
    {
        "X" => Part<TranslateTransform>(visual).X,
        "Y" => Part<TranslateTransform>(visual).Y,
        "Scale" or "ScaleX" => Part<ScaleTransform>(visual).ScaleX,
        "ScaleY" => Part<ScaleTransform>(visual).ScaleY,
        "Rotate" => Part<RotateTransform>(visual).Angle,
        _ => Assert.IsType<BlurEffect>(visual.Effect).Radius,
    };

    private static (FollowXamlView View, LevelViewModel Model, Border Direct, Border Sprung) ShowView()
    {
        var model = new LevelViewModel();
        var view = new FollowXamlView { DataContext = model };
        Show(view);
        return (view, model, view.FindControl<Border>("Direct")!, view.FindControl<Border>("SprungMotion")!);
    }

    [AvaloniaFact]
    public void BindWritesOncePerFrameHoweverManySourcesChanged()
    {
        var border = Unsized();
        Show(border);
        var (a, b) = (Presto.CreateMotionValue(10), Presto.CreateMotionValue(20));
        Follow.Bind(border, Layoutable.WidthProperty, MotionValue.Combine(a, b, (x, y) => x + y));
        var writes = 0;
        border.PropertyChanged += (_, e) => writes += e.Property == Layoutable.WidthProperty ? 1 : 0;

        Assert.Equal(30, border.Width);
        a.Set(40);
        b.Set(50);
        Assert.Equal(30, border.Width);
        Frames.Advance(16);

        Assert.Equal((90d, 1), (border.Width, writes));
    }

    [AvaloniaFact]
    public void BindIsAtLocalValueOverAStyleAndConvertsToThePropertyType()
    {
        var border = Styled();

        Follow.Bind(border, Layoutable.WidthProperty, Presto.CreateMotionValue(20));
        Follow.Bind(border, Visual.ZIndexProperty, Presto.CreateMotionValue(2.6));

        Assert.Equal(20, border.Width);
        Assert.Equal(BindingPriority.LocalValue, border.GetDiagnostic(Layoutable.WidthProperty).Priority);
        Assert.Equal(3, border.ZIndex);
    }

    [AvaloniaFact]
    public void AnObjectValuedCellBindsTheSameWay()
    {
        var brush = new SolidColorBrush(Colors.Black);
        var source = Presto.CreateMotionValue();

        Follow.Bind(brush, SolidColorBrush.ColorProperty, source.Map([0, 100], [Colors.Red, Colors.Blue]));
        Assert.Equal(Colors.Red, brush.Color);
        source.Set(100);
        Frames.Advance(16);

        Assert.Equal(Colors.Blue, brush.Color);
    }

    [AvaloniaFact]
    public void AnAnimationOfThePropertyLayersOverTheBindingAndRevertsToTheLatestValue()
    {
        var border = Unsized();
        Show(border);
        var source = Presto.CreateMotionValue(20);
        Follow.Bind(border, Layoutable.WidthProperty, source);

        var animation = border.Animate(new() { ["Width"] = 90, Duration = 100, Ease = "linear" });
        Frames.Advance(48, 16);
        source.Set(30);
        Frames.Advance(200, 16);

        Assert.Equal(90, border.Width);
        animation.Revert();
        Assert.Equal(30, border.Width);
        source.Set(60);
        Frames.Advance(16);
        Assert.Equal(60, border.Width);
    }

    [AvaloniaFact]
    public void DisposingStopsTheWrites()
    {
        var border = Styled();
        var source = Presto.CreateMotionValue(20);
        var binding = Follow.Bind(border, Layoutable.WidthProperty, source);

        binding.Dispose();
        source.Set(80);
        Frames.Advance(16);

        Assert.Equal(7, border.Width);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void BindStopsListeningWhileDetachedAndSnapsOnReattach(bool timed)
    {
        var border = Unsized();
        var panel = new StackPanel { Children = { border } };
        Show(panel);
        var source = Presto.CreateMotionValue(20);
        Follow.Bind(border, Layoutable.WidthProperty, source, timed ? new() { Duration = 100, Ease = "linear" } : null);

        source.Set(60);
        Frames.Advance(200, 16);
        panel.Children.Remove(border);
        Assert.Equal(60, border.Width);
        source.Set(80);
        Frames.Advance(200, 16);
        Assert.Equal(60, border.Width);

        panel.Children.Add(border);
        Assert.Equal(80, border.Width);
    }

    [AvaloniaFact]
    public void TheXamlExtensionPassesAViewModelValueThroughAtZeroDurationAndSmoothsItWithASpring()
    {
        var (_, model, direct, sprung) = ShowView();
        Assert.Equal((20d, 20d), (direct.Width, sprung.Width));

        model.Motion.Set(120);
        Frames.Advance(96, 16);

        Assert.Equal(120, direct.Width);
        Assert.InRange(sprung.Width, 21, 119);
        Frames.Advance(2000, 16);
        Assert.Equal(120, sprung.Width);
    }

    [AvaloniaFact]
    public void ADataContextSwapRebindsToTheNewItemsValueAndSnaps()
    {
        var (view, model, direct, sprung) = ShowView();
        var next = new LevelViewModel { Motion = Presto.CreateMotionValue(70) };

        view.DataContext = next;
        Assert.Equal((70d, 70d), (direct.Width, sprung.Width));
        model.Motion.Set(5);
        Frames.Advance(2000, 16);
        Assert.Equal((70d, 70d), (direct.Width, sprung.Width));

        next.Motion.Set(90);
        Frames.Advance(2000, 16);
        Assert.Equal((90d, 90d), (direct.Width, sprung.Width));
    }

    [AvaloniaTheory]
    [InlineData("X")]
    [InlineData("Y")]
    [InlineData("Scale")]
    [InlineData("ScaleX")]
    [InlineData("ScaleY")]
    [InlineData("Rotate")]
    [InlineData("Blur")]
    public void EachShorthandWritesEachFlushedValueIgnoringTheTiming(string name)
    {
        var border = Box();
        Follow.SetTiming(border, Presto.Build().Duration(1000).Ease("linear"));
        Show(border);
        var source = Presto.CreateMotionValue(2);

        border.SetValue(Shorthand(name), source);
        Assert.Equal(2, Shown(border, name));
        source.Set(3);
        Frames.Advance(16);

        Assert.Equal(3, Shown(border, name));
    }

    [AvaloniaFact]
    public void AnXAnimationOverridesTheFollowedValueAndRevertsToTheLatestOne()
    {
        var border = Box();
        Show(border);
        var source = Presto.CreateMotionValue(30);
        Follow.SetX(border, source);

        var animation = border.Animate(new() { ["X"] = 90, Duration = 100, Ease = "linear" });
        Frames.Advance(48, 16);
        source.Set(10);
        Frames.Advance(200, 16);

        Assert.Equal(90, Part<TranslateTransform>(border).X);
        animation.Revert();
        Assert.Equal(10, Part<TranslateTransform>(border).X);
        source.Set(60);
        Frames.Advance(16);
        Assert.Equal(60, Part<TranslateTransform>(border).X);
    }

    [AvaloniaFact]
    public void AnotherValueOrNullReleasesTheSubscription()
    {
        var border = Box();
        Follow.SetTiming(border, Presto.Build().Duration(100).Ease("linear"));
        Show(border);
        var (first, second) = (Presto.CreateMotionValue(30), Presto.CreateMotionValue(5));
        Follow.SetX(border, first);
        Follow.SetY(border, second);

        Follow.SetX(border, 10);
        Follow.SetY(border, null);
        first.Set(70);
        second.Set(50);
        Frames.Advance(200, 16);

        Assert.Equal((10d, 0d), (Part<TranslateTransform>(border).X, Part<TranslateTransform>(border).Y));
    }
}
