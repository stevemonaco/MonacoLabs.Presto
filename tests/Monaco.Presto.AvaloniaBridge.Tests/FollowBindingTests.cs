using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Diagnostics;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge.Bindings;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>Value-driven bindings through <see cref="Follow.Bind"/> (GAPS-PLAN §1.5).</summary>
public sealed class FollowBindingTests : BridgeTestBase
{
    private static readonly FollowParams Linear100 = new() { Duration = 100, Ease = "linear" };

    private static Border Unsized() => new() { Height = 4, HorizontalAlignment = HorizontalAlignment.Left };

    private static Binding Level() => new(nameof(LevelViewModel.Level));

    private static Border Styled(LevelViewModel model)
    {
        var border = Unsized();
        border.Styles.Add(new Style(x => x.OfType<Border>()) { Setters = { new Setter(Layoutable.WidthProperty, 7d) } });
        border.DataContext = model;
        Show(border);
        return border;
    }

    private static (Border Border, LevelViewModel Model, IDisposable Binding) ShowBound(FollowParams? options = null)
    {
        var model = new LevelViewModel();
        var border = Unsized();
        border.DataContext = model;
        Show(new StackPanel { Children = { border } });
        return (border, model, Follow.Bind(border, Layoutable.WidthProperty, Level(), options ?? Linear100));
    }

    [AvaloniaFact]
    public void TheFirstValueSnapsAtLocalValue()
    {
        var (border, _, _) = ShowBound();

        Assert.Equal(20, border.Width);
        Assert.Equal(BindingPriority.LocalValue, border.GetDiagnostic(Layoutable.WidthProperty).Priority);
    }

    [AvaloniaFact]
    public void AViewModelChangeAnimatesAndLandsExactly()
    {
        var (border, model, _) = ShowBound();

        model.Level = 120.3;
        Frames.Advance(48, 16);

        Assert.InRange(border.Width, 30, 110);
        Frames.Advance(100, 16);
        Assert.Equal(120.3, border.Width);
    }

    [AvaloniaFact]
    public void AMidFlightRetargetKeepsTheVelocity()
    {
        var (border, model, _) = ShowBound(new());
        model.Level = 120;
        Frames.Advance(80, 5);

        // Resume rebases the start 12 ms back, as upstream does, so the frame after a retarget covers 17 ms.
        var previous = border.Width;
        Frames.Advance(5);
        var current = border.Width;
        model.Level = 140;
        Frames.Advance(5);
        var (before, after) = ((current - previous) / 5, (border.Width - current) / 17);

        Assert.True(before > .2, $"moving {before} per ms before the retarget");
        Assert.Equal(before, after, before * .1);
    }

    [AvaloniaFact]
    public void ADataContextSwapSnapsAndAnUnchangedValueStillLetsTheNextChangeAnimate()
    {
        var (border, _, _) = ShowBound();

        border.DataContext = new LevelViewModel { Level = 90 };
        Assert.Equal(90, border.Width);

        var same = new LevelViewModel { Level = 90 };
        border.DataContext = same;
        Dispatcher.UIThread.RunJobs();
        same.Level = 10;
        Assert.Equal(90, border.Width);
    }

    [AvaloniaFact]
    public void ARecycledContainerInAVirtualizedListSnapsToItsNewItem()
    {
        var items = Enumerable.Range(0, 50).Select(i => new LevelViewModel { Level = 2 * i }).ToList();
        var list = new ListBox
        {
            Height = 100,
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<LevelViewModel>((_, _) =>
            {
                var row = Unsized();
                Follow.Bind(row, Layoutable.WidthProperty, Level(), Linear100);
                return row;
            }),
        };
        var window = Show(list);

        for (var y = 40; y <= 400; y += 40)
        {
            list.Scroll!.Offset = new Vector(0, y);
            window.UpdateLayout();
        }

        var rows = list.GetVisualDescendants().OfType<Border>().Where(border => border.Height == 4).ToList();
        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Equal(((LevelViewModel)row.DataContext!).Level, row.Width));
    }

    [AvaloniaFact]
    public void ValuesThatCannotBeInterpolatedPassThrough()
    {
        var border = Unsized();
        var alignment = new Pushed<HorizontalAlignment>(HorizontalAlignment.Left);
        Follow.Bind(border, Layoutable.HorizontalAlignmentProperty, alignment, Linear100);

        alignment.Push(HorizontalAlignment.Right);

        Assert.Equal(HorizontalAlignment.Right, border.HorizontalAlignment);
    }

    [AvaloniaFact]
    public void AWriteBackModeThrows()
    {
        var border = Unsized();

        Assert.Throws<ArgumentException>(() => Follow.Bind(border, Layoutable.WidthProperty, new Binding("Level") { Mode = BindingMode.TwoWay }));
        Assert.Throws<ArgumentException>(() => Follow.Bind(border, Layoutable.WidthProperty, new MultiBinding { Mode = BindingMode.OneWayToSource }));
    }

    [AvaloniaFact]
    public void ReducedMotionSnaps()
    {
        Presto.Engine.Defaults.ReducedMotion = true;
        var (border, model, _) = ShowBound();

        model.Level = 100;

        Assert.Equal(100, border.Width);
    }

    [AvaloniaFact]
    public void DetachLandsOnTheLatestTargetAndReattachSnaps()
    {
        var (border, model, _) = ShowBound();
        var panel = (StackPanel)border.Parent!;
        model.Level = 100;
        Frames.Advance(32, 16);

        panel.Children.Remove(border);
        Assert.Equal(100, border.Width);
        model.Level = 40;
        panel.Children.Add(border);
        Frames.Advance(16);

        Assert.Equal(40, border.Width);
        Assert.Null(Presto.Engine._head);
    }

    [AvaloniaFact]
    public void AStyleValueAtALowerPriorityIsOverridden()
    {
        var border = Styled(new LevelViewModel());

        Follow.Bind(border, Layoutable.WidthProperty, Level());

        Assert.Equal(20, border.Width);
    }

    [AvaloniaFact]
    public void AnAnimationOfTheSamePropertyLayersOverTheFollowAndRevertsToIt()
    {
        var (border, model, _) = ShowBound();

        var animation = border.Animate(new() { ["Width"] = 90, Duration = 100, Ease = "linear" });
        Frames.Advance(50, 16);
        model.Level = 30;
        Frames.Advance(200, 16);

        Assert.Equal(90, border.Width);
        animation.Revert();
        Assert.Equal(30, border.Width);
        model.Level = 60;
        Frames.Advance(200, 16);
        Assert.Equal(60, border.Width);
    }

    [AvaloniaFact]
    public void ARelativeSourceAncestorResolvesOnTheTarget()
    {
        var ancestor = Unsized();
        var panel = new StackPanel { MinWidth = 33, Children = { ancestor } };
        Show(panel);

        Follow.Bind(ancestor, Layoutable.WidthProperty, new Binding(nameof(StackPanel.MinWidth)) { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(StackPanel) } });

        Assert.Equal(33, ancestor.Width);
        panel.MinWidth = 60;
        Assert.Equal(33, ancestor.Width);
        Frames.Advance(2000, 16);
        Assert.Equal(60, ancestor.Width);
    }

    [AvaloniaFact]
    public void AnimateInitialEasesFromThePreBindingValue()
    {
        var model = new LevelViewModel();
        var border = Unsized();
        border.Width = 5;
        border.DataContext = model;
        Show(border);

        Follow.Bind(border, Layoutable.WidthProperty, Level(), Linear100 with { AnimateInitial = true });
        Frames.Advance(48, 16);

        Assert.InRange(border.Width, 5.01, 19.99);
        Frames.Advance(100, 16);
        Assert.Equal(20, border.Width);
    }

    [AvaloniaFact]
    public void AnimateInitialSnapsWhenThePreBindingValueIsUnset()
    {
        var (border, _, _) = ShowBound(Linear100 with { AnimateInitial = true });

        Assert.Equal(20, border.Width);
    }

    [AvaloniaFact]
    public void ColourBrushThicknessAndCornerRadiusAnimate()
    {
        var border = Unsized();
        var brush = new SolidColorBrush(Colors.Red);
        var color = new Pushed<Color>(Colors.Red);
        var background = new Pushed<IBrush?>(Brushes.Red);
        var thickness = new Pushed<Thickness>(new Thickness(0));
        var radius = new Pushed<CornerRadius>(new CornerRadius(0));
        Follow.Bind(brush, SolidColorBrush.ColorProperty, color);
        Follow.Bind(border, Border.BackgroundProperty, background);
        Follow.Bind(border, Border.BorderThicknessProperty, thickness);
        Follow.Bind(border, Border.CornerRadiusProperty, radius);

        color.Push(Colors.Blue);
        background.Push(Brushes.Blue);
        thickness.Push(new Thickness(10));
        radius.Push(new CornerRadius(8));
        Frames.Advance(64, 16);

        Assert.NotEqual(Colors.Blue, brush.Color);
        Assert.NotEqual(Colors.Blue, ((ISolidColorBrush)border.Background!).Color);
        Assert.InRange(border.BorderThickness.Left, 0.01, 9.99);
        Assert.InRange(border.CornerRadius.TopLeft, 0.01, 7.99);
        Frames.Advance(2000, 16);
        Assert.Equal(Colors.Blue, brush.Color);
        Assert.Equal(Colors.Blue, ((ISolidColorBrush)border.Background!).Color);
        Assert.Equal(new Thickness(10), border.BorderThickness);
        Assert.Equal(new CornerRadius(8), border.CornerRadius);
    }

    [AvaloniaFact]
    public void DisposingRestoresThePropertyAndStopsTicking()
    {
        var model = new LevelViewModel();
        var border = Styled(model);
        var binding = Follow.Bind(border, Layoutable.WidthProperty, Level(), Linear100);
        model.Level = 100;
        Frames.Advance(32, 16);

        binding.Dispose();
        Frames.Advance(16);

        Assert.Equal(7, border.Width);
        Assert.Null(Presto.Engine._head);
        Assert.Null(border.GetValue(CompanionProperties.For(Layoutable.WidthProperty)));
        model.Level = 50;
        Assert.Equal(7, border.Width);
    }

    private sealed class Pushed<T>(T value) : IObservable<T>, IDisposable
    {
        private IObserver<T>? _observer;

        public void Push(T next) => _observer?.OnNext(next);

        public IDisposable Subscribe(IObserver<T> observer)
        {
            _observer = observer;
            observer.OnNext(value);
            return this;
        }

        public void Dispose() => _observer = null;
    }
}
