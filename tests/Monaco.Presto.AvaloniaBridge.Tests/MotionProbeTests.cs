using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>Pins the Avalonia 12 behavior <c>Motion</c>'s tracker relies on (MOTION-KIT-PLAN §2.4 step 1).</summary>
public sealed class MotionProbeTests : BridgeTestBase
{
    [AvaloniaFact]
    public void PointerOverIsAColonPrefixedClassThatRaisesCollectionChanged()
    {
        var box = Box();
        var window = Show(box);
        var changes = 0;
        box.Classes.CollectionChanged += (_, _) => changes++;

        window.MouseMove(new Point(25, 25));

        Assert.Contains(":pointerover", box.Classes);
        Assert.Equal(1, changes);

        window.MouseMove(new Point(150, 150));

        Assert.DoesNotContain(":pointerover", box.Classes);
        Assert.Equal(2, changes);
    }

    [AvaloniaFact]
    public void CheckedIsAColonPrefixedClassThatRaisesCollectionChanged()
    {
        var toggle = new ToggleButton();
        Show(toggle);
        var changes = 0;
        toggle.Classes.CollectionChanged += (_, _) => changes++;

        toggle.IsChecked = true;

        Assert.Contains(":checked", toggle.Classes);
        Assert.True(changes > 0);
    }

    [AvaloniaFact]
    public void BaseValueIsTheValueBelowAnimationPriority()
    {
        var box = Box();
        box.Styles.Add(new Style(x => x.OfType<Border>()) { Setters = { new Setter(Visual.OpacityProperty, 0.8) } });
        Show(box);
        AvaloniaProperty property = Visual.OpacityProperty;

        box.SetValue(property, 0.2, BindingPriority.Animation);

        Assert.Equal(0.2, box.Opacity);
        Assert.Equal(0.8, box.GetBaseValue(property));

        box.Opacity = 0.5;

        Assert.Equal(0.5, box.GetBaseValue(property));
    }

    [AvaloniaFact]
    public void BaseValueIsUnsetWhenOnlyTheDefaultAndAnAnimationApply()
    {
        var box = Box();
        Show(box);

        box.SetValue(Visual.OpacityProperty, 0.2, BindingPriority.Animation);

        Assert.Same(AvaloniaProperty.UnsetValue, box.GetBaseValue((AvaloniaProperty)Visual.OpacityProperty));
    }

    [AvaloniaFact]
    public void FocusVisibleFollowsKeyboardFocusButNotPointerFocus()
    {
        var first = new Button { Content = "one" };
        var second = new Button { Content = "two", Width = 80, Height = 30 };
        var window = Show(new StackPanel { Children = { first, second } });

        window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, "\t");

        Assert.True(first.IsFocused);
        Assert.Contains(":focus-visible", first.Classes);

        window.MouseDown(Center(second, window), MouseButton.Left);
        window.MouseUp(Center(second, window), MouseButton.Left);

        Assert.True(second.IsFocused);
        Assert.Contains(":focus", second.Classes);
        Assert.DoesNotContain(":focus-visible", second.Classes);
    }

    [AvaloniaFact]
    public void HandledEventsTooSeesAButtonsOwnPress()
    {
        var button = new Button { Content = "press", Width = 80, Height = 30 };
        var window = Show(button);
        bool? handled = null;
        button.AddHandler(InputElement.PointerPressedEvent, (_, e) => handled = e.Handled, RoutingStrategies.Bubble, handledEventsToo: true);

        window.MouseDown(Center(button, window), MouseButton.Left);

        Assert.True(handled);
        Assert.Contains(":pressed", button.Classes);
    }
}
