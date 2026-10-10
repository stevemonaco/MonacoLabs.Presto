using Avalonia;
using Avalonia.Controls;
using MonacoLabs.Presto.Events;
using MonacoLabs.Presto.Core;
using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto.Targets;

/// <summary>
/// The <c>"ScrollX"</c> and <c>"ScrollY"</c> shorthands of a <see cref="ScrollViewer"/>: one axis of its offset, written
/// through <see cref="ScrollWriter"/> and clamped to the scrollable range every frame.
/// </summary>
/// <remarks>
/// Any offset change the tween did not write removes it, unless an extent shrink only cut its last write back to the new
/// end. A revert leaves the offset where it is.
/// </remarks>
internal sealed class ScrollOffsetAccessor : IPropertyAccessor
{
    private readonly bool _horizontal;
    private readonly EventHandler<AvaloniaPropertyChangedEventArgs> _handlePropertyChanged;

    public ScrollOffsetAccessor(bool horizontal)
    {
        _horizontal = horizontal;
        _handlePropertyChanged = HandlePropertyChanged;
    }

    public IValueCodec? Codec => null;

    public object? GetValue(object target) => ScrollWriter.Along(((ScrollViewer)target).Offset, _horizontal);

    public void SetValue(object target, double number, Tween tween)
    {
        var scroller = (ScrollViewer)target;
        var writer = ScrollWriter.Of(scroller);
        if (!writer.Owns(this, _horizontal))
        {
            scroller.PropertyChanged -= _handlePropertyChanged;
            scroller.PropertyChanged += _handlePropertyChanged;
            // A first write equal to the offset raises no change, so a running snap is stopped here rather than by the write.
            if (ScrollContainer.Containers.TryGetValue(scroller, out var container)) container.WaitToSnap();
        }
        writer.Write(this, _horizontal, number);
        if (tween._currentTime >= tween._changeDuration) Release(scroller);
    }

    public void Revert(object target, object? originalValue)
    {
        var scroller = (ScrollViewer)target;
        // The revert has just rendered the tween's start, so that write is undone.
        ScrollWriter.Of(scroller).Undo(this, _horizontal);
        Release(scroller);
    }

    /// <summary>Lets go of <paramref name="scroller"/> without restarting its snap waits.</summary>
    public void Detach(ScrollViewer scroller)
    {
        scroller.PropertyChanged -= _handlePropertyChanged;
        ScrollWriter.Of(scroller).Release(this);
    }

    private void Release(ScrollViewer scroller)
    {
        Detach(scroller);
        if (ScrollContainer.Containers.TryGetValue(scroller, out var container)) container.WaitToSnap();
    }

    private void HandlePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != ScrollViewer.OffsetProperty) return;
        var scroller = (ScrollViewer)sender!;
        var writer = ScrollWriter.Of(scroller);
        var value = ScrollWriter.Along(scroller.Offset, _horizontal);
        if (writer.Wrote(this, _horizontal, value) || writer.Clamped(this, _horizontal, value)) return;
        Release(scroller);
        Presto.Remove(scroller, property: _horizontal ? "ScrollX" : "ScrollY");
    }
}
