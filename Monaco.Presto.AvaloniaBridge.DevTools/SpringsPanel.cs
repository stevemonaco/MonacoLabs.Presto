using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Monaco.Presto.AvaloniaBridge.DevTools;

/// <summary>The engine's moving <see cref="MotionValue"/>s and every property of the registered <see cref="Animatable"/>s: value, target and a speed sparkline.</summary>
internal sealed class SpringsPanel : StackPanel
{
    private readonly Dictionary<object, Entry> _entries = new(ReferenceEqualityComparer.Instance);
    private int _generation;

    internal SpringsPanel() => Margin = new Thickness(6, 2);

    /// <summary>Polls the springs; the overlay calls it once per frame.</summary>
    internal void Refresh()
    {
        var diagnostics = Presto.Engine.Diagnostics;
        var now = Presto.Engine.Now;
        _generation++;
        foreach (var value in diagnostics.ActiveValues)
        {
            var target = value.SpringTarget is { } settling ? Format(settling) : "";
            Show(value, value.FollowSpring != null ? "spring" : "velocity", Format(value.Value), value.Value, target, now);
        }
        foreach (var animatable in diagnostics.Animatables)
        {
            var name = animatable.Targets.Count > 0 ? TickableRow.Summarize(animatable.Targets[0]) : "";
            foreach (var (property, animation) in animatable.Animations)
            {
                var first = FirstTween(animation);
                var current = first?.GetCurrent();
                var number = current is { Channels.Length: 0 } numeric ? numeric.Number : double.NaN;
                Show(animation, $"{name}.{property}", current is { } shown ? TickableRow.Display(shown) : "", number, first is { } to ? TickableRow.Display(to.GetTo()) : "", now);
            }
        }
        foreach (var (key, entry) in _entries)
        {
            if (entry.Generation == _generation) continue;
            _entries.Remove(key);
            Children.Remove(entry.Row);
        }
        IsVisible = _entries.Count > 0;
    }

    private void Show(object key, string label, string text, double number, string target, double now)
    {
        if (!_entries.TryGetValue(key, out var entry))
        {
            _entries[key] = entry = new Entry(label, number, now);
            Children.Add(entry.Row);
        }
        entry.Generation = _generation;
        entry.Value.Text = text;
        entry.Target.Text = target;
        if (double.IsFinite(number) && now > entry.Time)
        {
            entry.Speed.Push(Math.Abs(number - entry.Last) / (now - entry.Time));
            (entry.Last, entry.Time) = (number, now);
        }
    }

    private static Tween? FirstTween(Animation animation)
    {
        foreach (var tween in animation.Tweens) return tween;
        return null;
    }

    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private sealed class Entry
    {
        internal Entry(string label, double value, double now)
        {
            (Last, Time) = (value, now);
            var name = DevToolsOverlay.Cell();
            name.Text = label;
            Row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("200,90,90,120"),
                Children = { name, Value, Target, Speed },
            };
            for (var i = 0; i < Row.Children.Count; i++) Grid.SetColumn(Row.Children[i], i);
        }

        internal Grid Row { get; }

        internal TextBlock Value { get; } = DevToolsOverlay.Cell();

        internal TextBlock Target { get; } = DevToolsOverlay.Cell();

        internal Sparkline Speed { get; } = new() { Height = 14, VerticalAlignment = VerticalAlignment.Center };

        internal double Last { get; set; }

        internal double Time { get; set; }

        internal int Generation { get; set; }
    }
}
