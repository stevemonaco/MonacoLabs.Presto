using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Media.Transformation;
using Avalonia.VisualTree;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// Accordion motion for an <see cref="Expander"/> or a <see cref="TreeViewItem"/>: an eased body, a chevron that springs
/// round, and, through <see cref="GroupProperty"/>, sections of which only one is open.
/// </summary>
/// <remarks>
/// The body faded is Fluent's <c>ExpanderContent</c>, or a tree item's <c>PART_ItemsPresenter</c>. Under
/// <see cref="PrestoDefaults.ReducedMotion"/> the item opens and closes at once and the chevron jumps.
/// </remarks>
public static class Accordion
{
    /// <summary>Gives an <see cref="Expander"/> or a <see cref="TreeViewItem"/> its accordion motion.</summary>
    public static readonly AttachedProperty<bool> EnabledProperty =
        AvaloniaProperty.RegisterAttached<TemplatedControl, bool>("Enabled", typeof(Accordion));

    /// <summary>
    /// Makes the element a single-open group: expanding an <see cref="Expander"/> inside it collapses every other one whose
    /// nearest group it is.
    /// </summary>
    public static readonly AttachedProperty<bool> GroupProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("Group", typeof(Accordion));

    /// <summary>
    /// Marks an element in an <see cref="Expander"/>'s or a <see cref="TreeViewItem"/>'s template, at any depth of nested
    /// templates, as its chevron, turned by this many degrees while the item is expanded and back to 0 while it is collapsed.
    /// </summary>
    public static readonly AttachedProperty<double?> ChevronProperty =
        AvaloniaProperty.RegisterAttached<Control, double?>("Chevron", typeof(Accordion));

    private static readonly AttachedProperty<Body?> BodyProperty =
        AvaloniaProperty.RegisterAttached<TemplatedControl, Body?>("Body", typeof(Accordion));

    private static readonly AttachedProperty<ChevronLink?> ChevronLinkProperty =
        AvaloniaProperty.RegisterAttached<Control, ChevronLink?>("ChevronLink", typeof(Accordion));

    static Accordion()
    {
        EnabledProperty.Changed.AddClassHandler<TemplatedControl>(HandleEnabledChanged);
        ChevronProperty.Changed.AddClassHandler<Control>(HandleChevronChanged);
        Expander.IsExpandedProperty.Changed.AddClassHandler<Expander>(HandleExpanded);
    }

    /// <summary>Gets whether <paramref name="item"/> has its accordion motion.</summary>
    public static bool GetEnabled(TemplatedControl item) => item.GetValue(EnabledProperty);

    /// <summary>Sets whether <paramref name="item"/> has its accordion motion.</summary>
    public static void SetEnabled(TemplatedControl item, bool value) => item.SetValue(EnabledProperty, value);

    /// <summary>Gets whether <paramref name="control"/> keeps only one of its expanders open.</summary>
    public static bool GetGroup(Control control) => control.GetValue(GroupProperty);

    /// <summary>Sets whether <paramref name="control"/> keeps only one of its expanders open.</summary>
    public static void SetGroup(Control control, bool value) => control.SetValue(GroupProperty, value);

    /// <summary>Gets the degrees <paramref name="chevron"/> turns while its item is expanded.</summary>
    public static double? GetChevron(Control chevron) => chevron.GetValue(ChevronProperty);

    /// <summary>Sets the degrees <paramref name="chevron"/> turns while its item is expanded.</summary>
    public static void SetChevron(Control chevron, double? value) => chevron.SetValue(ChevronProperty, value);

    private static bool IsExpanded(Control item) => item is Expander expander ? expander.IsExpanded : ((TreeViewItem)item).IsExpanded;

    private static void HandleEnabledChanged(TemplatedControl item, AvaloniaPropertyChangedEventArgs e)
    {
        if (Design.IsDesignMode) return;
        item.GetValue(BodyProperty)?.Release();
        item.ClearValue(BodyProperty);
        if (GetEnabled(item)) item.SetValue(BodyProperty, new Body(item));
    }

    private static void HandleExpanded(Expander expander, AvaloniaPropertyChangedEventArgs e)
    {
        if (!expander.IsExpanded || GroupOf(expander) is not { } group) return;
        foreach (var other in group.GetLogicalDescendants().OfType<Expander>())
        {
            if (other != expander && GroupOf(other) == group) other.IsExpanded = false;
        }
    }

    private static ILogical? GroupOf(Expander expander) =>
        expander.GetLogicalAncestors().FirstOrDefault(ancestor => ancestor is Control control && GetGroup(control));

    private static void HandleChevronChanged(Control chevron, AvaloniaPropertyChangedEventArgs e)
    {
        if (Design.IsDesignMode) return;
        if (e.OldValue is null)
        {
            chevron.AttachedToVisualTree += HandleChevronAttached;
            chevron.DetachedFromVisualTree += HandleChevronDetached;
        }
        else if (e.NewValue is null)
        {
            chevron.AttachedToVisualTree -= HandleChevronAttached;
            chevron.DetachedFromVisualTree -= HandleChevronDetached;
        }
        Unlink(chevron);
        if (e.NewValue is not null && chevron.IsAttachedToVisualTree()) Link(chevron);
    }

    private static void HandleChevronAttached(object? sender, VisualTreeAttachmentEventArgs e) => Link((Control)sender!);

    private static void HandleChevronDetached(object? sender, VisualTreeAttachmentEventArgs e) => Unlink((Control)sender!);

    private static void Link(Control chevron)
    {
        for (var parent = chevron.TemplatedParent; parent is not null; parent = (parent as StyledElement)?.TemplatedParent)
        {
            if (parent is not (Expander or TreeViewItem)) continue;
            chevron.SetValue(ChevronLinkProperty, new ChevronLink(chevron, (Control)parent));
            return;
        }
    }

    private static void Unlink(Control chevron)
    {
        chevron.GetValue(ChevronLinkProperty)?.Dispose();
        chevron.ClearValue(ChevronLinkProperty);
    }

    private sealed class ChevronLink : IDisposable
    {
        private readonly Control _chevron;
        private readonly Control _item;

        public ChevronLink(Control chevron, Control item)
        {
            _chevron = chevron;
            _item = item;
            item.PropertyChanged += HandleItemChanged;
            PrestoAvalonia.Set(chevron, new() { ["Rotate"] = Angle() });
        }

        public void Dispose() => _item.PropertyChanged -= HandleItemChanged;

        private double Angle() => IsExpanded(_item) ? GetChevron(_chevron) ?? 0 : 0;

        private void HandleItemChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property != Expander.IsExpandedProperty && e.Property != TreeViewItem.IsExpandedProperty) return;
            var angle = Angle();
            _chevron.Animate(b => b.Rotate(angle).Ease(Springs.Snappy).WhenReduced(reduced => reduced.Rotate(angle).Duration(0).Ease(Eases.None)));
        }
    }

    // Release takes back exactly what the constructor and FindParts set.
    private sealed class Body
    {
        private readonly TemplatedControl _item;
        private Control? _part;
        private ToggleButton? _header;
        private Control? _chevron;
        private Control? _chevronPath;

        public Body(TemplatedControl item)
        {
            _item = item;
            var tokens = Recipes.Tokens;
            Animate.SetContentSize(item, Presto.Build().Duration(tokens.Short).Ease(tokens.Ease).WhenReduced(reduced => reduced.Duration(0)));
            item.TemplateApplied += HandleTemplateApplied;
            FindParts();
        }

        public void Release()
        {
            _item.TemplateApplied -= HandleTemplateApplied;
            _item.ClearValue(Animate.ContentSizeProperty);
            ReleaseParts();
        }

        private void HandleTemplateApplied(object? sender, TemplateAppliedEventArgs e) => FindParts();

        private void FindParts()
        {
            ReleaseParts();
            _part = Part(_item, _item is Expander ? "ExpanderContent" : "PART_ItemsPresenter");
            if (_part is not null)
            {
                var tokens = Recipes.Tokens;
                Presence.SetExit(_part, Presto.Build().Opacity(0).Duration(tokens.Short).Ease(tokens.Ease).WhenReduced(reduced => reduced.Opacity(0).Duration(0)));
                Presence.SetEnterOnShow(_part, true);
            }
            _header = Part(_item, "ExpanderHeader") as ToggleButton;
            if (_header is null) return;
            _header.TemplateApplied += HandleHeaderTemplateApplied;
            FindChevron(_header);
        }

        private void HandleHeaderTemplateApplied(object? sender, TemplateAppliedEventArgs e)
        {
            ReleaseChevron();
            FindChevron((ToggleButton)sender!);
        }

        // Fluent turns the path by animating its RotateTransform, which a TransformOperations render transform opts out of.
        private void FindChevron(ToggleButton header)
        {
            _chevron = Part(header, "ExpandCollapseChevronBorder");
            _chevronPath = Part(header, "ExpandCollapseChevron");
            if (_chevron is null || _chevronPath is null) return;
            _chevronPath.RenderTransform = TransformOperations.Identity;
            SetChevron(_chevron, 180);
        }

        private void ReleaseParts()
        {
            if (_part is not null)
            {
                _part.ClearValue(Presence.ExitProperty);
                _part.ClearValue(Presence.EnterOnShowProperty);
                _part = null;
            }
            if (_header is not null) _header.TemplateApplied -= HandleHeaderTemplateApplied;
            _header = null;
            ReleaseChevron();
        }

        private void ReleaseChevron()
        {
            _chevron?.ClearValue(ChevronProperty);
            _chevronPath?.ClearValue(Visual.RenderTransformProperty);
            _chevron = _chevronPath = null;
        }

        private static Control? Part(TemplatedControl owner, string name) =>
            owner.GetVisualDescendants().OfType<Control>().FirstOrDefault(child => child.Name == name && child.TemplatedParent == owner);
    }
}
