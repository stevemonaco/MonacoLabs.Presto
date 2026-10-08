using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Path = Avalonia.Controls.Shapes.Path;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>The <see cref="Follow"/> transform and effect shorthands (BRIDGE-FOLLOW).</summary>
public sealed class FollowTests : BridgeTestBase
{
    private static T Part<T>(Visual visual) where T : Transform =>
        Assert.Single(Assert.IsType<TransformGroup>(visual.RenderTransform).Children.OfType<T>());

    private static Border Shown()
    {
        var border = Box();
        Follow.SetTiming(border, Presto.Build().Duration(100).Ease("linear"));
        Show(border);
        return border;
    }

    [AvaloniaFact]
    public void XRotateScaleAndBlurAnimateToTheirBoundValuesAndLandExactly()
    {
        var border = Shown();
        var model = new LevelViewModel { Level = 1 };
        border.DataContext = model;
        foreach (var property in new[] { Follow.XProperty, Follow.RotateProperty, Follow.ScaleProperty, Follow.BlurProperty })
        {
            border.Bind(property, new Binding(nameof(LevelViewModel.Level)));
        }

        model.Level = 3.5;
        Frames.Advance(48, 16);

        var blur = Assert.IsType<BlurEffect>(border.Effect);
        Assert.InRange(Part<TranslateTransform>(border).X, 1.01, 3.49);
        Assert.InRange(Part<RotateTransform>(border).Angle, 1.01, 3.49);
        Assert.InRange(Part<ScaleTransform>(border).ScaleX, 1.01, 3.49);
        Assert.InRange(blur.Radius, 1.01, 3.49);
        Frames.Advance(100, 16);
        Assert.Equal((3.5, 3.5, 3.5, 3.5), (Part<TranslateTransform>(border).X, Part<RotateTransform>(border).Angle, Part<ScaleTransform>(border).ScaleY, blur.Radius));
    }

    [AvaloniaFact]
    public void TheFirstValueSnapsAndAUnitStringConverts()
    {
        var border = Shown();

        Follow.SetX(border, 40);
        Follow.SetRotate(border, "0.25turn");

        Assert.Equal(40, Part<TranslateTransform>(border).X);
        Assert.Equal(90, Part<RotateTransform>(border).Angle, 9);
        Assert.Null(Presto.Engine._head);
    }

    [AvaloniaFact]
    public void AMidFlightRetargetKeepsTheVelocity()
    {
        var border = Box();
        Show(border);
        Follow.SetX(border, 0);
        Follow.SetX(border, 100);
        Frames.Advance(80, 5);

        // Resume rebases the start 12 ms back, as upstream does, so the frame after a retarget covers 17 ms.
        var previous = Part<TranslateTransform>(border).X;
        Frames.Advance(5);
        var current = Part<TranslateTransform>(border).X;
        Follow.SetX(border, 120);
        Frames.Advance(5);
        var (before, after) = ((current - previous) / 5, (Part<TranslateTransform>(border).X - current) / 17);

        Assert.True(before > .2, $"moving {before} per ms before the retarget");
        Assert.Equal(before, after, before * .1);
    }

    [AvaloniaFact]
    public void TimingIsHonoredAndAChangeTakesEffectFromTheNextValue()
    {
        var border = Shown();
        Follow.SetX(border, 0);

        Follow.SetX(border, 100);
        Frames.Advance(48, 16);
        Assert.InRange(Part<TranslateTransform>(border).X, 30, 70);
        Frames.Advance(64, 16);
        Assert.Equal(100, Part<TranslateTransform>(border).X);

        Follow.SetTiming(border, Presto.Build().Duration(1000).Ease("linear"));
        Follow.SetX(border, 0);
        Frames.Advance(112, 16);
        Assert.InRange(Part<TranslateTransform>(border).X, 80, 95);
    }

    [AvaloniaFact]
    public void AnAnimationOfTheSameShorthandLayersOverTheFollowAndRevertsToIt()
    {
        var border = Shown();
        Follow.SetX(border, 30);

        var animation = border.Animate(new() { ["X"] = 90, Duration = 100, Ease = "linear" });
        Frames.Advance(48, 16);
        Assert.InRange(Part<TranslateTransform>(border).X, 40, 80);
        Follow.SetX(border, 10);
        Frames.Advance(200, 16);

        Assert.Equal(90, Part<TranslateTransform>(border).X);
        animation.Revert();
        Assert.Equal(10, Part<TranslateTransform>(border).X);
        Follow.SetX(border, 60);
        Frames.Advance(200, 16);
        Assert.Equal(60, Part<TranslateTransform>(border).X);
    }

    [AvaloniaFact]
    public void AnAnimatedBlurLayersOverTheFollowedBlurAndRevertsToIt()
    {
        var border = Shown();
        Follow.SetBlur(border, 4);

        var animation = border.Animate(new() { ["Blur"] = 12, Duration = 100, Ease = "linear" });
        Frames.Advance(48, 16);
        Assert.InRange(Assert.IsType<BlurEffect>(border.Effect).Radius, 5, 11);
        Frames.Advance(200, 16);
        animation.Revert();

        Assert.Equal(4, Assert.IsType<BlurEffect>(border.Effect).Radius);
    }

    [AvaloniaFact]
    public void ClearingRestoresTheIdentityAndTheVisualsOwnRenderTransform()
    {
        var border = Shown();
        var own = new RotateTransform(15);
        border.RenderTransform = own;
        Follow.SetX(border, 0);
        Follow.SetBlur(border, 0);
        Follow.SetX(border, 40);
        Follow.SetBlur(border, 6);
        Frames.Advance(48, 16);

        border.ClearValue(Follow.XProperty);
        border.ClearValue(Follow.BlurProperty);
        Frames.Advance(16);

        Assert.Same(own, border.RenderTransform);
        Assert.Equal(15, own.Angle);
        Assert.Null(border.Effect);
        Assert.Null(Presto.Engine._head);
    }

    [AvaloniaFact]
    public void ReducedMotionSnaps()
    {
        Presto.Engine.Defaults.ReducedMotion = true;
        var border = Shown();
        Follow.SetX(border, 0);

        Follow.SetX(border, 100);

        Assert.Equal(100, Part<TranslateTransform>(border).X);
    }

    [AvaloniaFact]
    public void DrawTracksAMotionValueAndReleasingRestoresTheDashes()
    {
        var path = new Path { Data = Geometry.Parse("M0,0 L100,0"), Stroke = Brushes.Black, StrokeThickness = 2 };
        Show(path);
        var progress = Presto.CreateMotionValue();

        Follow.SetDraw(path, progress);
        Assert.Equal(0, path.StrokeDashArray![0]);
        progress.Set(.25);
        Frames.Advance(16);
        Assert.Equal([0, .25], (double[])PrestoAvalonia.Get(path, "Draw")!);
        Assert.Equal(25 / 2d, path.StrokeDashArray![0], 3);
        progress.Set(1.5);
        Frames.Advance(16);
        Assert.Equal([0, 1], (double[])PrestoAvalonia.Get(path, "Draw")!);

        path.ClearValue(Follow.DrawProperty);
        Assert.Null(path.StrokeDashArray);
    }

    [AvaloniaFact]
    public void DrawEasesToANumber()
    {
        var path = new Path { Data = Geometry.Parse("M0,0 L100,0"), Stroke = Brushes.Black, StrokeThickness = 2 };
        Follow.SetTiming(path, Presto.Build().Duration(100).Ease("linear"));
        Show(path);
        Follow.SetDraw(path, 0);

        Follow.SetDraw(path, 1);
        Frames.Advance(48, 16);
        Assert.InRange(((double[])PrestoAvalonia.Get(path, "Draw")!)[1], .2, .8);
        Frames.Advance(100, 16);
        Assert.Equal([0, 1], (double[])PrestoAvalonia.Get(path, "Draw")!);
    }

    [AvaloniaFact]
    public void ADataContextSwapSnaps()
    {
        var border = Shown();
        border.DataContext = new LevelViewModel();
        border.Bind(Follow.YProperty, new Binding(nameof(LevelViewModel.Level)));

        border.DataContext = new LevelViewModel { Level = 90 };

        Assert.Equal(90, Part<TranslateTransform>(border).Y);
    }
}
