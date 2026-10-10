using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MonacoLabs.Presto.DevTools;

/// <summary>The tweens of the selected timer and its children, one row each, with live values, ease and composition.</summary>
internal sealed class TweenTable : StackPanel
{
    private Timer? _timer;
    private int _shape = -1;

    internal IEnumerable<Row> Rows => Children.OfType<Row>();

    /// <summary>The root of a live, non-ended root other than <paramref name="tween"/>'s that animates the same property of the same target with a newer tween.</summary>
    internal static Timer? NewerOwner(Tween tween)
    {
        var root = RootInspector.RootOf(tween.Parent);
        foreach (var other in root.Engine.Diagnostics.Roots)
        {
            if (other != root && !other.Completed && !other.Canceled && Takes(other, tween)) return other;
        }
        return null;
    }

    internal void Refresh(Timer timer, Tween? selected)
    {
        var shape = TickableRow.Shape(timer);
        if (timer != _timer || shape != _shape)
        {
            _timer = timer;
            _shape = shape;
            Children.Clear();
            foreach (var tween in TickableRow.AllTweens(timer)) Children.Add(new Row(tween));
        }
        foreach (var control in Children) ((Row)control).Update(selected);
    }

    private static bool Takes(Timer timer, Tween tween) =>
        TickableRow.AllTweens(timer).Any(other => other.Id > tween.Id && other.Target == tween.Target && other.Property == tween.Property);

    internal sealed class Row : Grid
    {
        private static readonly IBrush s_selected = new SolidColorBrush(Colors.SteelBlue, 0.25);

        internal Row(Tween tween)
        {
            Tween = tween;
            ColumnDefinitions = new ColumnDefinitions("90,*,60,40,90,55,Auto");
            EaseName.Text = Eases.TryGetName(tween.Ease, out var name) ? name : "custom";
            Thumbnail.Ease = tween.Ease;
            var start = ScrubTrack.Origin(tween.Parent) + tween.StartTime;
            var span = DevToolsOverlay.Cell(0.7);
            span.Text = $"{start:0}–{start + tween.Duration:0} ms";
            var composition = DevToolsOverlay.Cell(0.7);
            composition.Text = tween.Composition.ToString().ToLowerInvariant();
            var property = DevToolsOverlay.Cell();
            property.Text = tween.Property;
            Children.AddRange([property, Values, EaseName, Thumbnail, span, composition, Marker]);
            for (var i = 0; i < Children.Count; i++) SetColumn(Children[i], i);
        }

        internal Tween Tween { get; }

        internal TextBlock Values { get; } = DevToolsOverlay.Cell();

        internal TextBlock EaseName { get; } = DevToolsOverlay.Cell(0.8);

        internal EaseThumbnail Thumbnail { get; } = new() { Width = 32, Height = 16, Margin = new Thickness(0, 1, 8, 1) };

        internal TextBlock Marker { get; } = new() { FontSize = 12, Foreground = Brushes.OrangeRed, IsVisible = false, VerticalAlignment = VerticalAlignment.Center };

        internal void Update(Tween? selected)
        {
            Background = Tween == selected ? s_selected : null;
            Values.Text = TickableRow.Values(Tween);
            Thumbnail.Progress = TickableRow.ProgressOf(Tween);
            if (Marker.IsVisible == Tween.IsOverridden) return;
            Marker.IsVisible = Tween.IsOverridden;
            if (Marker.IsVisible) Marker.Text = $"overridden by #{NewerOwner(Tween)?.Id ?? "?"}";
        }
    }
}
