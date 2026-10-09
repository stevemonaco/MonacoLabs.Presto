using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Compositor;
using static Monaco.Presto.AvaloniaBridge.PrestoKeys;
using Timer = Monaco.Presto.Timer;

namespace PrestoSample.Showcase.Pages;

public partial class StressBenchPage : ShowcasePage
{
    private const int DefaultCount = 5000;

    private const int BrowserMaximum = 4000;
    private const int GcWindowSeconds = 10;
    private const double CycleMs = 1200;
    private const string Aqua = "#2DD4BF";

    private const int UiThread = 0;
    private const int PerVisual = 1;
    private const int OpacityOnly = 0;
    private const int Transform = 1;
    private const int TransformAndColor = 2;

    private static readonly Size Field = new(720, 300);

    private readonly Queue<(int Gen0, long Heap)> _gc = new();
    private Scope? _bench;
    private List<object> _workload = [];

    public StressBenchPage()
    {
        InitializeComponent();
        if (OperatingSystem.IsBrowser())
        {
            CountSlider.Maximum = BrowserMaximum;
            ModeBox.IsEnabled = false;
            WorkerToggle.IsEnabled = false;
        }
        CountSlider.Value = DefaultCount;
        HowCode.Code = $"{Snippets.StressBenchModes}\n\n{Snippets.StressBenchWorker}";
    }

    /// <summary>What the current count, mode and property set run: animations, the bench scene and the worker's engine.</summary>
    public IReadOnlyList<object> Workload => _workload;

    protected override void OnAttached()
    {
        Graph.Scene = new FrameGraphScene();
        Presto.CreateTimer(new() { OnUpdate = _ => Graph.Post(new FrameGraphScene.FrameTime(Presto.Engine.DeltaTime)) });
        _gc.Clear();
        SampleGc();
        Presto.CreateTimer(new() { Duration = 1000, Loop = true, OnLoop = _ => SampleGc() });
        _bench = new Scope();
        _bench.Add(_ => Build());
    }

    private void Rebuild(object? sender, RoutedEventArgs e) => _bench?.Refresh();

    private Action Build()
    {
        var count = (int)CountSlider.Value;
        var set = SetBox.SelectedIndex;
        _workload = [];
        var mode = ModeBox.SelectedIndex;
        var targets = mode is UiThread or PerVisual ? AddTargets(count) : [];
        var build = Stopwatch.StartNew();
        #region snippet StressBenchModes
        switch (mode)
        {
            case UiThread:
                _workload.Add(Presto.Animate(targets, Motion(set, "Background", Aqua)));
                break;
            case PerVisual:
                _workload.Add(PrestoAvalonia.AnimateOnCompositor(targets, Timed(set == OpacityOnly
                    ? new CompositorAnimationParams { ["Opacity"] = .15 }
                    : new CompositorAnimationParams { [Scale] = .35, [Rotate] = 90 })));
                // Color has no composition track, so it stays on the UI thread.
                if (set == TransformAndColor) _workload.Add(Presto.Animate(targets, Timed(new AnimationParams { ["Background"] = Aqua })));
                break;
            default:
                var scene = new BenchScene(count, Motion(set, "Tint", 1));
                SceneField.Scene = scene;
                _workload.Add(scene);
                break;
        }
        #endregion
        BuildReadout.Text = string.Create(CultureInfo.InvariantCulture, $"build {build.Elapsed.TotalMilliseconds:0.0} ms");

        var stopWorker = WorkerToggle.IsChecked == true ? StartWorker(count, set) : null;
        return () =>
        {
            Targets.Children.Clear();
            SceneField.Scene = null;
            stopWorker?.Cancel();
            WorkerReadout.Text = "worker off";
        };
    }

    private Border[] AddTargets(int count)
    {
        var (columns, pitch) = BenchScene.Grid(count, Field);
        var targets = new Border[count];
        for (var i = 0; i < count; i++)
        {
            var target = targets[i] = new Border { Width = pitch * .7, Height = pitch * .7, Background = Palette.Accent };
            Canvas.SetLeft(target, ((i % columns) + .15) * pitch);
            Canvas.SetTop(target, ((i / columns) + .15) * pitch);
        }
        Targets.Children.AddRange(targets);
        return targets;
    }

    private CancellationTokenSource StartWorker(int count, int set)
    {
        var stop = new CancellationTokenSource();
        var cells = Enumerable.Range(0, count).Select(_ => new BenchScene.Cell()).ToArray();
        var frames = 0;
        var windowStart = 0d;

        #region snippet StressBenchWorker
        var engine = new Engine(new StopwatchFrameSource()) { UseDefaultMainLoop = false };
        // Started from the thread pool, which has no synchronization context, so the pump ticks the engine on its own thread.
        Task.Run(() =>
        {
            using (engine.MakeCurrent())
            {
                Presto.Animate(cells, Motion(set, "Tint", 1));
                Presto.CreateTimer(new() { OnUpdate = Report });
            }
            engine.UseDefaultMainLoop = true;
            engine.Wake();
        });
        #endregion

        void Report(Timer timer)
        {
            // Cancellation pauses the worker engine from its own tick, on its own thread.
            if (stop.IsCancellationRequested)
            {
                engine.Pause();
                return;
            }
            if (!FpsMeter.TrySample(timer, ref frames, ref windowStart, out var fps)) return;
            var cell = cells[0];
            var text = string.Create(CultureInfo.InvariantCulture, $"worker {fps:0} fps · cell 0 opacity {cell.Opacity:0.00} scale {cell.Scale:0.00}");
            Dispatcher.UIThread.Post(() =>
            {
                if (!stop.IsCancellationRequested) WorkerReadout.Text = text;
            });
        }

        _workload.Add(engine);
        return stop;
    }

    private void SampleGc()
    {
        var now = (Gen0: GC.CollectionCount(0), Heap: GC.GetTotalMemory(false));
        _gc.Enqueue(now);
        if (_gc.Count > GcWindowSeconds + 1) _gc.Dequeue();
        var (gen0, heap) = _gc.Peek();
        GcReadout.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"gen-0 +{now.Gen0 - gen0} · heap {(now.Heap - heap) / 1048576d:+0.0;-0.0;0.0} MB over {_gc.Count - 1} s");
    }

    private static AnimationParams Motion(int set, string tint, TweenValue tintTo) => Timed(set switch
    {
        OpacityOnly => new AnimationParams { ["Opacity"] = .15 },
        Transform => new AnimationParams { [Scale] = .35, [Rotate] = 90 },
        _ => new AnimationParams { [Scale] = .35, [Rotate] = 90, [tint] = tintTo },
    });

    private static AnimationParams Timed(AnimationParams motion) => motion with
    {
        Duration = CycleMs,
        Delay = Presto.Stagger(new double[] { 0, CycleMs }),
        Ease = Eases.InOutSine,
        Loop = true,
        Alternate = true,
    };

    private static CompositorAnimationParams Timed(CompositorAnimationParams motion) => motion with
    {
        Duration = CycleMs,
        Delay = Presto.Stagger(new double[] { 0, CycleMs }),
        Ease = Eases.InOutSine,
        Loop = true,
        Alternate = true,
    };
}
