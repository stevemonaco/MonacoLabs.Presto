using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Monaco.Presto.AvaloniaBridge.Compositor;
using Monaco.Presto.AvaloniaBridge.Controls;
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
    public void EffectAndRevealMethodsWriteTheIndexerKeysAndValues()
    {
        var expected = new AnimationParams
        {
            ["RotateX"] = 1, ["RotateY"] = 2, ["Perspective"] = 3, ["Blur"] = 4, ["ShadowX"] = 5, ["ShadowY"] = 6,
            ["ShadowBlur"] = 7, ["ShadowColor"] = "#f00", ["ShadowOpacity"] = .5, ["ClipInset"] = "10%", ["ClipCircle"] = 8,
            ["ClipCircleCenter"] = "0.5 0.5", ["MaskSweep"] = .25, ["MaskSweepAngle"] = 90, ["MaskSweepFeather"] = .1,
        };

        var actual = Presto.Build()
            .RotateX(1).RotateY(2).Perspective(3).Blur(4).ShadowX(5).ShadowY(6).ShadowBlur(7).ShadowColor("#f00").ShadowOpacity(.5)
            .ClipInset("10%").ClipCircle(8).ClipCircleCenter("0.5 0.5").MaskSweep(.25).MaskSweepAngle(90).MaskSweepFeather(.1)
            .ToParams();

        Assert.Equal(expected.Properties.Keys, actual.Properties.Keys);
        foreach (var key in expected.Properties.Keys) Assert.Equal(expected[key].Raw, actual[key].Raw);
    }

    [AvaloniaFact]
    public void EffectAndRevealOverloadsWriteTheSameKey()
    {
        FunctionValue function = (_, i, _) => i;

        Assert.Equal(["Blur"], Presto.Build().Blur(from: 0, to: 8, duration: 100).ToParams().Properties.Keys);
        Assert.Equal(["RotateY"], Presto.Build().RotateY(function).ToParams().Properties.Keys);
        Assert.Equal(["MaskSweep"], Presto.Build().MaskSweep(k => k.To(0).To(1)).ToParams().Properties.Keys);
    }

    [AvaloniaFact]
    public void ImageFilterMethodsWriteTheIndexerKeysAndValues()
    {
        var expected = new AvaloniaAnimationParams
        {
            [PrestoImageBase.BrightnessProperty] = 1.5, [PrestoImageBase.ContrastProperty] = 2, [PrestoImageBase.SaturateProperty] = 0.5,
            [PrestoImageBase.GrayscaleProperty] = 1, [PrestoImageBase.SepiaProperty] = 0.8, [PrestoImageBase.HueRotateProperty] = 180,
            [PrestoImageBase.InvertProperty] = 1, [PrestoImageBase.TintProperty] = 0.5, [PrestoImageBase.TintColorProperty] = TweenValue.Of(Colors.Red),
            [PrestoImageBase.DuotoneProperty] = 1, [PrestoImageBase.DuotoneShadowProperty] = TweenValue.Of(Colors.Black), [PrestoImageBase.DuotoneHighlightProperty] = TweenValue.Of(Colors.White),
        };

        var actual = Presto.Build()
            .Brightness(1.5).Contrast(2).Saturate(0.5).Grayscale(1).Sepia(0.8).HueRotate(180).Invert(1).Tint(0.5).TintColor(Colors.Red)
            .Duotone(1).DuotoneShadow(Colors.Black).DuotoneHighlight(Colors.White)
            .ToParams();

        Assert.Equal(expected.Properties.Keys, actual.Properties.Keys);
        foreach (var key in expected.Properties.Keys) Assert.Equal(expected[key].Raw, actual[key].Raw);
    }

    [AvaloniaFact]
    public void ImageFilterOverloadsWriteTheSameKey()
    {
        FunctionValue function = (_, i, _) => i;
        var key = AvaloniaObjectTargetAdapter.Encode(PrestoImageBase.GrayscaleProperty);

        Assert.Equal([key], Presto.Build().Grayscale(1).ToParams().Properties.Keys);
        Assert.Equal([key], Presto.Build().Grayscale(from: 1, to: 0, duration: 100).ToParams().Properties.Keys);
        Assert.Equal([key], Presto.Build().Grayscale(function).ToParams().Properties.Keys);
        Assert.Equal([key], Presto.Build().Grayscale(k => k.To(1).To(0)).ToParams().Properties.Keys);
    }

    [AvaloniaFact]
    public void AFilterMethodAnimatesEveryImageOnTheBase()
    {
        PrestoImageBase[] images = [new FilteredImage(), new SpriteSheet(), new ImageSequence()];

        Presto.Build().Grayscale(1).Dissolve(1).DissolveScale(12).DissolveSoftness(0.3).Pixelate(8).Ripple(4).RippleScale(20)
            .Duration(100).Animate(images);
        Frames.Advance(200);

        Assert.All(images, image => Assert.Equal((1, 1, 12, 0.3, 8, 4, 20),
            (image.Grayscale, image.Dissolve, image.DissolveScale, image.DissolveSoftness, image.Pixelate, image.Ripple, image.RippleScale)));
    }

    [AvaloniaFact]
    public void TypedEffectValuesAreWrappedAsTheIndexerWouldWrapThem()
    {
        var parameters = Presto.Build()
            .ClipInset(new Thickness(2))
            .ClipCircleCenter(RelativePoint.Center)
            .Shadow(1, 2, 3, Colors.Red)
            .ToParams();

        Assert.Equal(new Thickness(2), parameters["ClipInset"].Raw);
        Assert.Equal(RelativePoint.Center, parameters["ClipCircleCenter"].Raw);
        Assert.Equal(["ClipInset", "ClipCircleCenter", "ShadowX", "ShadowY", "ShadowBlur", "ShadowColor"], parameters.Properties.Keys);
        Assert.Equal(3.0, Convert.ToDouble(parameters["ShadowBlur"].Raw));
        Assert.Equal(Colors.Red, parameters["ShadowColor"].Raw);
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
    public void ABuilderRunsOnTheCompositorAsTheEquivalentParamsDo()
    {
        var byParams = ShowBar();
        var byBuilder = ShowBar();

        var expected = byParams.AnimateOnCompositor(new() { ["X"] = 20, ["Y"] = 30, ["Opacity"] = 1, Duration = 150, Ease = "linear", Loop = 1 });
        var actual = byBuilder.AnimateOnCompositor(Presto.Build().X(20).Y(30).Opacity(1).Duration(150).Ease("linear").Loop(1));

        var end = new Rect(50, 70, 40, 20);
        Assert.True(Pump(() => Extent(byParams, Colors.Red) == end && Extent(byBuilder, Colors.Red) == end), "the translations never arrived");

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

    [AvaloniaFact]
    public void ALambdaAnimatesAsItsBuilderDoes()
    {
        var byBuilder = new Border { Opacity = 1 };
        var byLambda = new Border { Opacity = 1 };

        var expected = byBuilder.Animate(Presto.Build().Opacity(0.25).Duration(100).Autoplay(false));
        var actual = byLambda.Animate(b => b.Opacity(0.25).Duration(100).Autoplay(false));
        expected.Seek(expected.Duration);
        actual.Seek(actual.Duration);

        Assert.Equal(expected.Duration, actual.Duration);
        Assert.Equal(0.25, byLambda.Opacity);
    }

    [AvaloniaFact]
    public void ALambdaRunsOnTheCompositorAsItsBuilderDoes()
    {
        var byBuilder = ShowBar();
        var byLambda = ShowBar();

        var expected = byBuilder.AnimateOnCompositor(Presto.Build().X(20).Y(30).Duration(150).Ease("linear"));
        var actual = PrestoAvalonia.AnimateOnCompositor(byLambda, b => b.X(20).Y(30).Duration(150).Ease("linear"));

        var end = new Rect(50, 70, 40, 20);
        Assert.True(Pump(() => Extent(byBuilder, Colors.Red) == end && Extent(byLambda, Colors.Red) == end), "the translations never arrived");
        Frames.Advance(200);
        Assert.True(expected.Completed);
        Assert.True(actual.Completed);
        Assert.Contains("FrameRate", Assert.Throws<NotSupportedException>(() => byLambda.AnimateOnCompositor(b => b.X(10).FrameRate(30))).Message);
    }

    [AvaloniaFact]
    public void ALambdaGivesALayoutTransitionTheMotionItsBuilderGives()
    {
        EaseValue ease = Eases.OutQuad;
        var expected = Presto.Build().Duration(300).Delay(20).Ease(ease).X(ease: Eases.None);
        Action<AnimationBuilder> build = b => b.Duration(300).Delay(20).Ease(ease).X(ease: Eases.None);
        var source = ShowBar();

        Assert.Equal(MotionOf(ShowBar().AnimateLayout(expected)), MotionOf(ShowBar().AnimateLayout(build)));
        Assert.Equal(
            MotionOf(PrestoAvalonia.AnimateSharedElement(source, ShowBar(), expected)),
            MotionOf(PrestoAvalonia.AnimateSharedElement(source, ShowBar(), build)));

        PrestoAvalonia.PrepareSharedElement("builder", source);
        PrestoAvalonia.PrepareSharedElement("lambda", source);
        Assert.Equal(
            MotionOf(PrestoAvalonia.AnimateSharedElement("builder", ShowBar(), expected)!),
            MotionOf(PrestoAvalonia.AnimateSharedElement("lambda", ShowBar(), build)!));
    }

    [AvaloniaFact]
    public void ALambdaExitPlaysOnAGhostAsItsBuilderDoes()
    {
        var expected = PrestoAvalonia.AnimateExit(ShowBar(), Presto.Build().Opacity(to: 0).Duration(200));
        var actual = PrestoAvalonia.AnimateExit(ShowBar(), b => b.Opacity(to: 0).Duration(200));

        Assert.NotNull(expected);
        Assert.Equal(expected.Duration, Assert.IsType<Animation>(actual).Duration);
        expected.Cancel();
        actual.Cancel();
    }

    [AvaloniaFact]
    public void TargetTypedParamsStillBindToTheParamsOverloads()
    {
        var border = new Border { Opacity = 1 };
        var bar = ShowBar();

        border.Animate(new() { ["Opacity"] = 0.0, Duration = 100, Autoplay = false }).Seek(100);
        bar.AnimateOnCompositor(new() { ["X"] = 10, Duration = 100 });

        Assert.Equal(0, border.Opacity);
    }

    private static object MotionOf(LayoutTransition transition) =>
        typeof(LayoutTransition).GetField("_motion", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(transition)!;

    [AvaloniaFact]
    public void ATypedWhenReducedReplacesTheProperties()
    {
        var border = new Border { Width = 10 };
        Presto.Defaults.ReducedMotion = true;

        Presto.Animate(border, new AvaloniaAnimationParams
        {
            [Layoutable.WidthProperty] = 100,
            Duration = 400,
            WhenReduced = new() { [Visual.OpacityProperty] = 0.5 },
        });
        Frames.Advance(1500);

        Assert.Equal(10, border.Width);
        Assert.Equal(0.5, border.Opacity);
    }
}
