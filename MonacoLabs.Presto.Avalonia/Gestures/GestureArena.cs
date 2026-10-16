using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Diagnostics;

namespace MonacoLabs.Presto.Gestures;

/// <summary>
/// Decides which gesture gets one pressed pointer, as Flutter's gesture arena does: members join on the press with a
/// <see cref="GestureGate"/>, the first gate to pass wins (the deepest element on a tie), and every other member is told it lost.
/// </summary>
internal sealed class GestureArena
{
    private static readonly Dictionary<IPointer, GestureArena> s_open = [];

    private readonly IPointer _pointer;
    private readonly TopLevel _root;
    private readonly Point _pressedAt;
    private readonly double _openedAt;
    private readonly bool _touch;
    private readonly InputElement? _watched;
    private readonly List<Entry> _members = [];
    private Timer? _hold;
    private Entry? _holding;

    private GestureArena(PointerPressedEventArgs e, TopLevel root)
    {
        _pointer = e.Pointer;
        _root = root;
        _pressedAt = e.GetPosition(root);
        _openedAt = Presto.Engine.Now;
        _touch = e.Pointer.Type != PointerType.Mouse;
        root.AddHandler(InputElement.PointerMovedEvent, HandleMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        root.AddHandler(InputElement.PointerReleasedEvent, HandleReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        // The pointer is implicitly captured to the pressed element, and any capture taken from it raises PointerCaptureLost there.
        _watched = (e.Pointer.Captured ?? e.Source) as InputElement;
        if (_watched is not null) _watched.PointerCaptureLost += HandleCaptureLost;
    }

    public static void Join(PointerPressedEventArgs e, IGestureMember member, GestureGate gate)
    {
        if (!s_open.TryGetValue(e.Pointer, out var arena))
        {
            var root = TopLevel.GetTopLevel(member.Element)!;
            s_open[e.Pointer] = arena = new GestureArena(e, root);
        }
        arena._members.Add(new Entry(member, gate) { Holding = arena._touch && gate.Hold > 0 });
        arena.ArmHold();
    }

    private static int Depth(Visual visual)
    {
        var depth = 0;
        for (var parent = visual.GetVisualParent(); parent is not null; parent = parent.GetVisualParent()) depth++;
        return depth;
    }

    private void HandleMoved(object? sender, PointerEventArgs e)
    {
        if (e.Pointer != _pointer) return;
        Vector travel = e.GetPosition(_root) - _pressedAt;
        Entry? winner = null;
        var deepest = -1;
        foreach (var entry in _members)
        {
            entry.Verdict = entry.Gate.Judge(travel, _touch);
            if (entry.Verdict != true) continue;
            var depth = Depth(entry.Member.Element);
            if (depth > deepest) (winner, deepest) = (entry, depth);
        }
        if (winner is not null)
        {
            Win(winner);
            e.PreventGestureRecognition();
            return;
        }
        for (var i = _members.Count - 1; i >= 0; i--)
        {
            var entry = _members[i];
            if (entry.Holding && travel.Length > GestureGate.TouchSlop) entry.Holding = false;
            if (entry.Verdict != false) continue;
            _members.RemoveAt(i);
            entry.Member.Lost();
        }
        if (_members.Count == 0) Close();
        else ArmHold();
    }

    private void HandleReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer != _pointer) return;
        Close();
        foreach (var entry in _members) entry.Member.EndedWithoutClaim();
    }

    private void HandleCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (e.Pointer != _pointer) return;
        Close();
        foreach (var entry in _members) entry.Member.Lost();
    }

    // Closed first, so the capture made for the winner is not taken for an outside one.
    private void Win(Entry winner)
    {
        Close();
        winner.Member.Won();
        foreach (var entry in _members)
        {
            if (entry != winner) entry.Member.Lost();
        }
        _pointer.Capture(winner.Member.CaptureTarget);
    }

    private void ArmHold()
    {
        Entry? next = null;
        foreach (var entry in _members)
        {
            if (entry.Holding && (next is null || entry.Gate.Hold < next.Gate.Hold)) next = entry;
        }
        if (next == _holding) return;
        _hold?.Cancel();
        _hold = null;
        _holding = next;
        if (next is null) return;
        using var origin = PrestoDiagnostics.Origin("Gestures");
        _hold = Presto.CreateTimer(new TimerParams
        {
            Duration = Math.Max(0, next.Gate.Hold - (Presto.Engine.Now - _openedAt)),
            OnComplete = _ => Win(next),
        });
    }

    private void Close()
    {
        s_open.Remove(_pointer);
        _root.RemoveHandler(InputElement.PointerMovedEvent, HandleMoved);
        _root.RemoveHandler(InputElement.PointerReleasedEvent, HandleReleased);
        if (_watched is not null) _watched.PointerCaptureLost -= HandleCaptureLost;
        _hold?.Cancel();
        _hold = null;
        _holding = null;
    }

    private sealed class Entry(IGestureMember member, GestureGate gate)
    {
        public IGestureMember Member { get; } = member;

        public GestureGate Gate { get; } = gate;

        public bool Holding { get; set; }

        public bool? Verdict { get; set; }
    }
}
