using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge.Targets;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// The Phase 57 spike (GAPS-2-PLAN §3.3), kept as pinning tests: what Avalonia 12.1.2 does when <see cref="Visual.IsVisible"/>
/// is held at <see cref="BindingPriority.Animation"/> across a flip underneath, and what the Fluent templates toggle.
/// </summary>
public sealed class VisibilitySpikeTests : CompositorTestBase
{
    [AvaloniaFact]
    public void HoldingIsVisibleInsideItsOwnChangeNotificationRendersNoHiddenFrameAndSettlesLayout()
    {
        var box = new MeasuredBox();
        var below = new Border { Height = 20, Background = Brushes.Blue };
        ShowWhite(new StackPanel { Children = { box, below } });
        var slot = Box(box);
        var hold = new object();
        TrackBaseFlips(box, visible =>
        {
            if (!visible) VisibilityHold.Hold(box, hold, true);
        });
        var measures = box.Measures;

        box.IsVisible = false;
        Assert.True(box.IsVisible);
        Assert.True(box.IsEffectivelyVisible);
        Tick();

        Assert.Equal(slot, Box(box));
        Assert.Equal(50, below.Bounds.Y);
        var settled = box.Measures;
        Assert.InRange(settled - measures, 0, 1);
        Tick();
        Tick();
        Assert.Equal(settled, box.Measures);

        VisibilityHold.Release(box, hold);
        Tick();
        Assert.False(box.IsVisible);
        Assert.Null(Extent(box, Colors.Red));
        Assert.Equal(0, below.Bounds.Y);
    }

    [AvaloniaFact]
    public void AHandlerAddedAfterTheHoldSeesTheNestedShowBeforeTheHideWhileBindingsReadTheCurrentValue()
    {
        var box = Box();
        ShowWhite(box);
        var hold = new object();
        TrackBaseFlips(box, visible =>
        {
            if (!visible) VisibilityHold.Hold(box, hold, true);
        });
        var late = new List<object?>();
        box.PropertyChanged += (_, e) =>
        {
            if (e.Property == Visual.IsVisibleProperty) late.Add(e.NewValue);
        };
        var mirror = new Border();
        mirror.Bind(Visual.IsVisibleProperty, box.GetObservable(Visual.IsVisibleProperty));

        box.IsVisible = false;

        Assert.Equal([true, false], late);
        Assert.True(box.IsVisible);
        Assert.True(mirror.IsVisible);
    }

    [AvaloniaTheory]
    [InlineData(BindingPriority.LocalValue)]
    [InlineData(BindingPriority.StyleTrigger)]
    [InlineData(BindingPriority.Template)]
    public void AValueChangedUnderTheHoldRaisesNothingAndWinsWhenTheHoldIsReleased(BindingPriority source)
    {
        var box = Box();
        box.Styles.Add(new Style(x => x.OfType<Border>().Class("hidden")) { Setters = { new Setter(Visual.IsVisibleProperty, false) } });
        var bound = new Border();
        if (source == BindingPriority.Template) box.Bind(Visual.IsVisibleProperty, bound.GetObservable(Visual.IsVisibleProperty), BindingPriority.Template);
        ShowWhite(box);
        var hold = new object();
        VisibilityHold.Hold(box, hold, true);
        var priorities = Priorities(box);

        switch (source)
        {
            case BindingPriority.LocalValue: box.IsVisible = false; break;
            case BindingPriority.StyleTrigger: box.Classes.Add("hidden"); break;
            default: bound.IsVisible = false; break;
        }
        Tick();

        Assert.Empty(priorities);
        Assert.True(box.IsVisible);
        Assert.False(box.GetBaseValue(Visual.IsVisibleProperty).Value);

        VisibilityHold.Release(box, hold);

        Assert.False(box.IsVisible);
        Assert.Equal([source], priorities);
    }

    [AvaloniaTheory]
    [InlineData(BindingPriority.LocalValue, false)]
    [InlineData(BindingPriority.Template, true)]
    public void TheHoldsOwnNotificationInsideATemplateFlipStillReadsTheOldBaseValue(BindingPriority source, bool nestedBase)
    {
        var box = Box();
        var bound = new Border();
        box.Bind(Visual.IsVisibleProperty, bound.GetObservable(Visual.IsVisibleProperty), source);
        ShowWhite(box);
        var hold = new object();
        var seen = new List<(BindingPriority, bool)>();
        box.PropertyChanged += (_, e) =>
        {
            if (e.Property != Visual.IsVisibleProperty) return;
            seen.Add((e.Priority, BaseVisible(box)));
            if (e.Priority != BindingPriority.Animation && !BaseVisible(box)) VisibilityHold.Hold(box, hold, true);
        };

        bound.IsVisible = false;

        Assert.Equal([(source, false), (BindingPriority.Animation, nestedBase)], seen);
        Assert.False(BaseVisible(box));
    }

    [AvaloniaFact]
    public void ComparingTheBaseValueSeparatesBaseFlipsFromAnimationPriorityHolds()
    {
        var box = Box();
        ShowWhite(box);
        var flips = TrackBaseFlips(box);
        var priorities = Priorities(box);
        var hold = new object();
        var other = new object();

        VisibilityHold.Hold(box, other, false);
        VisibilityHold.Release(box, other);
        box.IsVisible = false;
        VisibilityHold.Hold(box, hold, true);
        VisibilityHold.Release(box, hold);
        box.IsVisible = true;

        Assert.Equal(
            [BindingPriority.Animation, BindingPriority.Unset, BindingPriority.LocalValue, BindingPriority.Animation, BindingPriority.LocalValue, BindingPriority.LocalValue],
            priorities);
        Assert.Equal([false, true], flips);
    }

    [AvaloniaFact]
    public void TheFluentExpanderCollapsesByHidingItsContentBorderThroughTheTemplate()
    {
        var expander = new Expander { Header = "h", Content = new Border { Height = 40, Background = Brushes.Red }, IsExpanded = true };
        ShowWhite(expander);
        var content = Part(expander, "ExpanderContent");
        var presenter = Part(expander, "PART_ContentPresenter");
        var priorities = Priorities(content);

        expander.IsExpanded = false;
        Tick();

        Assert.Null(expander.ContentTransition);
        Assert.Equal([BindingPriority.Template], priorities);
        Assert.False(content.IsVisible);
        Assert.True(presenter.IsVisible);
        Assert.True(((Visual)expander.Content!).IsAttachedToVisualTree());
        Assert.Null(Extent(expander, Colors.Red));
    }

    [AvaloniaFact]
    public void HoldingTheExpanderContentBorderKeepsTheCollapsingContentDrawn()
    {
        var expander = new Expander { Header = "h", Content = new Border { Height = 40, Background = Brushes.Red }, IsExpanded = true };
        ShowWhite(expander);
        var content = Part(expander, "ExpanderContent");
        var drawn = Box(expander);
        var hold = new object();
        TrackBaseFlips(content, visible =>
        {
            if (!visible) VisibilityHold.Hold(content, hold, true);
        });

        expander.IsExpanded = false;
        Tick();
        Assert.Equal(drawn, Box(expander));

        VisibilityHold.Release(content, hold);
        Tick();
        Assert.Null(Extent(expander, Colors.Red));
    }

    [AvaloniaFact]
    public void TheFluentTreeViewItemCollapsesByHidingItsItemsPresenterAndKeepsItsChildrenAttached()
    {
        var section = new TreeViewItem { Header = "s", ItemsSource = new[] { "a", "b" }, IsExpanded = true };
        ShowWhite(new TreeView { Items = { section } });
        var items = Part(section, "PART_ItemsPresenter");
        var children = section.GetRealizedContainers().ToList();
        var priorities = Priorities(items);

        section.IsExpanded = false;
        Tick();

        Assert.Equal([BindingPriority.Template], priorities);
        Assert.False(items.IsVisible);
        Assert.Equal(double.PositiveInfinity, items.MaxHeight);
        Assert.Equal(2, children.Count);
        Assert.All(children, child => Assert.True(child.IsAttachedToVisualTree()));
    }

    [AvaloniaFact]
    public void ASnapshotDrawsAHiddenElementOnlyWhileAHoldShowsIt()
    {
        var box = Box();
        ShowWhite(box);

        box.IsVisible = false;
        Assert.Equal(0, SnapshotPixels(box));

        VisibilityHold.Hold(box, new object(), true);
        Assert.NotEqual(0, SnapshotPixels(box));
    }

    private static Control Part(Control owner, string name) =>
        owner.GetVisualDescendants().OfType<Control>().Single(part => part.Name == name && part.TemplatedParent == owner);

    private static int SnapshotPixels(Visual visual)
    {
        using var snapshot = new RenderTargetBitmap(new PixelSize(50, 50));
        snapshot.Render(visual);
        var pixels = new byte[50 * 50 * 4];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            snapshot.CopyPixels(new PixelRect(0, 0, 50, 50), handle.AddrOfPinnedObject(), pixels.Length, 50 * 4);
        }
        finally
        {
            handle.Free();
        }
        return pixels.Count(b => b != 0);
    }

    private static List<BindingPriority> Priorities(Visual visual)
    {
        var priorities = new List<BindingPriority>();
        visual.PropertyChanged += (_, e) =>
        {
            if (e.Property == Visual.IsVisibleProperty) priorities.Add(e.Priority);
        };
        return priorities;
    }

    private static List<bool> TrackBaseFlips(Visual visual, Action<bool>? flipped = null)
    {
        var flips = new List<bool>();
        var last = BaseVisible(visual);
        visual.PropertyChanged += (_, e) =>
        {
            if (e.Property != Visual.IsVisibleProperty || e.Priority == BindingPriority.Animation || BaseVisible(visual) == last) return;
            last = !last;
            flips.Add(last);
            flipped?.Invoke(last);
        };
        return flips;
    }

    private static bool BaseVisible(Visual visual) => visual.GetBaseValue(Visual.IsVisibleProperty).GetValueOrDefault(true);

    private sealed class MeasuredBox : Border
    {
        public MeasuredBox()
        {
            Width = 50;
            Height = 50;
            Background = Brushes.Red;
            HorizontalAlignment = HorizontalAlignment.Left;
        }

        public int Measures { get; private set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            Measures++;
            return base.MeasureOverride(availableSize);
        }
    }
}
