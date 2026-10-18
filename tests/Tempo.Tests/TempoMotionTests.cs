using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Controls;
using Tempo.Model;
using Tempo.Motion;
using Tempo.ViewModels;
using Tempo.Views;
using PrestoMotion = MonacoLabs.Presto.Motion;

namespace Tempo.Tests;

public sealed class TempoMotionTests : TempoTestBase
{
    [AvaloniaFact]
    public void EveryBuilderHasAReducedForm()
    {
        var fields = typeof(TempoMotion).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.FieldType == typeof(AnimationBuilder)).ToList();

        Assert.NotEmpty(fields);
        Assert.All(fields, field => Assert.True(Reduced((AnimationBuilder)field.GetValue(null)!) != null, field.Name));
    }

    [AvaloniaFact]
    public void EveryStateValidatesInBothMotionModes()
    {
        // A state is checked against the type it lands on, since its properties need a rest there.
        List<(Type Target, MotionStates States)> lists = [.. StyleStates(Application.Current!.Styles)];
        Assert.NotEmpty(lists);
        lists.Add((typeof(Avalonia.Controls.Shapes.Path), [new MotionState { When = "IsRunning", Motion = TempoMotion.IconToPause }]));
        lists.Add((typeof(ProgressRing), [new MotionState { When = "IsRunning", Motion = TempoMotion.RingBreathe }]));

        foreach (var (target, states) in lists)
            PrestoMotion.SetStates((Control)Activator.CreateInstance(target)!, states);
        using (ReducedMotion.On())
        {
            foreach (var (target, states) in lists)
                PrestoMotion.SetStates((Control)Activator.CreateInstance(target)!, states);
        }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheModeRecolorsBackgroundForegroundAndRingFillInBothMotionModes(bool reduced)
    {
        TestFrames.Install();
        using var motion = reduced ? ReducedMotion.On() : null;
        var dot = new Border { Classes = { "accent-bg" } };
        var label = new TextBlock { Classes = { "accent-fg" } };
        var ring = new ProgressRing { Classes = { "accent" } };
        var root = new StackPanel { Children = { dot, label, ring } };
        PrestoMotion.SetState(root, FocusMode.Focus);
        var window = new Window { Content = root };
        window.Show();
        TestFrames.Tick();

        PrestoMotion.SetState(root, FocusMode.ShortBreak);
        TestFrames.Tick(60);

        Assert.Equal(TempoMotion.ShortBreakAccent, Color(dot.Background));
        Assert.Equal(TempoMotion.ShortBreakAccent, Color(label.Foreground));
        Assert.Equal(TempoMotion.ShortBreakAccent, Color(ring.Fill));
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void IconToPauseMorphsThePlayIconAsAStateAndReturnsToIt(bool reduced)
    {
        TestFrames.Install();
        using var motion = reduced ? ReducedMotion.On() : null;
        Assert.True(Application.Current!.TryGetResource("PlayIcon", null, out var play));
        var path = new Avalonia.Controls.Shapes.Path { Data = (Geometry)play! };
        PrestoMotion.SetStates(path, [new MotionState { When = "IsRunning", Motion = TempoMotion.IconToPause }]);
        var window = new Window { Content = path };
        window.Show();
        TestFrames.Tick();

        PrestoMotion.SetState(path, "IsRunning");
        TestFrames.Tick(30);
        var paused = path.Data!.Bounds;
        PrestoMotion.SetState(path, null);
        TestFrames.Tick(30);

        Assert.Equal(TempoMotion.PauseIcon.Bounds.Left, paused.Left, 1);
        Assert.Equal(TempoMotion.PauseIcon.Bounds.Right, paused.Right, 1);
        Assert.Same(play, path.Data);
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheShellAttachesTakesKeysAndFollowsTheModeInBothMotionModes(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced);
        var backdrop = window.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("backdrop"));

        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        Assert.True(vm.Focus.IsRunning);
        window.KeyPress(Key.OemComma, RawInputModifiers.Control, PhysicalKey.Comma, ",");
        Assert.Single(window.GetVisualDescendants().OfType<SettingsFlyout>());
        vm.Focus.SkipCommand.Execute(null);
        TestFrames.Tick(60);

        Assert.Equal(FocusMode.ShortBreak, vm.Focus.Mode);
        Assert.Equal(TempoMotion.ShortBreakAccent, Color(backdrop.Background));
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheBackdropBlobsDriftOnTheCompositorLeanAndSwellTowardsThePointerAndHoldStillWhenReduced(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced);
        var blobs = window.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("blob")).ToList();
        var drift = TempoMotion.BlobDrift.ToParams();

        Assert.Equal(4, blobs.Count);
        Assert.True(PrestoAvalonia.CanAnimateOnCompositor(TempoMotion.BlobDrift, out var reason), reason);
        Assert.Equal(!reduced, (reduced ? drift.WhenReduced! : drift).Loop!.Value.IsInfinite);

        window.MouseMove(new Point(200, 200));
        TestFrames.Tick(150);
        var leaning = blobs.Count(b => Math.Abs(Convert.ToDouble(Presto.Get(b, "X", withUnit: false))) > .5);
        var swelling = blobs.Count(b => Convert.ToDouble(Presto.Get(b, "Scale", withUnit: false)) > 1.01 && b.Opacity > TempoMotion.BlobGlow(0));
        if (reduced)
        {
            Assert.Equal(0, leaning);
            Assert.Equal(0, swelling);
        }
        else
        {
            Assert.InRange(leaning, 1, 4);
            Assert.InRange(swelling, 1, 4);
        }
        window.Close();
        await vm.FlushAsync();
    }

    private static Color Color(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    private static IEnumerable<(Type Target, MotionStates States)> StyleStates(IEnumerable<IStyle> styles) => styles.SelectMany(style => style switch
    {
        StyleInclude include => StyleStates([include.Loaded]),
        Styles nested => StyleStates(nested),
        Style rule => rule.Setters.OfType<Setter>().Where(s => s.Property == PrestoMotion.StatesProperty)
            .Select(s => (Target(rule), (MotionStates)s.Value!))
            .Concat(StyleStates(rule.Children)),
        _ => [],
    });

    // An :is(Control) rule targets any control, so it is checked on one with a background and a foreground.
    private static Type Target(Style rule) =>
        rule.Selector is { } selector && TargetType(selector) is { } type && type != typeof(Control) ? type : typeof(TemplatedControl);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_TargetType")]
    private static extern Type? TargetType(Selector selector);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_reduced")]
    private static extern ref Action<AnimationBuilder>? Reduced(AnimationBuilder builder);
}
