using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace MonacoLabs.Presto.DevTools;

/// <summary>
/// Playback controls, a scrub track and the tween table for the root of the selection. Controls always act on the root:
/// a selected timeline child is only highlighted on its track. An ended root is controlled only after an explicit revive.
/// </summary>
internal sealed class RootInspector : DockPanel
{
    private readonly StackPanel _controls;
    private readonly TextBlock _time = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    private readonly ConditionalWeakTable<Timer, StrongBox<double>> _speeds = new();
    private Timer? _revived;
    private bool _warned;
    private bool _resumeAfterScrub;

    internal RootInspector()
    {
        Track = new ScrubTrack(this) { Margin = new Thickness(6, 2) };
        Play.Click += (_, _) => Root!.Play();
        Pause.Click += (_, _) => Root!.Pause();
        Reverse.Click += (_, _) => Root!.Reverse();
        Restart.Click += (_, _) => Root!.Restart();
        Revive.Click += (_, _) => ConfirmRevive();
        ConfirmRevert.Click += (_, _) =>
        {
            Root!.Revert();
            Revert.Flyout!.Hide();
        };
        Revert.Flyout = new Flyout { Content = ConfirmRevert };
        Seek.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && TryParse(Seek, out var time)) Root!.Seek(time);
        };
        Speed.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || !TryParse(Speed, out var speed) || speed <= 0) return;
            _speeds.TryAdd(Root!, new StrongBox<double>(Root!.Speed));
            Root.Speed = speed;
        };

        _controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { Play, Pause, Reverse, Restart, Seek, Speed, Revert } };
        var header = new WrapPanel { Margin = new Thickness(6, 2), Children = { _time, CreatedAt, _controls, Revive, Warning } };
        _time.Margin = new Thickness(0, 0, 8, 0);
        SetDock(header, Dock.Top);
        SetDock(Track, Dock.Top);
        Children.Add(header);
        Children.Add(Track);
        Children.Add(new ScrollViewer { Content = Tweens, Margin = new Thickness(6, 2) });
    }

    internal Timer? Root { get; private set; }

    internal ScrubTrack Track { get; }

    internal TweenTable Tweens { get; } = new();

    internal Button Play { get; } = DevToolsOverlay.Compact(new Button { Content = "Play" });

    internal Button Pause { get; } = DevToolsOverlay.Compact(new Button { Content = "Pause" });

    internal Button Reverse { get; } = DevToolsOverlay.Compact(new Button { Content = "Reverse" });

    internal Button Restart { get; } = DevToolsOverlay.Compact(new Button { Content = "Restart" });

    internal TextBox Seek { get; } = new() { PlaceholderText = "seek ms", Width = 70, FontSize = 12, MinHeight = 0, Padding = new Thickness(4, 1) };

    internal TextBox Speed { get; } = new() { Width = 50, FontSize = 12, MinHeight = 0, Padding = new Thickness(4, 1) };

    internal Button Revert { get; } = DevToolsOverlay.Compact(new Button { Content = "Revert…" });

    internal Button ConfirmRevert { get; } = DevToolsOverlay.Compact(new Button { Content = "Revert to the start values" });

    /// <summary>Shown while the root has completed or been canceled; scrubbing it revives it and can override newer animations.</summary>
    internal Button Revive { get; } = DevToolsOverlay.Compact(new Button { Content = "Revive" });

    /// <summary>The first app frame of the root's <see cref="Timer.CreationTrace"/>, with the whole trace as its tooltip; hidden without a trace.</summary>
    internal TextBlock CreatedAt { get; } = DevToolsOverlay.Cell(0.7);

    internal TextBlock Warning { get; } = new() { FontSize = 12, Foreground = Brushes.OrangeRed, IsVisible = false, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) };

    private bool Blocked => Root is { } root && (root.Completed || root.Canceled) && root != _revived;

    internal static Timer RootOf(Timer timer)
    {
        while (timer.Parent is { } parent) timer = parent;
        return timer;
    }

    /// <summary>Follows the list selection; the overlay calls it once per frame.</summary>
    internal void Refresh(Timer? selected, Tween? tween)
    {
        var root = selected is null ? null : RootOf(selected);
        if (root != Root)
        {
            Root = root;
            _warned = false;
            Warning.IsVisible = false;
            Revive.Content = "Revive";
            if (root != null) Speed.Text = root.Speed.ToString(CultureInfo.InvariantCulture);
            CreatedAt.Text = root?.CreationTrace is { } trace ? AppFrame(trace) : null;
            CreatedAt.IsVisible = CreatedAt.Text != null;
            ToolTip.SetTip(CreatedAt, root?.CreationTrace?.ToString());
        }
        IsVisible = root != null;
        if (root is null) return;
        var blocked = Blocked;
        _controls.IsEnabled = !blocked;
        Revive.IsVisible = blocked;
        var duration = double.IsPositiveInfinity(root.IterationCount) ? "∞" : root.Duration.ToString("0", CultureInfo.InvariantCulture);
        _time.Text = $"#{root.Id}  {root.CurrentTime:0} / {duration} ms";
        Track.Update(root, tween != null ? Span(ScrubTrack.Origin(tween.Parent) + tween.StartTime, tween.Duration)
            : selected != root ? Span(ScrubTrack.Origin(selected!.Parent!) + selected.Offset, (selected.Delay + selected.Duration) / selected.Speed)
            : null);
        Tweens.Refresh(selected!, tween);
    }

    /// <summary>Pauses the root for scrubbing, remembering whether to resume it; false while the root is ended and not revived.</summary>
    internal bool BeginScrub()
    {
        if (Root is null || Blocked) return false;
        _resumeAfterScrub = !Root.Paused;
        Root.Pause();
        return true;
    }

    internal void ScrubTo(double time) => Root?.Seek(time);

    internal void RestoreSpeeds()
    {
        foreach (var (root, speed) in _speeds) root.Speed = speed.Value;
    }

    internal void EndScrub()
    {
        if (_resumeAfterScrub) Root?.Resume();
        _resumeAfterScrub = false;
    }

    private static (double, double) Span(double start, double length) => (start, start + length);

    private void ConfirmRevive()
    {
        if (!_warned && Conflicts(Root!) is { Length: > 0 } conflicts)
        {
            _warned = true;
            Warning.Text = $"Reviving overrides live tweens: {conflicts}";
            Warning.IsVisible = true;
            Revive.Content = "Revive anyway";
            return;
        }
        _revived = Root;
        Warning.IsVisible = false;
    }

    private static string Conflicts(Timer root) => string.Join(", ", TickableRow.AllTweens(root)
        .Select(tween => TweenTable.NewerOwner(tween) is { } owner ? $"{tween.Property} (#{owner.Id})" : null)
        .Where(conflict => conflict != null));

    private static string? AppFrame(StackTrace trace)
    {
        foreach (var frame in trace.GetFrames())
        {
            if (DiagnosticMethodInfo.Create(frame) is not { DeclaringTypeName: { } type } method || IsPresto(type)) continue;
            var line = frame.GetFileLineNumber();
            var file = Path.GetFileName(frame.GetFileName());
            return $"created at {type[(type.LastIndexOfAny(['.', '+']) + 1)..]}.{method.Name}" + (file is null ? "" : $" ({file}:{line})");
        }
        return null;
    }

    private static bool IsPresto(string name) => name == "MonacoLabs.Presto" || name.StartsWith("MonacoLabs.Presto.", StringComparison.Ordinal);

    private static bool TryParse(TextBox box, out double value) =>
        double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
