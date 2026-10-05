using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Monaco.Presto.AvaloniaBridge.Events;

/// <summary>
/// Scrolls a list on a <see cref="VirtualizingStackPanel"/> to an item that may not be realized yet (decision 87): jumps to a
/// viewport short of a far item, animates the last stretch aimed at the item's measured slot once it realizes, and settles exactly.
/// </summary>
internal sealed class IndexScroller
{
    private const double JumpViewports = 2;
    private const int MaxPasses = 3;

    private readonly ItemsControl _list;
    private readonly VirtualizingStackPanel _panel;
    private readonly ScrollViewer _scroller;
    private readonly bool _horizontal;
    private readonly object? _item;
    private ScrollAlignment _alignment;
    private int _index;

    public IndexScroller(ItemsControl list, VirtualizingStackPanel panel, ScrollViewer scroller, int index, ScrollAlignment alignment)
    {
        _list = list;
        _panel = panel;
        _scroller = scroller;
        _horizontal = panel.Orientation == Orientation.Horizontal;
        _item = list.Items[index];
        _index = index;
        _alignment = alignment;
        // Fixed to the edge the item comes in at, or the offset would stop wherever a moving estimate first shows the item.
        if (alignment == ScrollAlignment.Nearest && Requested() is var nearest && nearest != Offset)
            _alignment = nearest < Offset ? ScrollAlignment.Start : ScrollAlignment.End;
    }

    private double Offset => ScrollWriter.Along(_scroller.Offset, _horizontal);

    private double Viewport => ScrollWriter.Along(_scroller.Viewport, _horizontal);

    private double Extent => ScrollWriter.Along(_scroller.Extent, _horizontal);

    public Animation Start(AnimationParams p)
    {
        for (var pass = 0; pass < MaxPasses && Requested() is var near && Math.Abs(near - Offset) > JumpViewports * Viewport; pass++)
        {
            // A jump clear of the realized items places the first one at offset ÷ average size, so aim in those coordinates.
            var size = AverageSize();
            var index = Index();
            var far = Aligned(index * size, (index + 1) * size, _alignment);
            Write(near > Offset ? far - Viewport : far + Viewport);
            _list.UpdateLayout();
        }
        var from = Offset;
        var to = Requested();
        var properties = new OrderedDictionary<string, TweenValue>(p.Properties);
        if (to != from) properties[_horizontal ? "ScrollX" : "ScrollY"] = ScrollMotion.Tween(_scroller, _horizontal, from, Requested);
        var animation = Presto.Animate(_scroller, ScrollMotion.Apply(
            p with
            {
                Properties = properties,
                OnComplete = timer =>
                {
                    Settle();
                    p.OnComplete?.Invoke(timer);
                },
            },
            Math.Abs(to - from) / Viewport));
        if (to == from) animation.Complete();
        return animation;
    }

    private void Settle()
    {
        for (var pass = 0; pass < MaxPasses; pass++)
        {
            _list.UpdateLayout();
            var to = Requested();
            if (to == Offset) return;
            Write(to);
        }
    }

    private void Write(double offset) =>
        ScrollWriter.Of(_scroller).Write(this, _horizontal, offset);

    private double Requested()
    {
        var index = Index();
        double start, end;
        if (_list.ContainerFromIndex(index) is { } container)
        {
            var slot = ScrollGeometry.Slot(container, _scroller);
            (start, end) = (ScrollWriter.Along(slot.TopLeft, _horizontal), ScrollWriter.Along(slot.BottomRight, _horizontal));
        }
        else
        {
            var size = AverageSize();
            var anchor = index < _panel.FirstRealizedIndex ? _panel.FirstRealizedIndex : _panel.LastRealizedIndex;
            start = (_list.ContainerFromIndex(anchor) is { } realized ? ScrollWriter.Along(ScrollGeometry.Slot(realized, _scroller).TopLeft, _horizontal) : 0) + (index - anchor) * size;
            end = start + size;
        }
        if (index == 0) start = 0;
        if (index == _list.ItemCount - 1) end = Extent;
        return Aligned(start, end, _alignment);
    }

    private double Aligned(double start, double end, ScrollAlignment alignment)
    {
        var rect = _horizontal ? new Rect(start, 0, end - start, 0) : new Rect(0, start, 0, end - start);
        return ScrollWriter.Along(ScrollGeometry.Target(_scroller, rect, alignment, default), _horizontal);
    }

    private double AverageSize()
    {
        // The panel takes the mean of the realized containers for every unrealized item.
        var total = 0d;
        for (var i = _panel.FirstRealizedIndex; i <= _panel.LastRealizedIndex; i++)
            if (_list.ContainerFromIndex(i) is { } container) total += ScrollWriter.Along(container.Bounds.Size, _horizontal);
        return _panel.FirstRealizedIndex < 0 ? Extent / _list.ItemCount : total / (_panel.LastRealizedIndex - _panel.FirstRealizedIndex + 1);
    }

    private int Index()
    {
        // Re-found when inserts or removals before the item have moved it.
        if (_index < _list.ItemCount && Equals(_list.Items[_index], _item)) return _index;
        var found = _list.Items.IndexOf(_item);
        return found >= 0 ? _index = found : _index;
    }

}
