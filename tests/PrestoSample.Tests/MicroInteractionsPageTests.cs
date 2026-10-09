using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Controls;
using PrestoSample.Builder;
using PrestoSample.Showcase.Pages;

namespace PrestoSample.Tests;

public sealed class MicroInteractionsPageTests
{
    [AvaloniaFact]
    public void ThePageRunsAndRevertsItsScopeOnDetach()
    {
        var page = new MicroInteractionsPage();
        var reverted = false;
        page.Scope.Add(_ => () => reverted = true);
        var window = SceneHarness.Show(page, 1280, 900);
        SceneHarness.Click(window, page.GetControl<Button>("Like"));
        TestFrames.Tick(60);

        SceneHarness.Close(window);

        Assert.True(reverted);
    }

    [AvaloniaFact]
    public void SubmitNarrowsTheButtonToACircleAndSpringsBackToItsLabelWidth()
    {
        var page = new MicroInteractionsPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var submit = page.GetControl<Button>("Submit");
        var width = submit.Width;

        SceneHarness.Click(window, submit);
        TestFrames.Tick(40);
        Assert.Equal(submit.Height, submit.Width, 1);

        TestFrames.Tick(200);
        Assert.Equal(width, submit.Width, 1);
        Assert.Equal(1, page.GetControl<TextBlock>("SubmitLabel").Opacity, 2);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void SaveMorphsItsLabelIntoACheckAndBack()
    {
        var page = new MicroInteractionsPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var label = page.GetControl<TextBlock>("SaveLabel");

        SceneHarness.Click(window, page.GetControl<Button>("Save"));
        TestFrames.Tick((600 + 700) / 16);
        SceneHarness.AssertPlain(label, "✓");

        TestFrames.Tick((1400 + 700) / 16 + 10);
        SceneHarness.AssertPlain(label, "Save");
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void NotifyAddsAToastThatDismissesItselfWhenItsTimerRunsOut()
    {
        var page = new MicroInteractionsPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var toasts = page.GetControl<ItemsControl>("Toasts");

        SceneHarness.Click(window, page.GetControl<Button>("Notify"));
        Assert.Single(toasts.Items);

        TestFrames.Tick(200);
        Assert.Single(toasts.Items);
        var bar = toasts.GetVisualDescendants().OfType<ProgressBar>().Single();
        Assert.InRange(bar.Value, 0.05, 0.4);

        TestFrames.Tick(90);
        Assert.Empty(toasts.Items);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void HoveringAToastHoldsItsCountdownUntilThePointerLeaves()
    {
        var page = new MicroInteractionsPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var toasts = page.GetControl<ItemsControl>("Toasts");
        SceneHarness.Click(window, page.GetControl<Button>("Notify"));
        TestFrames.Tick(40);
        var toast = toasts.GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("toast"));
        var bar = toast.GetVisualDescendants().OfType<ProgressBar>().Single();

        window.MouseMove(SceneHarness.Center(toast, window));
        var held = bar.Value;
        TestFrames.Tick(300);
        Assert.Equal(held, bar.Value, 6);
        Assert.Single(toasts.Items);

        window.MouseMove(new Point(1, 1));
        TestFrames.Tick(300);
        Assert.Empty(toasts.Items);
        SceneHarness.Close(window);
    }

    [AvaloniaTheory]
    [InlineData(1)]
    [InlineData(-1)]
    public void SwipingAToastPastHalfItsWidthSlidesItOutTowardsTheSwipeAndDismissesIt(int direction)
    {
        var page = new MicroInteractionsPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var toasts = page.GetControl<ToastHost>("Toasts");
        SceneHarness.Click(window, page.GetControl<Button>("Notify"));
        TestFrames.Tick(40);
        var container = toasts.ContainerFromIndex(0)!;
        var travel = direction * container.Bounds.Width * .7;
        var grab = SceneHarness.Center(container, window);

        window.MouseDown(grab, MouseButton.Left);
        for (var i = 1; i <= 12; i++)
        {
            window.MouseMove(grab.WithX(grab.X + (travel * i / 12)));
            TestFrames.Tick();
        }
        window.MouseUp(grab.WithX(grab.X + travel), MouseButton.Left);
        TestFrames.Tick(2);

        Assert.Equal(direction, Math.Sign(Convert.ToDouble(PrestoAvalonia.Get(container, "X"))));
        TestFrames.Tick(30);
        Assert.Empty(toasts.Items);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void SubscribingWithAnInvalidAddressShakesTheField()
    {
        var page = new MicroInteractionsPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var email = page.GetControl<TextBox>("Email");
        email.Text = "nobody";

        SceneHarness.Click(window, page.GetLogicalDescendants().OfType<Button>().First(button => Equals(button.Content, "Subscribe")));
        TestFrames.Tick(4);
        Assert.NotEqual(0, (double)PrestoAvalonia.Get(email, "X")!, 3);
        Assert.Contains("invalid", email.Classes);

        TestFrames.Tick(60);
        Assert.Equal(0, (double)PrestoAvalonia.Get(email, "X")!, 6);
        SceneHarness.Close(window);
    }

    [AvaloniaTheory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public void LikingBurstsParticlesUnlessMotionIsReducedWhenTheHeartFadesIn(bool reduced, int overlays)
    {
        var page = new MicroInteractionsPage();
        var window = SceneHarness.Show(page, 1280, 900);
        Presto.Defaults.ReducedMotion = reduced;
        try
        {
            SceneHarness.Click(window, page.GetControl<Button>("Like"));

            Assert.Equal(overlays, SceneHarness.ParticleOverlays(page).Length);
            if (reduced)
                Assert.NotEmpty(Presto.GetAnimations(page.GetControl<PathIcon>("Heart")));
        }
        finally
        {
            Presto.Defaults.ReducedMotion = false;
            SceneHarness.Close(window);
        }
    }

    [AvaloniaFact]
    public void PressingLikeScalesItInAndReleasingReturnsIt()
    {
        var page = new MicroInteractionsPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var like = page.GetControl<Button>("Like");
        var center = SceneHarness.Center(like, window);

        window.MouseDown(center, MouseButton.Left);
        TestFrames.Tick(20);
        Assert.Equal(.92, (double)PrestoAvalonia.Get(like, "Scale")!, 3);

        window.MouseUp(center, MouseButton.Left);
        TestFrames.Tick(60);
        Assert.Equal(1, (double)PrestoAvalonia.Get(like, "Scale")!, 3);
        SceneHarness.Close(window);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AMagneticButtonLeansTowardsANearbyPointerAndSpringsBackUnlessMotionIsReduced(bool reduced)
    {
        var page = new MicroInteractionsPage();
        var window = SceneHarness.Show(page, 1280, 1100);
        var magnet = page.GetControl<Button>("Magnet");
        Presto.Defaults.ReducedMotion = reduced;
        try
        {
            window.MouseMove(magnet.TranslatePoint(new Point(magnet.Bounds.Width + 10, magnet.Bounds.Height + 10), window)!.Value);
            TestFrames.Tick(60);
            Assert.Equal(!reduced, X(magnet) > 0 && Y(magnet) > 0);

            window.MouseMove(new Point(1, 1));
            TestFrames.Tick(60);
            Assert.Equal(0, X(magnet), 6);
            Assert.Equal(0, Y(magnet), 6);
        }
        finally
        {
            Presto.Defaults.ReducedMotion = false;
            SceneHarness.Close(window);
        }

        static double X(Control control) => Convert.ToDouble(PrestoAvalonia.Get(control, "X"));
        static double Y(Control control) => Convert.ToDouble(PrestoAvalonia.Get(control, "Y"));
    }

    [AvaloniaFact]
    public void PressingSubmitGrowsInkFromThePressPoint()
    {
        var page = new MicroInteractionsPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var submit = page.GetControl<Button>("Submit");
        var near = submit.TranslatePoint(new Point(12, 6), window)!.Value;
        var far = submit.TranslatePoint(new Point(submit.Bounds.Width - 12, 6), window)!.Value;

        window.MouseDown(near, MouseButton.Left);
        TestFrames.Tick(3);
        var frame = SceneHarness.Capture(window);

        Assert.NotEqual(Pixel(frame, far), Pixel(frame, near));
        SceneHarness.Close(window);

        static Color Pixel((byte[] Pixels, int Stride) frame, Point at)
        {
            var i = ((int)at.Y * frame.Stride) + ((int)at.X * 4);
            return Color.FromRgb(frame.Pixels[i], frame.Pixels[i + 1], frame.Pixels[i + 2]);
        }
    }
}
