using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using MonacoLabs.Presto;
using PrestoGallery.Pages;
using PrestoGallery.Showcase;
using PrestoGallery.ViewModels;
using PrestoGallery.Views;

namespace PrestoGallery.Tests;

/// <summary>The Springs page.</summary>
public sealed class SpringsPageTests
{
    private const double RaceDistance = 300;

    [AvaloniaFact]
    public void MovingBounceRederivesStiffnessDampingAndTheRatio()
    {
        var page = new SpringsPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var stiffness = Slider(page, "StiffnessSlider").Value;
        var damping = Slider(page, "DampingSlider").Value;

        Slider(page, "BounceSlider").Value = -.5;

        Assert.NotEqual(stiffness, Slider(page, "StiffnessSlider").Value);
        Assert.NotEqual(damping, Slider(page, "DampingSlider").Value);
        Assert.EndsWith("overdamped", Text(page, "RatioText"));
        Assert.StartsWith("var spring = Eases.Spring(bounce: -0.5, duration: 628", page.GetControl<CodePanel>("LabCode").Code);

        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void MovingDampingRederivesBounceAndPrintsThePhysicalCall()
    {
        var page = new SpringsPage();
        var window = SceneHarness.Show(page, 1280, 900);

        Slider(page, "DampingSlider").Value = 20;

        Assert.Equal(0, Slider(page, "BounceSlider").Value, 3);
        Assert.Contains("critically damped", Text(page, "RatioText"));
        Assert.StartsWith("var spring = Eases.Spring(mass: 1, stiffness: 100, damping: 20, velocity: 0);", page.GetControl<CodePanel>("LabCode").Code);

        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheUnderdampedDotOvershootsAndTheCriticalOneArrivesBeforeTheOverdampedOne()
    {
        var page = new SpringsPage();
        var window = SceneHarness.Show(page, 1280, 900);
        Border[] dots = [page.GetControl<Border>("UnderDot"), page.GetControl<Border>("CriticalDot"), page.GetControl<Border>("OverDot")];
        var furthest = new double[3];
        var arrived = new int?[3];

        var race = page.GetControl<Button>("RaceButton");
        race.BringIntoView();
        TestFrames.Tick();
        SceneHarness.Click(window, race);
        for (var frame = 0; frame < 240; frame++)
        {
            for (var i = 0; i < dots.Length; i++)
            {
                var x = (double)Presto.Get(dots[i], "X")!;
                furthest[i] = Math.Max(furthest[i], x);
                if (arrived[i] == null && x > RaceDistance - 1) arrived[i] = frame;
            }
            TestFrames.Tick();
        }

        Assert.True(furthest[0] > RaceDistance + 50, $"underdamped peaked at {furthest[0]:0.#}");
        Assert.True(furthest[1] <= RaceDistance + .01, $"critically damped peaked at {furthest[1]:0.###}");
        Assert.True(furthest[2] <= RaceDistance + .01, $"overdamped peaked at {furthest[2]:0.###}");
        Assert.NotNull(arrived[2]);
        Assert.True(arrived[1] < arrived[2], $"critically damped arrived on frame {arrived[1]}, overdamped on {arrived[2]}");

        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ASecondClickKeepsTheFollowersVelocityWhileTheTweensJumps()
    {
        var page = new SpringsPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var stage = page.GetControl<Canvas>("VelocityStage");
        stage.BringIntoView();
        TestFrames.Tick();

        ClickStage(window, stage, new Point(600, 110));
        TestFrames.Tick(15);
        var before = page.HorizontalVelocity;
        ClickStage(window, stage, new Point(40, 110));
        var after = new (double Tween, double Spring)[4];
        for (var frame = 0; frame < after.Length; frame++)
        {
            TestFrames.Tick();
            after[frame] = page.HorizontalVelocity;
        }
        var springJump = Math.Abs(after[0].Spring - before.Spring);
        for (var frame = 1; frame < after.Length; frame++) springJump = Math.Max(springJump, Math.Abs(after[frame].Spring - after[frame - 1].Spring));

        Assert.True(before.Tween > .3 && before.Spring > .3, $"mid-flight the tween moved at {before.Tween:0.###} and the spring at {before.Spring:0.###} px/ms");
        Assert.True(after[0].Tween < -1 || after[1].Tween < -1, $"the tween went from {before.Tween:0.###} to {after[0].Tween:0.###}, {after[1].Tween:0.###} px/ms");
        Assert.True(after[0].Spring > 0 && after[1].Spring > 0, $"the spring flipped from {before.Spring:0.###} to {after[0].Spring:0.###}, {after[1].Spring:0.###} px/ms");
        Assert.True(springJump < .5, $"the spring's velocity jumped {springJump:0.###} px/ms in a frame");

        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void AThrownPuckOvershootsTheRestPointInTheThrowDirectionThenSettlesOnIt()
    {
        var page = new SpringsPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var puck = page.GetControl<Border>("ThrowPuck");
        puck.BringIntoView();
        TestFrames.Tick();
        var grab = SceneHarness.Center(puck, window);

        window.MouseDown(grab, MouseButton.Left);
        for (var i = 1; i <= 10; i++)
        {
            window.MouseMove(grab - new Vector(15 * i, 0));
            TestFrames.Tick();
        }
        TestFrames.Tick(30);
        for (var i = 1; i <= 4; i++)
        {
            window.MouseMove(grab + new Vector(-150 + (25 * i), 0));
            TestFrames.Tick();
        }
        var released = X(puck);
        window.MouseUp(grab - new Vector(50, 0), MouseButton.Left);
        var peak = double.NegativeInfinity;
        for (var frame = 0; frame < 180; frame++)
        {
            TestFrames.Tick();
            peak = Math.Max(peak, X(puck));
        }

        var arrow = page.GetControl<Polyline>("ThrowArrow").Points;
        Assert.True(released < -60, $"the chase had the puck at {released:0.#} on release");
        Assert.True(arrow[1].X - arrow[0].X > 60, $"the arrow ran from {arrow[0]} to {arrow[1]}");
        Assert.True(peak > -released * .5, $"released at {released:0.#}, the puck peaked at {peak:0.#} past the rest point");
        Assert.Equal(0, X(puck), 1);
        Assert.Equal(0, (double)Presto.Get(puck, "Y")!, 1);

        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void MovingTheFollowerRowsTargetMidFlightKeepsEveryDotsMotionContinuous()
    {
        var page = new SpringsPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var slider = page.GetControl<Slider>("FollowerTargetSlider");
        slider.BringIntoView();
        TestFrames.Tick();
        var dots = page.GetControl<StackPanel>("FollowerRow").Children;

        slider.Value = 150;
        TestFrames.Tick(8);
        var previous = dots.Select(Y).ToArray();
        var steps = new double[dots.Count][];
        for (var i = 0; i < dots.Count; i++) steps[i] = new double[6];
        for (var frame = 0; frame < 6; frame++)
        {
            if (frame == 3) slider.Value = 20;
            TestFrames.Tick();
            for (var i = 0; i < dots.Count; i++)
            {
                var y = Y(dots[i]);
                steps[i][frame] = y - previous[i];
                previous[i] = y;
            }
        }

        foreach (var step in steps)
        {
            Assert.True(step[2] > 1 && step[3] > 1, $"a dot moved {step[2]:0.##} px before the retarget and {step[3]:0.##} px after it");
            for (var frame = 1; frame < step.Length; frame++)
                Assert.True(Math.Abs(step[frame] - step[frame - 1]) < 5, $"a dot's step went from {step[frame - 1]:0.##} to {step[frame]:0.##} px on frame {frame}");
        }
        Assert.Equal(42, page.GetControl<Line>("FollowerTargetLine").StartPoint.Y, 3);

        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void EachChainLinkLagsTheOneAheadOfIt()
    {
        var page = new SpringsPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var stage = page.GetControl<Canvas>("ChainStage");
        stage.BringIntoView();
        TestFrames.Tick();
        var links = stage.Children.Reverse().ToArray();

        window.MouseMove(stage.TranslatePoint(new Point(600, 40), window)!.Value);
        TestFrames.Tick(30);
        var midway = links.Select(X).ToArray();

        for (var i = 1; i < links.Length; i++)
            Assert.True(midway[i] < midway[i - 1], $"link {i} was at {midway[i]:0.###}, the one ahead at {midway[i - 1]:0.###}");

        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void SwitchingTheChainPickerRetunesEveryFollowersSpring()
    {
        var page = new SpringsPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var followers = page.GetControl<Canvas>("ChainStage").Children
            .SelectMany(link => new[] { Follow.GetX(link), Follow.GetY(link) })
            .Select(value => ((MotionValue)value!).FollowSpring!)
            .ToArray();
        var picker = page.GetControl<ComboBox>("ChainPicker");
        var rope = followers[0].Stiffness;

        picker.SelectedIndex = 1;
        Assert.All(followers, spring => Assert.True(spring.DampingRatio < 1, $"the jelly's ratio is {spring.DampingRatio:0.###}"));

        picker.SelectedIndex = 2;
        Assert.All(followers, spring =>
        {
            Assert.Equal(1, spring.DampingRatio, 2);
            Assert.True(spring.Stiffness > rope * 10, $"the rod's stiffness {spring.Stiffness:0.#} against the rope's {rope:0.#}");
        });

        SceneHarness.Close(window);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryStageComesToRestWhereItWasSentUnderFullAndReducedMotion(bool reduced)
    {
        TestFrames.Install();
        using var motion = reduced ? ReducedMotion.On() : null;
        var page = new SpringsPage();
        var window = SceneHarness.Show(page, 1280, 900);

        Slider(page, "BounceSlider").Value = .3;
        SceneHarness.Click(window, page.GetControl<Button>("DropButton"));
        SceneHarness.Click(window, Reach(page.GetControl<Button>("RaceButton")));
        ClickStage(window, Reach(page.GetControl<Canvas>("VelocityStage")), new Point(600, 110));

        var puck = Reach(page.GetControl<Border>("ThrowPuck"));
        var grab = SceneHarness.Center(puck, window);
        window.MouseDown(grab, MouseButton.Left);
        for (var i = 1; i <= 6; i++)
        {
            window.MouseMove(grab + new Vector(20 * i, 10 * i));
            TestFrames.Tick();
        }
        window.MouseUp(grab + new Vector(120, 60), MouseButton.Left);

        SceneHarness.Click(window, Reach(page.GetControl<Button>("EaseRowButton")));
        Slider(page, "FollowerTargetSlider").Value = 100;

        SceneHarness.Click(window, Reach(page.GetControl<Border>("ToggleTrack")));
        SceneHarness.Click(window, page.GetControl<StackPanel>("Tabs").Children[2]);
        SceneHarness.Click(window, page.GetControl<Button>("BadgeButton"));
        SceneHarness.Click(window, page.GetControl<Button>("SheetButton"));

        page.GetControl<ComboBox>("ChainPicker").SelectedIndex = 1;
        var chain = Reach(page.GetControl<Canvas>("ChainStage"));
        window.MouseMove(chain.TranslatePoint(new Point(600, 40), window)!.Value);
        TestFrames.Tick(400);

        Assert.Equal(120, Y(page.GetControl<Border>("Weight")), 1);
        foreach (var dot in new[] { "UnderDot", "CriticalDot", "OverDot" }) Assert.Equal(RaceDistance, X(page.GetControl<Border>(dot)), 1);
        Assert.Equal(280, X(page.GetControl<Border>("TweenPuck")), 1);
        Assert.Equal(280, ((MotionValue)Follow.GetX(page.GetControl<Border>("SpringPuck"))!).Value, 1);
        Assert.Equal(0, X(puck), 1);
        Assert.Equal(0, Y(puck), 1);
        Assert.All(page.GetControl<StackPanel>("EaseRow").Children, dot => Assert.Equal(150, Y(dot), 1));
        Assert.All(page.GetControl<StackPanel>("FollowerRow").Children, dot => Assert.Equal(100, Y(dot), 1));
        Assert.Equal(26, X(page.GetControl<Border>("ToggleThumb")), 1);
        Assert.Equal(128, X(page.GetControl<Border>("TabIndicator")), 1);
        Assert.Equal(1, (double)Presto.Get(page.GetControl<Border>("Badge"), "Scale")!, 2);
        Assert.Equal("2", Text(page, "BadgeCount"));
        Assert.Equal(50,Canvas.GetTop(page.GetControl<Border>("Sheet")), 1);
        Assert.Equal(.5, page.GetControl<Border>("Scrim").Opacity, 2);
        Assert.All(chain.Children, link =>
        {
            Assert.Equal(600, X(link), 0);
            Assert.Equal(40, Y(link), 0);
        });

        SceneHarness.Close(window);
    }

    [AvaloniaTheory]
    [InlineData("Eases", typeof(EasesPage))]
    [InlineData("Interruption lab", typeof(PrestoGallery.Showcase.Pages.InterruptionPage))]
    [InlineData("Animatable", typeof(AnimatablePage))]
    [InlineData("Draggable", typeof(DraggablePage))]
    public void TheCrossLinkOnARelatedPageOpensSprings(string title, Type type)
    {
        var gallery = new MainViewModel();
        var window = SceneHarness.Show(new MainView { DataContext = gallery }, 1400, 1000);
        try
        {
            gallery.SelectedPage = gallery.Sections.SelectMany(section => section.Pages).First(page => page.Title == title);
            TestFrames.Tick(60);
            var page = window.GetLogicalDescendants().OfType<ShowcasePage>().Single(page => page.GetType() == type);
            if (page is EasesPage)
            {
                var line = page.GetControl<StackPanel>("SpringsLine");
                Assert.False(line.IsVisible);
                while (!line.IsVisible) page.GetControl<ComboBox>("EaseBox").SelectedIndex++;
            }
            var link = Reach(page.GetLogicalDescendants().OfType<HyperlinkButton>().Single(button => Equals(button.Content, "Springs")));

            SceneHarness.Click(window, link);

            Assert.Equal("Springs", gallery.SelectedPage?.Title);
        }
        finally
        {
            SceneHarness.Close(window);
        }
    }

    private static T Reach<T>(T control) where T : Control
    {
        control.BringIntoView();
        TestFrames.Tick();
        return control;
    }

    private static double X(Visual visual) => (double)Presto.Get(visual, "X")!;

    private static double Y(Visual visual) => (double)Presto.Get(visual, "Y")!;

    private static void ClickStage(Window window, Canvas stage, Point point)
    {
        var at = stage.TranslatePoint(point, window)!.Value;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
    }

    private static Slider Slider(SpringsPage page, string name) => page.GetControl<Slider>(name);

    private static string Text(SpringsPage page, string name) => page.GetControl<TextBlock>(name).Text!;
}
