using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Monaco.Presto.AvaloniaBridge.Compositor;
using Monaco.Presto.AvaloniaBridge.Targets;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>The bridge's typed <see cref="PropertyBuilderExtensions"/> against the string and <see cref="AvaloniaProperty"/> keys the indexers write.</summary>
public sealed class AnimationBuilderBridgeTests : CompositorTestBase
{
    [AvaloniaFact]
    public void TypedMethodsWriteTheIndexerKeys()
    {
        var parameters = Presto.Build()
            .X(1).Y(1).Rotate(1).Scale(1).ScaleX(1).ScaleY(1).SkewX(1).SkewY(1)
            .Opacity(1).Width(1).Height(1).Background("#fff").Foreground("#fff").CornerRadius(1)
            .BorderThickness(1).Margin(1).Padding(1).Draw("0 1").Text("a").Data("M0,0 L1,1")
            .ToParams();

        Assert.Equal(
            ["X", "Y", "Rotate", "Scale", "ScaleX", "ScaleY", "SkewX", "SkewY", "Opacity", "Width", "Height", "Background",
             "Foreground", "CornerRadius", "BorderThickness", "Margin", "Padding", "Draw", "Text", "Data"],
            parameters.Properties.Keys);
    }

    [AvaloniaFact]
    public void EveryOverloadWritesTheSameKey()
    {
        FunctionValue function = (_, i, _) => i;

        Assert.Equal(["X"], Presto.Build().X(1).ToParams().Properties.Keys);
        Assert.Equal(["X"], Presto.Build().X(from: 0, to: 1, duration: 100).ToParams().Properties.Keys);
        Assert.Equal(["X"], Presto.Build().X(function).ToParams().Properties.Keys);
        Assert.Equal(["X"], Presto.Build().X(k => k.To(1).To(2)).ToParams().Properties.Keys);
        Assert.Equal(["Text"], Presto.Build().Text(1, textModifier: v => v.ToString()).ToParams().Properties.Keys);
    }

    [AvaloniaFact]
    public void TypedValuesAreWrappedAsTheIndexerWouldWrapThem()
    {
        var brush = new ImmutableSolidColorBrush(Colors.Red);
        var geometry = Geometry.Parse("M0,0 L10,10");

        var parameters = Presto.Build()
            .Background(brush)
            .CornerRadius(new CornerRadius(4))
            .Margin(new Thickness(2))
            .Draw(0, 0.5)
            .Data(geometry)
            .ToParams();

        Assert.Same(brush, parameters["Background"].Raw);
        Assert.Equal(new CornerRadius(4), parameters["CornerRadius"].Raw);
        Assert.Equal(new Thickness(2), parameters["Margin"].Raw);
        Assert.Equal("0 0.5", parameters["Draw"].Raw);
        Assert.Same(geometry, parameters["Data"].Raw);
    }

    [AvaloniaFact]
    public void ATypedPropertyReplacesAStringKeyedAnimationOfTheSameProperty()
    {
        var border = new Border { Opacity = 1 };
        var byName = border.Animate(new() { ["Opacity"] = 0.0, Duration = 100, Ease = Eases.None });
        var byHandle = border.Animate(Presto.Build().Property(Visual.OpacityProperty, 1.0).Duration(100).Ease(Eases.None));

        Frames.Advance(50);

        Assert.True(byName.Cancelled);
        Assert.False(byHandle.Cancelled);
        Assert.Equal([AvaloniaObjectTargetAdapter.Encode(Visual.OpacityProperty)],
            Presto.Build().Property(Visual.OpacityProperty, to: 1.0, duration: 50).ToParams().Properties.Keys);
    }

    [AvaloniaFact]
    public void ShorthandsChainOnKeyframeFrames()
    {
        var border = new Border();

        var animation = Presto.Build()
            .Keyframes(k => k.Frame(f => f.X(10)).Frame(f => f.X(20).Rotate(45)))
            .Duration(100)
            .Autoplay(false)
            .Animate(border);
        animation.Seek(animation.Duration);

        var group = Assert.IsType<TransformGroup>(border.RenderTransform);
        Assert.Equal(45, Assert.IsType<RotateTransform>(group.Children[0]).Angle);
        Assert.Equal(20, Assert.IsType<TranslateTransform>(group.Children[1]).X);
    }

    [AvaloniaFact]
    public void BackgroundAnimatesToAColor()
    {
        var border = new Border { Background = Brushes.Black };

        var animation = border.Animate(Presto.Build().Background(Colors.Red).Duration(100).Autoplay(false));
        animation.Seek(animation.Duration);

        Assert.Equal(Colors.Red, Assert.IsAssignableFrom<ISolidColorBrush>(border.Background).Color);
    }

    [AvaloniaFact]
    public void AnimatingAControlWithACoreOnlyBuilderInstallsTheBridge()
    {
        PrestoAvalonia.Initialized = false;
        for (var i = Presto.Adapters.Count - 1; i >= 0; i--)
        {
            if (Presto.Adapters[i] is AvaloniaObjectTargetAdapter or CompositorScratch.Adapter) Presto.Adapters.RemoveAt(i);
        }
        var border = new Border { Opacity = 1 };

        var animation = border.Animate(Presto.Build().Property("Opacity", 0.0).Duration(100).Autoplay(false));
        animation.Seek(animation.Duration);

        Assert.True(PrestoAvalonia.Initialized);
        Assert.Contains(Presto.Adapters, a => a is AvaloniaObjectTargetAdapter);
        Assert.Equal(0, border.Opacity);
    }

    [AvaloniaFact]
    public async Task ABuilderRunsOnTheCompositorAsTheEquivalentParamsDo()
    {
        var byParams = ShowBar();
        var byBuilder = ShowBar();

        var expected = byParams.AnimateOnCompositor(new() { ["X"] = 20, ["Y"] = 30, ["Opacity"] = 1, Duration = 150, Ease = "linear", Loop = 1 });
        var actual = byBuilder.AnimateOnCompositor(Presto.Build().X(20).Y(30).Opacity(1).Duration(150).Ease("linear").Loop(1));

        var end = new Rect(50, 70, 40, 20);
        Assert.True(await Pump(() => Extent(byParams, Colors.Red) == end && Extent(byBuilder, Colors.Red) == end), "the translations never arrived");

        Frames.Advance(200);
        Assert.False(actual.Completed);
        Frames.Advance(200);
        Assert.True(expected.Completed);
        Assert.True(actual.Completed);
    }

    [AvaloniaFact]
    public void KeyframesAreNotSupportedOnTheCompositor()
    {
        var builder = Presto.Build().Keyframes(k => k.Frame(f => f.X(10))).Duration(100);

        var error = Assert.Throws<NotSupportedException>(() => new Border().AnimateOnCompositor(builder));

        Assert.Contains("Keyframes", error.Message);
    }

    [AvaloniaFact]
    public void OnUpdateIsNotSupportedOnTheCompositor()
    {
        var builder = Presto.Build().X(10).OnUpdate(_ => { });

        var error = Assert.Throws<NotSupportedException>(() => PrestoAvalonia.AnimateOnCompositor(new Border(), builder));

        Assert.Contains("OnUpdate", error.Message);
    }
}
