using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Monaco.Presto.AvaloniaBridge.Compositor;
using Monaco.Presto.AvaloniaBridge.Controls;
using Monaco.Presto.AvaloniaBridge.Gestures;
using Path = Avalonia.Controls.Shapes.Path;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// Per-frame allocation budget of the bridge write paths, the counterpart of <see cref="Tests.AllocationTests"/>. What is
/// left in each budget is Avalonia's own property system: a bare <c>border.Opacity = x</c> costs 80 bytes on its own.
/// </summary>
public sealed class AllocationTests : CompositorTestBase
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void StyledPropertyTweenTicksWithinBudget(bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        var border = new Border { Opacity = 0 };
        border.Animate(new() { ["Opacity"] = 1, Duration = 400, Loop = true, Alternate = true });

        var bytes = BytesPerTick();

        // measured 80, down from 104 when the double was boxed into an untyped binding: all of it is the property change Avalonia raises.
        Assert.True(bytes <= 96, $"{bytes:0.##} bytes per tick");
    }

    [AvaloniaTheory]
    [InlineData("HorizontalAlignment", false)]
    [InlineData("HorizontalAlignment", true)]
    [InlineData("IsVisible", false)]
    [InlineData("IsVisible", true)]
    public void ADiscreteTweenTicksWithinBudget(string property, bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        var border = new Border { Opacity = 0 };
        border.Animate(new()
        {
            ["Opacity"] = 1,
            [property] = TweenValue.Of(property == "IsVisible" ? false : HorizontalAlignment.Right),
            Duration = 400,
            Loop = true,
            Alternate = true,
        });

        var bytes = BytesPerTick();

        // measured 80 (IsVisible) and 83 (HorizontalAlignment): the opacity's 80, plus the alignment's change args at each flip.
        Assert.True(bytes <= 96, $"{bytes:0.##} bytes per tick");
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AFollowBoundWidthTicksWithinBudget(bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        var model = new LevelViewModel { Level = 0 };
        var border = new Border { DataContext = model };
        Follow.Bind(border, Layoutable.WidthProperty, new Binding(nameof(LevelViewModel.Level)), new() { Duration = 100000, Ease = "linear" });
        model.Level = 100000;

        var bytes = BytesPerTick();

        // measured 104, as GAPS-PLAN §1.7 predicted: the 80 of the property change, plus the follower's boxed double.
        Assert.True(bytes <= 128, $"{bytes:0.##} bytes per tick");
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnAnimatedMarkupWidthTicksWithinBudget(bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        var model = new LevelViewModel { Level = 0 };
        var border = new Border { DataContext = model };
        var extension = new FollowExtension(new Binding(nameof(LevelViewModel.Level))) { Duration = 100000, Ease = "linear" };
        border.Bind(Layoutable.WidthProperty, (BindingBase)extension.ProvideValue(new ProvidedTarget(border, Layoutable.WidthProperty)));
        model.Level = 100000;

        var bytes = BytesPerTick();

        // measured 160, as GAPS-PLAN §1.7 predicted for ToBinding(): 56 more than Follow.Bind's direct Bind.
        Assert.True(bytes <= 192, $"{bytes:0.##} bytes per tick");
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AMotionValueBoundWidthTicksWithinBudget(bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        var progress = Presto.CreateTimer(new() { Duration = 100000 }).ProgressValue;
        Follow.Bind(new Border(), Layoutable.WidthProperty, progress.Map(p => p * 100000));

        var bytes = BytesPerTick();

        // measured 80, below Follow.Bind's 104 for a binding: the double is pushed unboxed, and the graph adds nothing.
        Assert.True(bytes <= 96, $"{bytes:0.##} bytes per tick");
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AMotionValueFollowedXTicksWithinBudget(bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        var progress = Presto.CreateTimer(new() { Duration = 100000 }).ProgressValue;
        Follow.SetX(new Border(), progress.Map(p => p * 100000));

        var bytes = BytesPerTick();

        // measured 80: all of it is the change the composer's TranslateTransform raises.
        Assert.True(bytes <= 96, $"{bytes:0.##} bytes per tick");
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void APlayingSpriteSheetTicksWithinBudget(bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        new SpriteSheet { Columns = 4, Rows = 2 }.Play(fps: 24);

        var bytes = BytesPerTick();

        // measured 104: the 80 of a double tween, plus 64 for CurrentFrame's change args on the 38% of 16 ms ticks that reach a new frame.
        Assert.True(bytes <= 112, $"{bytes:0.##} bytes per tick");
    }

    [AvaloniaTheory]
    [InlineData("Grayscale", 1, false)]
    [InlineData("Grayscale", 1, true)]
    [InlineData("Pixelate", 8, false)]
    [InlineData("Pixelate", 8, true)]
    public void AnImageFilterTweenTicksWithinBudget(string filter, double to, bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        new FilteredImage().Animate(new() { [filter] = to, Duration = 400, Loop = true, Alternate = true });

        var bytes = BytesPerTick();

        // A drawn frame also creates native image filters and an SKPaint per render, which this managed budget does not price.
        Assert.True(bytes <= 96, $"{bytes:0.##} bytes per tick");
    }

    [AvaloniaTheory]
    [InlineData("Rotate", false)]
    [InlineData("Rotate", true)]
    [InlineData("RotateY", false)]
    [InlineData("RotateY", true)]
    public void TransformShorthandTweenTicksWithinBudget(string rotation, bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        var border = new Border();
        border.Animate(new() { ["X"] = 100, [rotation] = 90, Duration = 400, Loop = true, Alternate = true });

        var bytes = BytesPerTick();

        // measured 160: the composer reuses its transforms, so this is 80 per shorthand for the property change each raises.
        Assert.True(bytes <= 192, $"{bytes:0.##} bytes per tick");
    }

    [AvaloniaTheory]
    [InlineData("Blur", false)]
    [InlineData("Blur", true)]
    [InlineData("ShadowBlur", false)]
    [InlineData("ShadowBlur", true)]
    public void EffectShorthandTweenTicksWithinBudget(string shorthand, bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        var border = new Border();
        border.Animate(new() { [shorthand] = new[] { 2, 8 }, Duration = 400, Loop = true, Alternate = true });

        var bytes = BytesPerTick();

        // measured 80: the composer mutates its one effect, so all of it is the property change the effect raises.
        Assert.True(bytes <= 96, $"{bytes:0.##} bytes per tick");
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void MaskSweepTicksWithinBudget(bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        var border = new Border();
        border.Animate(new() { ["MaskSweep"] = new[] { 0, 0.9 }, Duration = 400, Loop = true, Alternate = true });

        var bytes = BytesPerTick();

        // measured 224: the composer mutates its one brush, so this is 112 each for the StartPoint and EndPoint changes, whose args carry two RelativePoints.
        Assert.True(bytes <= 256, $"{bytes:0.##} bytes per tick");
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AScrollYTweenTicksWithinBudget(bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        var scroller = ScrollStage.Show(trail: 600).Scroller;
        scroller.Animate(new() { ["ScrollY"] = 800, Duration = 400, Loop = true, Alternate = true });

        var bytes = BytesPerTick();

        // measured 800, as much as a timer writing the offset with SetCurrentValue itself: all of it is the ScrollViewer reacting to the change.
        Assert.True(bytes <= 896, $"{bytes:0.##} bytes per tick");
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void BrushTweenTicksWithinBudget(bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        var border = new Border { Background = new SolidColorBrush(Colors.Red) };
        border.Animate(new() { ["Background"] = "#0000ff", Duration = 400, Loop = true, Alternate = true });

        var bytes = BytesPerTick();

        // measured 64: recoloring the brush the codec reuses; pushing that same brush again costs nothing.
        Assert.True(bytes <= 96, $"{bytes:0.##} bytes per tick");
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void DrawTweenTicksWithinBudget(bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        var path = new Path
        {
            Data = Geometry.Parse("M 0,0 L 100,0 L 100,100"),
            Stretch = Stretch.None,
            Stroke = Brushes.Black,
            StrokeThickness = 2,
        };
        PrestoAvalonia.CreateDrawable(path).Animate(new() { ["Draw"] = "0 1", Duration = 400, Loop = true, Alternate = true });

        var bytes = BytesPerTick();

        // measured 288, down from 944 for a fresh list per frame: 144 per dash written into the reused
        // AvaloniaList, which is the change args and the two boxed doubles its render subscription is handed.
        Assert.True(bytes <= 320, $"{bytes:0.##} bytes per tick");
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AHoverAllocatesOnlyAtItsTransitions(bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        var box = Box();
        Motion.SetHover(box, Presto.Build().Scale(1.1).Duration(100));
        var window = Show(box);
        Tick();

        var idle = BytesPerTick();
        // BytesPerTick's warm-up frames play each 100 ms transition out, so only the settled frames are measured.
        window.MouseMove(new Point(25, 25));
        var hovered = BytesPerTick();
        window.MouseMove(new Point(150, 150));
        var rested = BytesPerTick();

        Assert.True(idle == 0 && hovered == 0 && rested == 0,
            $"{idle:0.##} bytes per tick idle, {hovered:0.##} hovered, {rested:0.##} at rest");
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnIdleTiltAllocatesNothingPerFrame(bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        var box = Box();
        Tilt.SetEnabled(box, true);
        var window = Show(box);
        Tick();

        var idle = BytesPerTick();
        window.MouseMove(new Point(40, 10));
        Frames.Advance(2000, 16);
        var tilted = BytesPerTick();

        Assert.True(idle == 0 && tilted == 0, $"{idle:0.##} bytes per tick idle, {tilted:0.##} tilted and still");
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void APinchMoveAllocatesOnlyItsWrites(bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        var target = new Border { Background = Brushes.Red };
        var bystander = new Border();
        var window = Show(new Panel { Children = { target, bystander } });
        target.CreateTransformable(new() { PanBounds = PanBounds.Free, Rotate = true });
        var touch = new Touch(window);
        touch.Down(1, new Point(40, 60));
        touch.Down(2, new Point(80, 60));
        PointerEventArgs[] moves = [touch.Move(2, new Point(100, 80)), touch.Move(2, new Point(80, 60))];

        // Raising the same args on a control nobody listens to prices Avalonia's routing, which is not the transformable's.
        var bytes = BytesPerMove(target, moves) - BytesPerMove(bystander, moves);

        // ~400 B is Avalonia's change args for X, Y, ScaleX, ScaleY and Angle; the rest is Animatable.Set composing each value (see GESTURES-PLAN §7).
        Assert.True(bytes <= 600, $"{bytes:0.##} bytes per move");
    }

    private double BytesPerMove(Interactive on, PointerEventArgs[] moves)
    {
        const int measured = 200;
        var before = 0L;
        for (var i = 0; i < 10 + measured; i++)
        {
            if (i == 10) before = GC.GetAllocatedBytesForCurrentThread();
            var move = moves[i % moves.Length];
            move.Handled = false;
            on.RaiseEvent(move);
            Frames.Advance(16);
        }
        return (GC.GetAllocatedBytesForCurrentThread() - before) / (double)measured;
    }

    /// <remarks>
    /// The scene's cost is what drawing with a tweened opacity adds to drawing the same dots opaque, measured in alternating
    /// blocks with the median difference kept: every window earlier tests left open is rendered with each frame too, which in
    /// a full run drifts the baseline of about 4.6 KB per frame by ±100 bytes, more than the budget.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompositorSceneFramesWithinBudget(bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        const int blocks = 11;
        var (_, canvas) = ShowScene(new DotScene());
        // Two alternate cycles of the scene fill the brush cache.
        TickFor(1000);

        var differences = new double[blocks];
        for (var i = 0; i < blocks; i++)
        {
            var animated = BytesPerFrame(canvas, animated: true);
            differences[i] = animated - BytesPerFrame(canvas, animated: false);
        }
        Array.Sort(differences);
        var bytes = differences[blocks / 2];

        // measured a median of 0 to 6 over twelve full runs, with single blocks ±80: once the brush cache is full, tweening costs nothing.
        Assert.True(bytes <= 64, $"{bytes:0.##} bytes per frame, median of {string.Join(", ", differences.Select(d => d.ToString("0")))}");
    }

    /// <remarks>
    /// Drawing is priced as a frame with 500 live particles minus a frame with none, in alternating blocks with the median
    /// kept as above; each block re-adds the same seeded burst, so the brush cache is warm from the second block on.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AParticleSceneOf500DrawsWithoutAllocating(bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        const int blocks = 11;
        var burst = new EmitterParams
        {
            Burst = 500,
            Speed = (0, 0),
            Lifetime = (20000, 20000),
            Spin = (-0.2, 0.2),
            Flutter = 2,
            Area = (64, 64),
            Colors = [new(255, 80, 80), new(80, 255, 80), new(80, 80, 255)],
            Seed = 3,
        };
        var (_, canvas) = ShowScene(new ParticleScene());
        ParticleSystem? system = null;

        var differences = new double[blocks + 1];
        for (var i = 0; i < differences.Length; i++)
        {
            canvas.Invoke<ParticleScene>(scene => system = scene.Add(burst, new Point(32, 32)));
            var drawing = BytesPerFrame();
            canvas.Invoke<ParticleScene>(_ => system!.Clear());
            differences[i] = drawing - BytesPerFrame();
        }
        var measured = differences[1..];
        Array.Sort(measured);
        var bytes = measured[blocks / 2];

        Assert.True(bytes <= 64, $"{bytes:0.##} bytes per frame, median of {string.Join(", ", measured.Select(d => d.ToString("0")))}");
    }

    /// <remarks>
    /// Each block presses 50 hosts from the keyboard and prices the frames where all 50 grow, then releases them and prices
    /// as many idle frames once they have faded; the median difference is kept, as above.
    /// </remarks>
    [AvaloniaFact]
    public void FiftyConcurrentRipplesDrawWithinTheirBudget()
    {
        const int blocks = 7;
        var grid = new UniformGrid { Columns = 10, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        for (var i = 0; i < 50; i++)
        {
            var host = new Border { Width = 36, Height = 20, CornerRadius = new CornerRadius(6), Background = Brushes.White };
            Ripple.SetEnabled(host, true);
            grid.Children.Add(host);
        }
        new Window { Content = grid, Background = Brushes.White, Width = 400, Height = 200 }.Show();
        Tick();

        var differences = new double[blocks + 1];
        for (var i = 0; i < differences.Length; i++)
        {
            foreach (var host in grid.Children) host.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = host });
            Tick(Frame);
            var growing = BytesPerFrame(2, 20);
            foreach (var host in grid.Children) host.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Enter, Source = host });
            TickFor(500);
            differences[i] = growing - BytesPerFrame(2, 20);
        }
        var measured = differences[1..];
        Array.Sort(measured);
        var bytes = measured[blocks / 2];

        // measured 16 900: about 280 per host is Avalonia redrawing an invalidated custom visual, and about 58 the rounded clip.
        Assert.True(bytes <= 20_000, $"{bytes:0.##} bytes per frame, median of {string.Join(", ", measured.Select(d => d.ToString("0")))}");
    }

    /// <summary>Bytes the render loop allocates per compositor frame, after the frames that deliver what was posted.</summary>
    private static double BytesPerFrame(int warmUp = 10, int measured = 100)
    {
        for (var i = 0; i < warmUp; i++) Tick(Frame);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < measured; i++) Tick(Frame);
        return (GC.GetAllocatedBytesForCurrentThread() - before) / (double)measured;
    }

    /// <summary>Bytes the render loop allocates per compositor frame while the scene draws its dots tweened or opaque.</summary>
    private static double BytesPerFrame(CompositorCanvas canvas, bool animated)
    {
        canvas.Post(animated);
        return BytesPerFrame();
    }

    /// <summary>A ring of dots whose opacity tweens; <see cref="OnMessage"/> switches between drawing that opacity and drawing them opaque.</summary>
    private sealed class DotScene : CompositorScene
    {
        private readonly SceneTarget[] _dots = [.. Enumerable.Range(0, 36).Select(_ => new SceneTarget())];
        private bool _animated = true;

        protected override void OnMessage(object message) => _animated = (bool)message;

        protected override void OnAttached() =>
            Presto.Animate(_dots, new()
            {
                ["Opacity"] = 0.15,
                Duration = 400,
                Delay = Presto.Stagger(8),
                Ease = Eases.InOutSine,
                Loop = true,
                Alternate = true,
            });

        protected override void OnRender(ImmediateDrawingContext context, Size size)
        {
            for (var i = 0; i < _dots.Length; i++)
            {
                var angle = i * Math.Tau / _dots.Length;
                var center = new Point(size.Width / 2 + Math.Cos(angle) * 18, size.Height / 2 + Math.Sin(angle) * 18);
                context.DrawEllipse(Brush(Colors.SteelBlue, _animated ? _dots[i].Opacity : 1), null, center, 3, 3);
            }
        }
    }
}
