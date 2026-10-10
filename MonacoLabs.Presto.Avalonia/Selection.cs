using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Compositor;

namespace MonacoLabs.Presto;

/// <summary>
/// One selection indicator per list, a pill behind the selected item or a bar along its edge, that glides to each new
/// selection. Set on a <see cref="SelectingItemsControl"/> (a <see cref="ListBox"/>, <see cref="TabStrip"/> or
/// <see cref="TabControl"/>'s headers) or on a <see cref="TreeView"/>.
/// </summary>
/// <remarks>
/// The indicator lives in the list's <see cref="ItemsPresenter"/>, behind its panel, so it scrolls with the items and
/// outlasts their virtualization. It is placed on the selected container (a <see cref="TreeViewItem"/>'s
/// <c>PART_LayoutRoot</c> row), measured from the container's parent so a pressed item's render transform does not move
/// it. Only a selection change glides it; a resize, a scroll into view or a <see cref="TreeView"/> section expanding
/// places it at once. It hides while the selected container is not realized, not effectively visible, or not wholly
/// inside a clipping ancestor below the presenter, such as a folding section. Under
/// <see cref="PrestoDefaults.ReducedMotion"/>, read at each selection change, it moves at once.
/// </remarks>
public static class Selection
{
    /// <summary>Builds the indicator; null (the default) shows none. Its <c>Margin</c> insets it from the item.</summary>
    public static readonly AttachedProperty<ITemplate<Control?>?> IndicatorProperty =
        AvaloniaProperty.RegisterAttached<ItemsControl, ITemplate<Control?>?>("Indicator", typeof(Selection));

    /// <summary>Whether the indicator fills the selected item (the default) or underlines it.</summary>
    public static readonly AttachedProperty<SelectionIndicatorMode> IndicatorModeProperty =
        AvaloniaProperty.RegisterAttached<ItemsControl, SelectionIndicatorMode>("IndicatorMode", typeof(Selection));

    /// <summary>
    /// Whether the indicator stretches as it glides, its leading edge running ahead and its trailing edge catching up, as
    /// Material's tab indicator does.
    /// </summary>
    public static readonly AttachedProperty<bool> StretchProperty =
        AvaloniaProperty.RegisterAttached<ItemsControl, bool>("Stretch", typeof(Selection));

    /// <summary>
    /// The glide's timing and ease, the builder's only; null (the default) glides over <see cref="MotionTokens.Medium"/>
    /// with <see cref="MotionTokens.Ease"/>. A spring sets the duration.
    /// </summary>
    public static readonly AttachedProperty<AnimationBuilder?> MotionProperty =
        AvaloniaProperty.RegisterAttached<ItemsControl, AnimationBuilder?>("Motion", typeof(Selection));

    private static readonly AttachedProperty<Tracker?> TrackerProperty =
        AvaloniaProperty.RegisterAttached<ItemsControl, Tracker?>("Tracker", typeof(Selection));

    static Selection() => IndicatorProperty.Changed.AddClassHandler<ItemsControl>(HandleIndicatorChanged);

    /// <summary>Gets the template that builds the selection indicator of <paramref name="list"/>.</summary>
    public static ITemplate<Control?>? GetIndicator(ItemsControl list) => list.GetValue(IndicatorProperty);

    /// <summary>Sets the template that builds the selection indicator of <paramref name="list"/>, or null for none.</summary>
    public static void SetIndicator(ItemsControl list, ITemplate<Control?>? value) => list.SetValue(IndicatorProperty, value);

    /// <summary>Gets whether the indicator of <paramref name="list"/> fills or underlines the selected item.</summary>
    public static SelectionIndicatorMode GetIndicatorMode(ItemsControl list) => list.GetValue(IndicatorModeProperty);

    /// <summary>Sets whether the indicator of <paramref name="list"/> fills or underlines the selected item.</summary>
    public static void SetIndicatorMode(ItemsControl list, SelectionIndicatorMode value) => list.SetValue(IndicatorModeProperty, value);

    /// <summary>Gets whether the indicator of <paramref name="list"/> stretches as it glides.</summary>
    public static bool GetStretch(ItemsControl list) => list.GetValue(StretchProperty);

    /// <summary>Sets whether the indicator of <paramref name="list"/> stretches as it glides.</summary>
    public static void SetStretch(ItemsControl list, bool value) => list.SetValue(StretchProperty, value);

    /// <summary>Gets the timing and ease of the glide of <paramref name="list"/>'s indicator.</summary>
    public static AnimationBuilder? GetMotion(ItemsControl list) => list.GetValue(MotionProperty);

    /// <summary>Sets the timing and ease of the glide of <paramref name="list"/>'s indicator.</summary>
    public static void SetMotion(ItemsControl list, AnimationBuilder? value) => list.SetValue(MotionProperty, value);

    internal static Control? IndicatorOf(ItemsControl list) => list.GetValue(TrackerProperty)?.Indicator;

    private static void HandleIndicatorChanged(ItemsControl list, AvaloniaPropertyChangedEventArgs e)
    {
        if (Design.IsDesignMode) return;
        list.GetValue(TrackerProperty)?.Release();
        if (GetIndicator(list)?.Build() is { } indicator) list.SetValue(TrackerProperty, new Tracker(list, indicator));
        else list.ClearValue(TrackerProperty);
    }

    // The panel's children are the generator's, by index, so the indicator joins the presenter behind the panel instead.
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_VisualChildren")]
    internal static extern IAvaloniaList<Visual> VisualChildren(Visual visual);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_LogicalChildren")]
    internal static extern IAvaloniaList<ILogical> LogicalChildren(StyledElement element);

    private sealed class Tracker
    {
        private readonly ItemsControl _list;
        private readonly Layer _layer;
        private Animation? _glide;
        private Rect? _placed;
        private Control? _row;

        public Tracker(ItemsControl list, Control indicator)
        {
            _list = list;
            Indicator = indicator;
            _layer = new Layer(indicator);
            list.AddHandler(SelectingItemsControl.SelectionChangedEvent, HandleSelectionChanged);
            list.LayoutUpdated += HandleLayoutUpdated;
            list.DetachedFromVisualTree += HandleDetached;
        }

        public Control Indicator { get; }

        public void Release()
        {
            _list.RemoveHandler(SelectingItemsControl.SelectionChangedEvent, HandleSelectionChanged);
            _list.LayoutUpdated -= HandleLayoutUpdated;
            _list.DetachedFromVisualTree -= HandleDetached;
            _glide?.Cancel();
            if (_layer.GetVisualParent() is ItemsPresenter presenter) Unhost(presenter);
        }

        private void HandleSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(e.Source, _list)) return;
            _row = null;
            Place(glide: true);
        }

        private void HandleLayoutUpdated(object? sender, EventArgs e) => Place(glide: false);

        private void HandleDetached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            _glide?.Cancel();
            _placed = null;
        }

        private void Place(bool glide)
        {
            if (_list.Presenter is not { } presenter) return;
            if (_layer.GetVisualParent() != presenter) Host(presenter);
            if (Target(presenter) is not { } target)
            {
                _glide?.Cancel();
                _placed = null;
                _layer.IsVisible = false;
                return;
            }
            if (target == _placed && _layer.IsVisible) return;
            _placed = target;
            glide &= _layer.IsVisible && !Engine.Current.Defaults.ReducedMotion;
            _layer.IsVisible = true;
            if (glide) Glide(target, Horizontal(presenter));
            else
            {
                _glide?.Cancel();
                _layer.Slot = target;
                _layer.InvalidateArrange();
            }
        }

        private void Glide(Rect target, bool horizontal)
        {
            var motion = GetMotion(_list) is { } builder ? LayoutMotion.From(builder) : new LayoutMotion(Recipes.Tokens.Medium);
            var ease = (motion.Ease ?? Recipes.Tokens.Ease).Resolve(_layer, 0, 1, out var spring);
            var from = _layer.Slot;
            var travel = horizontal ? target.Center.X - from.Center.X : target.Center.Y - from.Center.Y;
            Ease leads = ease, trails = ease;
            // Material's elastic tab indicator: over one duration the leading edge starts fast and the trailing edge slow.
            if (GetStretch(_list) && travel != 0)
            {
                leads = t => ease(Math.Sin(t * Math.PI / 2));
                trails = t => ease(1 - Math.Cos(t * Math.PI / 2));
            }
            var (start, end) = travel > 0 ? (trails, leads) : (leads, trails);
            _glide?.Cancel();
            _glide = Presto.Animate(_layer.Edges, new AnimationParams
            {
                ["Left"] = Edge(target.Left, horizontal ? start : ease),
                ["Right"] = Edge(target.Right, horizontal ? end : ease),
                ["Top"] = Edge(target.Top, horizontal ? ease : start),
                ["Bottom"] = Edge(target.Bottom, horizontal ? ease : end),
                Duration = spring?.SettlingDuration ?? motion.Duration,
                Delay = motion.Delay,
                OnRender = _ => _layer.InvalidateArrange(),
            });
        }

        private static TweenParams Edge(double to, Ease ease) => new() { To = to, Ease = ease };

        private Rect? Target(ItemsPresenter presenter)
        {
            var row = _list switch
            {
                TreeView tree => _row ??= tree.SelectedItem is { } item && tree.TreeContainerFromItem(item) is TreeViewItem container
                    ? container.GetTemplateDescendants().OfType<Control>().FirstOrDefault(part => part.Name == "PART_LayoutRoot") ?? container
                    : null,
                SelectingItemsControl list => list.SelectedIndex >= 0 ? list.ContainerFromIndex(list.SelectedIndex) : null,
                _ => null,
            };
            if (row is not { IsEffectivelyVisible: true } || Clipped(row, presenter)
                || row.GetVisualParent()?.TranslatePoint(row.Bounds.Position, presenter) is not { } at) return null;
            var slot = new Rect(at, row.Bounds.Size);
            if (GetIndicatorMode(_list) == SelectionIndicatorMode.Fill) return slot;
            Indicator.Measure(Size.Infinity);
            var thickness = Indicator.DesiredSize;
            return Horizontal(presenter)
                ? new Rect(slot.X, slot.Bottom - thickness.Height, slot.Width, thickness.Height)
                : new Rect(slot.X, slot.Y, thickness.Width, slot.Height);
        }

        private static bool Clipped(Control row, ItemsPresenter presenter)
        {
            var corner = new Point(row.Bounds.Width, row.Bounds.Height);
            for (var host = row.GetVisualParent(); host is not null && host != presenter; host = host.GetVisualParent())
            {
                if (host.ClipToBounds && row.TranslatePoint(corner, host) is { } inside
                    && (inside.X > host.Bounds.Width + .5 || inside.Y > host.Bounds.Height + .5)) return true;
            }
            return false;
        }

        private static bool Horizontal(ItemsPresenter presenter) => presenter.Panel switch
        {
            StackPanel panel => panel.Orientation == Orientation.Horizontal,
            VirtualizingStackPanel panel => panel.Orientation == Orientation.Horizontal,
            WrapPanel panel => panel.Orientation == Orientation.Horizontal,
            _ => false,
        };

        private void Host(ItemsPresenter presenter)
        {
            if (_layer.GetVisualParent() is ItemsPresenter old) Unhost(old);
            VisualChildren(presenter).Insert(0, _layer);
            LogicalChildren(presenter).Add(_layer);
        }

        private void Unhost(ItemsPresenter presenter)
        {
            VisualChildren(presenter).Remove(_layer);
            LogicalChildren(presenter).Remove(_layer);
        }
    }

    /// <summary>Spans the presenter and lays the indicator out at <see cref="Slot"/>, taking no size of its own.</summary>
    private sealed class Layer : Control
    {
        private readonly Control _indicator;

        public Layer(Control indicator)
        {
            _indicator = indicator;
            IsHitTestVisible = false;
            IsVisible = false;
            VisualChildren.Add(indicator);
            LogicalChildren.Add(indicator);
        }

        public Edges Edges { get; } = new();

        public Rect Slot
        {
            get => new(Edges.Left, Edges.Top, Edges.Right - Edges.Left, Edges.Bottom - Edges.Top);
            set => (Edges.Left, Edges.Top, Edges.Right, Edges.Bottom) = (value.Left, value.Top, value.Right, value.Bottom);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            _indicator.Measure(Size.Infinity);
            return default;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _indicator.Arrange(Slot);
            return finalSize;
        }
    }

    private sealed class Edges
    {
        public double Left { get; set; }

        public double Top { get; set; }

        public double Right { get; set; }

        public double Bottom { get; set; }
    }
}
