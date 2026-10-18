using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Controls;
using MonacoLabs.Presto.DevTools;
using MonacoLabs.Presto.Draggables;
using MonacoLabs.Presto.Lottie;
using MonacoLabs.Presto.Text;

namespace MonacoLabs.Presto.AotProbe;

internal static partial class Program
{
    private static void AnimatableSet()
    {
        var box = new Border();
        Show(box);
        var animatable = box.CreateAnimatable(new() { ["Opacity"] = 100 });

        animatable.Set("Opacity", .25);
        Advance(200);

        Check(box.Opacity == .25, $"the opacity stopped at {box.Opacity}");
    }

    private static void SyncTimer()
    {
        var timer = Presto.CreateTimer(new() { Duration = 100 });
        var timeline = Presto.CreateTimeline(new()).Sync(timer);

        Advance(200);

        Check(timeline.Completed && timer.CurrentTime == 100, $"the synced timer stopped at {timer.CurrentTime}");
    }

    private static void SyncAnimation()
    {
        var box = new Border();
        var animation = box.Animate(new() { ["Width"] = 50, Duration = 100 });
        var timeline = Presto.CreateTimeline(new()).Sync(animation);

        Advance(200);

        Check(timeline.Completed && box.Width == 50, $"the synced animation stopped at {box.Width}");
    }

    private static void CompositorAnimation()
    {
        var bar = new Border { Background = Brushes.Red, Width = 40, Height = 20 };
        var window = Show(new Canvas { Children = { bar } }, 100, 100);

        var animation = bar.AnimateOnCompositor(new() { ["X"] = 60, Duration = 200, Ease = "linear" });
        Advance(300);
        // The render thread reads its own clock, so the move on screen takes real time.
        for (var i = 0; i < 60 && At(window, 70, 10) != Colors.Red; i++)
        {
            Thread.Sleep(16);
            Render();
        }

        Check(animation.Completed && At(window, 10, 10) == Colors.White && At(window, 70, 10) == Colors.Red, "the bar never moved 60 to the right");
    }

    private static void LayoutTransition()
    {
        var spacer = new Border { Height = 10 };
        var bar = new Border { Background = Brushes.Red, Width = 40, Height = 20 };
        Show(new StackPanel { Children = { spacer, bar } });
        var transition = bar.AnimateLayout(200, Eases.None);

        spacer.Height += 40;
        Render();
        Advance(100);
        var halfway = transition.Progress;
        Advance(200);

        Check(halfway is > 0 and < 1 && transition.Completed, $"the layout transition was at {halfway} halfway");
    }

    private static void PagerTravel()
    {
        var pager = new Pager();
        foreach (var color in new[] { Colors.Red, Colors.Green, Colors.Blue }) pager.Slides.Add(new Border { Background = new SolidColorBrush(color) });
        Show(new Panel { Children = { pager } });

        pager.GoTo(2);
        Advance(3000);

        var strip = Convert.ToDouble(PrestoAvalonia.Get(pager.GetVisualChildren().Single(), "X"));
        Check(Math.Abs(pager.Position.Value - 2) < 1e-3 && Math.Abs(strip + 400) < 1, $"the pager stopped at {pager.Position.Value}, strip {strip}");
    }

    private static void Draggable()
    {
        var target = new Border { Width = 50, Height = 50, Background = Brushes.Red };
        var container = new Canvas
        {
            Width = 200, Height = 200, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Children = { target },
        };
        var window = Show(container, 300, 300);
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams { Container = container, Y = new DraggableAxisParams { Enabled = false } });

        Presto.Animate(draggable, new AnimationParams { ["X"] = 30, Duration = 100 });
        Advance(200);
        Check(draggable.X == 30, $"X animated by name stopped at {draggable.X}");

        // Thrown past the container's edge, so the release overshoots and springs back.
        window.MouseDown(new Point(55, 25), MouseButton.Left);
        for (var step = 1; step <= 10; step++)
        {
            window.MouseMove(new Point(55 + (step * 20), 25));
            s_frames.Advance(20);
        }
        window.MouseUp(new Point(255, 25), MouseButton.Left);
        Advance(3000);

        Check(draggable.X == 150, $"the throw settled at {draggable.X}");
    }

    private static void DraggableAutoScroll()
    {
        var target = new Border { Width = 50, Height = 50, Background = Brushes.Red };
        var scroller = new ScrollViewer
        {
            Width = 200, Height = 200, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
            Content = new Canvas { Width = 200, Height = 1000, Children = { target } },
        };
        var window = Show(scroller, 300, 300);
        PrestoAvalonia.CreateDraggable(target);

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        window.MouseMove(new Point(25, 195));
        Advance(200);
        window.MouseUp(new Point(25, 195), MouseButton.Left);

        Check(scroller.Offset.Y > 0, "the pointer sat in the bottom band but nothing scrolled");
    }

    private static void ContentSize()
    {
        var inner = new Border { Height = 20 };
        var host = new Border { Background = Brushes.Red, Child = new StackPanel { Children = { inner } }, VerticalAlignment = VerticalAlignment.Top };
        Show(host);
        host.AnimateContentSize(100, "linear");

        inner.Height = 60;
        Render();
        Advance(48);
        var halfway = host.Bounds.Height;
        Advance(100);

        Check(halfway is > 25 and < 55 && host.Bounds.Height == 60, $"the host was {halfway} halfway and ended at {host.Bounds.Height}");
    }

    private static void SelectionGlide()
    {
        var indicator = new Border { Background = Brushes.Red };
        var list = new ListBox { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        for (var i = 0; i < 5; i++) list.Items.Add(new Border { Width = 100, Height = 40 });
        Selection.SetIndicator(list, new FuncTemplate<Control?>(() => indicator));
        list.SelectedIndex = 0;
        Show(list, 200, 400);
        Advance(32);
        var from = indicator.Bounds.Y;

        list.SelectedIndex = 3;
        Advance(100);
        var halfway = indicator.Bounds.Y;
        Advance(1000);

        var to = list.ContainerFromIndex(3)!.TranslatePoint(default, list.Presenter!)!.Value.Y;
        Check(halfway > from && halfway < to && indicator.Bounds.Y == to, $"the indicator went {from}, {halfway}, {indicator.Bounds.Y} toward {to}");
    }

    private static void FollowChannels()
    {
        Follows(0d, 10d);
        Follows(0f, 10f);
        Follows(0, 10);
        Follows<IBrush>(Brushes.Red, Brushes.Blue, brush => ((ISolidColorBrush)brush).Color);
        Follows(Colors.Red, Colors.Blue);
        Follows(new Thickness(0), new Thickness(10));
        Follows(new CornerRadius(0), new CornerRadius(10));
        Follows(new Point(0, 0), new Point(10, 10));
        Follows(new Size(0, 0), new Size(10, 10));
        Follows(new Rect(0, 0, 0, 0), new Rect(10, 10, 10, 10));
        Follows(new Vector(0, 0), new Vector(10, 10));
        Follows(new RelativePoint(0, 0, RelativeUnit.Relative), new RelativePoint(1, 1, RelativeUnit.Relative));
        Follows(BoxShadows.Parse("0 0 0 0 Red"), BoxShadows.Parse("10 10 10 10 Blue"));
    }

    private static void Follows<T>(T from, T to, Func<T, object?>? key = null)
    {
        key ??= value => value;
        var channel = new Channel<T>();
        var source = new Pushed<T>(from);
        using var binding = Follow.Bind(channel, Channel<T>.ValueProperty, source, new FollowParams { Duration = 100, Ease = "linear" });

        source.Push(to);
        Advance(48);
        var halfway = key(channel.GetValue(Channel<T>.ValueProperty));
        Advance(100);
        var end = key(channel.GetValue(Channel<T>.ValueProperty));

        Check(!Equals(halfway, key(to)) && Equals(end, key(to)), $"{typeof(T).Name} went {halfway}, {end} toward {key(to)}");
    }

    private static void DistortRedraw()
    {
        var bar = new Border { Width = 40, Height = 20, Background = new SolidColorBrush(Colors.Red) };
        var distort = new Distort
        {
            Child = bar, Padding = new Thickness(20), Glitch = 6,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
        };
        var window = Show(distort, 100, 100);
        Render();

        bar.Background = new SolidColorBrush(Colors.Blue);
        Render();
        Render();

        Check(Count(window, Colors.Red) == 0 && Count(window, Colors.Blue) > 0, "the snapshot was not retaken after the child redrew");
    }

    private static void GlyphShardsOf()
    {
        var outline = PrestoAvalonia.TextOutline("B", new Typeface(FontFamily.Default), 100);

        var shards = GlyphShards.Of(outline, 8, seed: 1);

        Check(shards.Count >= 4 && shards.All(shard => shard.Bounds.Width > 0), $"the outline broke into {shards.Count} shards");
    }

    private static void ReorderMove()
    {
        ObservableCollection<int> items = [0, 1, 2, 3, 4];
        var list = new ItemsControl
        {
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<int>((_, _) => new Border { Width = 40, Height = 20, Background = Brushes.Blue, HorizontalAlignment = HorizontalAlignment.Left }),
            VerticalAlignment = VerticalAlignment.Top,
        };
        Reorder.SetEnabled(list, true);
        var window = Show(list, 100, 200);

        window.MouseDown(new Point(10, 30), MouseButton.Left);
        window.MouseMove(new Point(10, 65));
        window.MouseUp(new Point(10, 65), MouseButton.Left);
        Advance(1000);

        Check(items.SequenceEqual([0, 2, 1, 3, 4]), $"the items are {string.Join(", ", items)}");
    }

    private static void ReorderFiltered()
    {
        ObservableCollection<int> source = [0, -1, 1, 2, 3, 4];
        using var view = new FilteredView<int>(source, n => n >= 0);
        var list = new ItemsControl
        {
            ItemsSource = view,
            ItemTemplate = new FuncDataTemplate<int>((_, _) => new Border { Width = 40, Height = 20, Background = Brushes.Blue, HorizontalAlignment = HorizontalAlignment.Left }),
            VerticalAlignment = VerticalAlignment.Top,
        };
        Reorder.SetEnabled(list, true);
        var window = Show(list, 100, 200);

        window.MouseDown(new Point(10, 30), MouseButton.Left);
        window.MouseMove(new Point(10, 65));
        window.MouseUp(new Point(10, 65), MouseButton.Left);
        Advance(1000);

        Check(source.SequenceEqual([0, -1, 2, 1, 3, 4]), $"the source is {string.Join(", ", source)}");
    }

    private static void DotLottie()
    {
        using var archive = typeof(Program).Assembly.GetManifestResourceStream("badge.lottie")!;

        var composition = LottieComposition.FromDotLottie(archive);
        var view = new LottieView
        {
            Composition = composition, Width = 100, Height = 100,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
        };
        var window = Show(view, 100, 100);
        Render();

        // The archive's 40x40 red image is inlined and drawn.
        var red = Count(window, Colors.Red);
        Check(composition.AnimationIds.SequenceEqual(["badge"]) && red > 1000, $"ids {string.Join(", ", composition.AnimationIds)}, {red} red pixels");
    }

    private static void DevToolsRow()
    {
        var box = new Border { Name = "Hero", Width = 50, Height = 50 };
        var window = Show(new Panel { Children = { box } }, 900, 500);
        PrestoDevTools.Open(window);

        box.Animate(new() { ["Opacity"] = 0, Duration = 1000 });
        Advance(100);

        var texts = window.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToList();
        var progress = window.GetVisualDescendants().OfType<ProgressBar>().Any(bar => bar.Value > 0);
        PrestoDevTools.Close(window);
        Check(texts.Contains("Hero") && texts.Contains("Visual.Opacity") && progress, "the row does not show the animation's target, property and progress");
    }

    private static void RegisteredTarget()
    {
        Presto.RegisterTarget<Gauge>();
        var gauge = new Gauge();

        Presto.Animate(gauge, new() { ["Level"] = 10, Duration = 100 });
        Advance(200);

        Check(gauge.Level == 10, $"the level stopped at {gauge.Level}");
    }

    private sealed class Gauge
    {
        public double Level { get; set; }
    }

    private sealed class Channel<T> : AvaloniaObject
    {
#pragma warning disable AVP1002 // never referenced from XAML
        public static readonly StyledProperty<T> ValueProperty = AvaloniaProperty.Register<Channel<T>, T>("Value");
#pragma warning restore AVP1002
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
