using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.VisualTree;

namespace Monaco.Presto.AvaloniaBridge.Targets;

/// <summary>
/// Applies <see cref="WhenDetached"/> to a root animation or timeline whose every target resolves to a visual through
/// <see cref="TargetOwner"/>: pausing or reverting it when the last attached one leaves the visual tree, and resuming
/// what it paused when one returns. A timeline gathers the targets of its child animations as it adds them, and
/// continues for good once one of them has a target that is not a visual.
/// </summary>
/// <remarks>
/// The trackers live in an attached value of each visual, never a static table, so a visual that never returns is
/// collected with its paused animation. Trackers of ended animations are dropped at each attach, detach or new track.
/// </remarks>
internal sealed class DetachTracker(Timer root, WhenDetached mode)
{
    private static readonly AttachedProperty<List<DetachTracker>?> TrackersProperty =
        AvaloniaProperty.RegisterAttached<DetachTracker, Visual, List<DetachTracker>?>("Trackers");

    private static readonly ConditionalWeakTable<Timer, DetachTracker> s_roots = new();

    private int _attached;
    private bool _paused;
    private bool _continues;

    public static void Track(Timer timer)
    {
        if (timer.Parent is Timeline parent)
        {
            if (timer is Animation child && s_roots.TryGetValue(parent, out var owner)) owner.Add(child);
            return;
        }
        if (timer is not (Timeline or Animation { Completed: false })) return;
        var mode = timer._hostLifetime as WhenDetached? ?? PrestoAvalonia.WhenDetached;
        if (mode == WhenDetached.Continue) return;
        var tracker = new DetachTracker(timer, mode);
        s_roots.Add(timer, tracker);
        if (timer is Animation animation) tracker.Add(animation);
    }

    /// <summary>Whether <paramref name="root"/> is paused because its last visual target left the tree.</summary>
    public static bool IsDetachPaused(Timer root) => root.Paused && s_roots.TryGetValue(root, out var tracker) && tracker._paused;

    private void Add(Animation animation)
    {
        if (_continues) return;
        var targets = animation.Targets;
        for (var i = 0; i < targets.Count; i++)
        {
            if (TargetOwner.Find(targets[i]) != null) continue;
            _continues = true;
            if (_paused) Resume();
            return;
        }
        for (var i = 0; i < targets.Count; i++) Watch(TargetOwner.Find(targets[i])!);
    }

    private void Watch(Visual visual)
    {
        var trackers = visual.GetValue(TrackersProperty);
        if (trackers == null)
        {
            visual.SetValue(TrackersProperty, trackers = []);
            visual.AttachedToVisualTree += OnAttached;
            visual.DetachedFromVisualTree += OnDetached;
        }
        else
        {
            if (trackers.Contains(this)) return;
            trackers.RemoveAll(static t => t.Ended);
        }
        trackers.Add(this);
        if (visual.IsAttachedToVisualTree()) Changed(1);
    }

    private bool Ended => _continues || root.Cancelled;

    private static void OnAttached(object? sender, VisualTreeAttachmentEventArgs e) => Changed((Visual)sender!, 1);

    private static void OnDetached(object? sender, VisualTreeAttachmentEventArgs e) => Changed((Visual)sender!, -1);

    private static void Changed(Visual visual, int change)
    {
        var trackers = visual.GetValue(TrackersProperty)!;
        for (var i = 0; i < trackers.Count; i++)
        {
            if (!trackers[i].Ended) trackers[i].Changed(change);
        }
        trackers.RemoveAll(static t => t.Ended);
        if (trackers.Count > 0) return;
        visual.AttachedToVisualTree -= OnAttached;
        visual.DetachedFromVisualTree -= OnDetached;
        visual.ClearValue(TrackersProperty);
    }

    private void Changed(int change)
    {
        _attached += change;
        if (change > 0)
        {
            if (_attached == 1 && _paused) Resume();
        }
        else if (_attached == 0)
        {
            if (mode == WhenDetached.Revert) root.Revert();
            else if (!root.Paused)
            {
                root.Pause();
                _paused = true;
            }
        }
    }

    private void Resume()
    {
        _paused = false;
        root.Resume();
    }
}
