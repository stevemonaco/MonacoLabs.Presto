using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// Covers the transform shorthands, adapting the "Render …", "Multiple animations on same target aggregate …" and
/// "Revert cleans/preserves …" groups of tests/suites/transforms.test.js. The rest of that suite is about the CSS transform
/// string itself and has no render transform counterpart: parsing inline transforms, calc()/clamp() values, the 3D
/// transforms (translateZ, rotate3d, perspective) and the matrix()/matrix3d() functions it preserves.
/// </summary>
public sealed class TransformsTests : BridgeTestBase
{
    [AvaloniaFact]
    public void XAndYComposeIntoASingleTranslateTransform()
    {
        var border = new Border();

        var animation = border.Animate(new() { ["X"] = 100, ["Y"] = 200, Duration = 100, Autoplay = false });
        animation.Seek(animation.Duration);

        var group = Assert.IsType<TransformGroup>(border.RenderTransform);
        var translate = Assert.IsType<TranslateTransform>(Assert.Single(group.Children));
        Assert.Equal(100, translate.X);
        Assert.Equal(200, translate.Y);
    }

    [AvaloniaFact]
    public void SeparateAnimationsOnTheSameVisualAggregate()
    {
        var border = new Border();

        var first = border.Animate(new() { ["X"] = 50, ["Rotate"] = 45, Duration = 100, Autoplay = false });
        var second = border.Animate(new() { ["Y"] = 100, ["Scale"] = 2, Duration = 100, Autoplay = false });
        first.Seek(first.Duration);
        second.Seek(second.Duration);

        var group = Assert.IsType<TransformGroup>(border.RenderTransform);
        Assert.Equal(3, group.Children.Count);
        Assert.Equal(2, Assert.IsType<ScaleTransform>(group.Children[0]).ScaleX);
        Assert.Equal(45, Assert.IsType<RotateTransform>(group.Children[1]).Angle);
        var translate = Assert.IsType<TranslateTransform>(group.Children[2]);
        Assert.Equal(50, translate.X);
        Assert.Equal(100, translate.Y);
    }

    [AvaloniaFact]
    public void SkewXAndSkewYComposeIntoOneSkewTransformAheadOfTheTranslation()
    {
        var border = new Border();

        var animation = border.Animate(new() { ["SkewX"] = 20, ["SkewY"] = 10, ["X"] = 40, Duration = 100, Autoplay = false });
        animation.Seek(animation.Duration);

        var group = Assert.IsType<TransformGroup>(border.RenderTransform);
        Assert.Equal(2, group.Children.Count);
        var skew = Assert.IsType<SkewTransform>(group.Children[0]);
        Assert.Equal(20, skew.AngleX);
        Assert.Equal(10, skew.AngleY);
        Assert.Equal(40, Assert.IsType<TranslateTransform>(group.Children[1]).X);
    }

    [AvaloniaFact]
    public void RotateXRotateYAndPerspectiveComposeIntoOneRotate3DTransformBetweenScaleAndRotate()
    {
        var border = new Border();

        var flat = border.Animate(new() { ["X"] = 40, ["Y"] = 20, ["Scale"] = 2, ["Rotate"] = 45, Duration = 100, Autoplay = false });
        flat.Seek(flat.Duration);
        var group = Assert.IsType<TransformGroup>(border.RenderTransform);
        Assert.DoesNotContain(group.Children, child => child is Rotate3DTransform);

        var tilted = border.Animate(new() { ["RotateX"] = 30, ["RotateY"] = 60, ["Perspective"] = 500, Duration = 100, Autoplay = false });
        tilted.Seek(tilted.Duration);

        Assert.Equal(4, group.Children.Count);
        Assert.Equal(2, Assert.IsType<ScaleTransform>(group.Children[0]).ScaleX);
        var rotate3D = Assert.IsType<Rotate3DTransform>(group.Children[1]);
        Assert.Equal(30, rotate3D.AngleX);
        Assert.Equal(60, rotate3D.AngleY);
        Assert.Equal(500, rotate3D.Depth);
        Assert.Equal(45, Assert.IsType<RotateTransform>(group.Children[2]).Angle);
        var translate = Assert.IsType<TranslateTransform>(group.Children[3]);
        Assert.Equal(40, translate.X);
        Assert.Equal(20, translate.Y);
    }

    [AvaloniaFact]
    public void RotateYAcceptsAngleUnits()
    {
        var border = new Border();

        var animation = border.Animate(new() { ["RotateY"] = "0.5turn", Duration = 100, Autoplay = false });
        animation.Seek(animation.Duration);

        var group = Assert.IsType<TransformGroup>(border.RenderTransform);
        Assert.Equal(180, Assert.IsType<Rotate3DTransform>(Assert.Single(group.Children)).AngleY, 10);
    }

    /// <summary>Upstream renders <c>scale(1.5) scaleX(2) scaleY(3)</c>; one <see cref="ScaleTransform"/> has to multiply them instead.</summary>
    [AvaloniaFact]
    public void AUniformScaleMultipliesThePerAxisScales()
    {
        var border = new Border();

        var animation = border.Animate(new() { ["Scale"] = 1.5, ["ScaleX"] = 2, ["ScaleY"] = 3, Duration = 100, Autoplay = false });
        animation.Seek(animation.Duration);

        var scale = Assert.IsType<ScaleTransform>(Assert.Single(Assert.IsType<TransformGroup>(border.RenderTransform).Children));
        Assert.Equal(3, scale.ScaleX);
        Assert.Equal(4.5, scale.ScaleY);
    }

    [AvaloniaTheory]
    [InlineData("1turn", 360)]
    [InlineData("0.5rad", 0.5 * 180 / Math.PI)]
    [InlineData("45deg", 45)]
    public void AngleUnitsEndInDegrees(string value, double expected)
    {
        var border = new Border();

        var animation = border.Animate(new() { ["Rotate"] = value, Duration = 100, Autoplay = false });
        animation.Seek(animation.Duration);

        var group = Assert.IsType<TransformGroup>(border.RenderTransform);
        Assert.Equal(expected, Assert.IsType<RotateTransform>(Assert.Single(group.Children)).Angle, 10);
    }

    [AvaloniaFact]
    public void ShorthandsReadTheirIdentityValueBeforeBeingAnimated()
    {
        var border = new Border();

        Assert.Equal(1d, Presto.Get(border, "Scale"));
        Assert.Equal(0d, Presto.Get(border, "X"));
        Assert.Equal("0deg", Presto.Get(border, "Rotate"));
        Assert.Null(border.RenderTransform);

        var animation = border.Animate(new() { ["Scale"] = "*=3", Duration = 100, Autoplay = false });
        animation.Seek(animation.Duration);

        Assert.Equal(3d, Presto.Get(border, "Scale"));
        Assert.Equal(3, Assert.IsType<ScaleTransform>(Assert.Single(Assert.IsType<TransformGroup>(border.RenderTransform).Children)).ScaleY);
    }

    [AvaloniaFact]
    public void RevertRestoresTheRenderTransformTheVisualAlreadyHad()
    {
        var existing = new RotateTransform(45);
        var border = new Border { RenderTransform = existing };

        var animation = border.Animate(new() { ["X"] = 100, ["Y"] = 50, Duration = 100, Autoplay = false });
        animation.Seek(animation.Duration);

        var group = Assert.IsType<TransformGroup>(border.RenderTransform);
        Assert.Same(existing, group.Children[0]);

        animation.Revert();

        Assert.Same(existing, border.RenderTransform);
    }

    [AvaloniaFact]
    public void RevertKeepsTheShorthandsAnotherAnimationStillAnimates()
    {
        var border = new Border();
        var translating = border.Animate(new() { ["X"] = 100, Duration = 100, Autoplay = false });
        var rotating = border.Animate(new() { ["Rotate"] = 90, Duration = 100, Autoplay = false });
        translating.Seek(translating.Duration);
        rotating.Seek(rotating.Duration);

        translating.Revert();

        var group = Assert.IsType<TransformGroup>(border.RenderTransform);
        Assert.Equal(90, Assert.IsType<RotateTransform>(Assert.Single(group.Children)).Angle);

        rotating.Revert();

        Assert.Null(border.RenderTransform);
    }

    /// <summary>Fluent's <c>Button:pressed</c> style sets a <c>scale(0.98)</c> render transform through a transition.</summary>
    [AvaloniaFact]
    public void PressingAnAnimatedButtonKeepsItDrawnAtItsAnimatedPositionAndComposesThePressedScale()
    {
        var button = new Button { Content = "Click me", Width = 80, Height = 30, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        var canvas = new Canvas { Children = { button } };
        var window = new Window { Content = canvas, Width = 400, Height = 200 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var animation = button.Animate(new() { ["X"] = 200, Duration = 1000, Ease = "linear" });

        Matrix Frame()
        {
            Frames.Advance(16);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var drawn = button.TransformToVisual(canvas)!.Value;
            Assert.Equal((double)Presto.Get(button, "X")!, drawn.M31, 1d);
            return drawn;
        }

        for (var i = 0; i < 10; i++) Frame();
        var point = button.TranslatePoint(new Point(40, 15), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        for (var i = 0; i < 5; i++) Assert.Equal(0.98, Frame().M11, 3);
        window.MouseUp(point, MouseButton.Left);
        for (var i = 0; i < 5; i++) Assert.Equal(1, Frame().M11, 3);

        animation.Revert();

        Assert.IsNotType<TransformGroup>(button.RenderTransform);
    }
}
