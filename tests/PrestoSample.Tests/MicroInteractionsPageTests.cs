using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
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

        window.MouseMove(SceneHarness.Centre(toast, window));
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
    public void SwipingAToastPastTheThresholdDismissesItTowardsTheSwipe(int direction)
    {
        var page = new MicroInteractionsPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var toasts = page.GetControl<ItemsControl>("Toasts");
        SceneHarness.Click(window, page.GetControl<Button>("Notify"));
        TestFrames.Tick(40);
        var toast = toasts.GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("toast"));
        var model = Assert.IsType<ToastsViewModel>(toasts.DataContext);
        var left = toast.TranslatePoint(default, page)!.Value.X;
        var grab = SceneHarness.Centre(toast, window);

        window.MouseDown(grab, MouseButton.Left);
        for (var i = 1; i <= 12; i++)
        {
            window.MouseMove(grab.WithX(grab.X + (direction * i * 10)));
            TestFrames.Tick();
        }
        window.MouseUp(grab.WithX(grab.X + (direction * 120)), MouseButton.Left);
        TestFrames.Tick(6);

        Assert.Empty(model.Toasts);
        var ghost = OverlayLayer.GetOverlayLayer(page)!.GetVisualDescendants().OfType<Image>().Single();
        Assert.Equal(direction, Math.Sign(ghost.TranslatePoint(default, page)!.Value.X - left));
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
        var centre = SceneHarness.Centre(like, window);

        window.MouseDown(centre, MouseButton.Left);
        TestFrames.Tick(20);
        Assert.Equal(.92, (double)PrestoAvalonia.Get(like, "Scale")!, 3);

        window.MouseUp(centre, MouseButton.Left);
        TestFrames.Tick(60);
        Assert.Equal(1, (double)PrestoAvalonia.Get(like, "Scale")!, 3);
        SceneHarness.Close(window);
    }
}
