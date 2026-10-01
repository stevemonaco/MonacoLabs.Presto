using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Core;
using Avalonia.Diagnostics;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// The Avalonia 12 behaviour Phase 41's value-driven bindings are built on (GAPS-PLAN §1.7): what <c>IProvideValueTarget</c>
/// names, the priority a returned observable binding lands at, how a companion property resolves the wrapped binding, and
/// the order a <c>DataContext</c> change and the value it causes arrive in.
/// </summary>
public sealed class FollowBindingPlumbingTests : BridgeTestBase
{
    private static (FollowBindingXamlView View, LevelViewModel Model) ShowView()
    {
        var model = new LevelViewModel();
        var view = new FollowBindingXamlView { DataContext = model };
        var window = Show(view);
        window.UpdateLayout();
        return (view, model);
    }

    private static Border Named(Control root, string name) =>
        root.GetVisualDescendants().OfType<Border>().Single(border => border.Name == name);

    private static BindingPriority PriorityOf(AvaloniaObject target, AvaloniaProperty property) => target.GetDiagnostic(property).Priority;

    [AvaloniaFact]
    public void TheTargetIsTheControlAndItsPropertyOnAnAttributeAndInsideTemplates()
    {
        ProbeExtension.Provided.Clear();
        var (view, _) = ShowView();
        Show(new ContentControl { ContentTemplate = (IDataTemplate)view.Resources["Row"]!, Content = new LevelViewModel() });

        var targets = ProbeExtension.Provided.Where(p => p.TargetObject is Border).ToList();
        Assert.All(targets, p => Assert.Same(Layoutable.WidthProperty, p.TargetProperty));
        var templated = targets.Select(p => (Border)p.TargetObject!).Where(border => border.Name is "PART_Relative" or "PART_TemplateBinding").ToList();
        Assert.Equal(2, templated.Count);
        // Already set when the extension runs, so a template part can be told apart from a plain attribute.
        Assert.All(templated, border => Assert.Same(view.FindControl<ContentControl>("Templated"), border.TemplatedParent));
        Assert.Contains(targets, p => p.TargetObject is Border { Name: null or "", Height: 20 });
    }

    [AvaloniaFact]
    public void InAStyleSetterTheTargetIsTheSharedSetterAndItsValueClrProperty()
    {
        ProbeExtension.Provided.Clear();
        var (view, _) = ShowView();

        var setter = Assert.Single(ProbeExtension.Provided, p => p.TargetObject is Setter);
        Assert.Equal("Value", Assert.IsAssignableFrom<IPropertyInfo>(setter.TargetProperty).Name);
        Assert.Equal(BindingPriority.StyleTrigger, PriorityOf(view.FindControl<Border>("Setter")!, Layoutable.HeightProperty));
    }

    [AvaloniaFact]
    public void AReturnedObservableBindingLandsAtLocalValueOverAStyleAndInsideATemplateLikeABareBinding()
    {
        var (view, _) = ShowView();
        var plain = view.FindControl<Border>("Plain")!;
        var templated = view.FindControl<ContentControl>("Templated")!;

        Assert.Equal(20, plain.Width);
        Assert.Equal(BindingPriority.LocalValue, PriorityOf(plain, Layoutable.WidthProperty));
        Assert.Equal(BindingPriority.LocalValue, PriorityOf(Named(templated, "PART_Relative"), Layoutable.WidthProperty));
        Assert.Equal(BindingPriority.LocalValue, PriorityOf(Named(templated, "PART_Bare"), Layoutable.WidthProperty));
        // Only a literal inside a template is held at Template priority.
        Assert.Equal(BindingPriority.Template, PriorityOf(Named(templated, "PART_Bare"), Layoutable.HeightProperty));
    }

    [AvaloniaFact]
    public void AnAnimationLayersOverTheReturnedBindingAndRevertsToItsLatestValue()
    {
        var (view, model) = ShowView();
        var plain = view.FindControl<Border>("Plain")!;

        var animation = plain.Animate(new() { ["Width"] = 90, Duration = 100, Ease = "linear" });
        Frames.Advance(50, 16);
        model.Level = 30;

        Assert.Equal(BindingPriority.Animation, PriorityOf(plain, Layoutable.WidthProperty));
        Assert.Equal(30, plain.GetBaseValue(Layoutable.WidthProperty).Value);
        Frames.Advance(100, 16);
        Assert.Equal(90, plain.Width);
        animation.Revert();
        Assert.Equal(30, plain.Width);
        Assert.Equal(BindingPriority.LocalValue, PriorityOf(plain, Layoutable.WidthProperty));
    }

    [AvaloniaFact]
    public void TheWrappedBindingResolvesOnTheCompanionAsItWouldOnTheProperty()
    {
        var (view, model) = ShowView();
        var templated = view.FindControl<ContentControl>("Templated")!;

        Assert.Equal(20, view.FindControl<Border>("Reflected")!.Width);
        Assert.Equal(40, view.FindControl<Border>("Converted")!.Width);
        Assert.Equal(12, view.FindControl<Border>("Fallback")!.Width);
        Assert.Equal(33, view.FindControl<Border>("Ancestor")!.Width);
        Assert.Equal(20, view.FindControl<Border>("Inner")!.Width);
        view.FindControl<Slider>("Source")!.Value = 60;
        templated.MinHeight = 50;
        model.Level = 25;

        Assert.Equal(60, view.FindControl<Border>("Named")!.Width);
        Assert.Equal(50, Named(templated, "PART_Relative").Width);
        Assert.Equal(50, Named(templated, "PART_TemplateBinding").Width);
        Assert.Equal(25, view.FindControl<Border>("Reflected")!.Width);
    }

    [AvaloniaFact]
    public void ADataContextChangeIsRaisedBeforeTheCompanionValueItCauses()
    {
        var (view, _) = ShowView();

        ProbeExtension.Log.Clear();
        view.DataContext = new LevelViewModel { Level = 70 };
        view.FindControl<Border>("Outer")!.DataContext = new LevelViewModel { Level = 71 };
        view.FindControl<Border>("Inner")!.DataContext = new LevelViewModel { Level = 72 };

        Assert.Equal(
            ["Inner:DataContextChanged", "Inner:Companion=70", "Inner:DataContextChanged", "Inner:Companion=71", "Inner:DataContextChanged", "Inner:Companion=72"],
            ProbeExtension.Log.Where(entry => entry.StartsWith("Inner:")));
        Assert.Equal(["Plain:DataContextChanged", "Plain:Companion=70"], ProbeExtension.Log.Where(entry => entry.StartsWith("Plain:")));
    }

    [AvaloniaFact]
    public void AReusedTemplateRootSeesItsNewItemBeforeItsValue()
    {
        var row = (IDataTemplate)new FollowBindingXamlView().Resources["Row"]!;
        var host = new ContentControl { ContentTemplate = row, Content = new LevelViewModel { Level = 1 } };
        var window = Show(host);
        var first = host.GetVisualDescendants().OfType<Border>().Single();

        ProbeExtension.Log.Clear();
        host.Content = new LevelViewModel { Level = 5 };
        window.UpdateLayout();

        Assert.Same(first, host.GetVisualDescendants().OfType<Border>().Single());
        var name = ProbeExtension.NameOf(first);
        Assert.Equal([$"{name}:DataContextChanged", $"{name}:Companion=5"], ProbeExtension.Log);
    }

    [AvaloniaFact]
    public void AVirtualizedListBuildsANewRowPerItemAndNullsTheRowsItRecycles()
    {
        var items = Enumerable.Range(0, 50).Select(i => new LevelViewModel { Level = i }).ToList();
        var list = new ListBox { Height = 100, ItemsSource = items, ItemTemplate = (IDataTemplate)new FollowBindingXamlView().Resources["Row"]! };
        var window = Show(list);
        window.UpdateLayout();
        var before = list.GetVisualDescendants().OfType<Border>().Where(border => border.Height == 20).ToList();

        ProbeExtension.Log.Clear();
        for (var y = 40; y <= 400; y += 40)
        {
            list.Scroll!.Offset = new Vector(0, y);
            window.UpdateLayout();
        }

        var after = list.GetVisualDescendants().OfType<Border>().Where(border => border.Height == 20).ToList();
        Assert.DoesNotContain(after, before.Contains);
        // A row scrolled away is handed a null as its DataContext clears; every value still follows its DataContextChanged.
        var log = ProbeExtension.Log;
        Assert.All(before, row => Assert.Contains($"{ProbeExtension.NameOf(row)}:Companion=", log));
        for (var i = 0; i < log.Count; i++)
        {
            if (log[i].Contains(":Companion=")) Assert.Equal(log[i][..log[i].IndexOf(':')] + ":DataContextChanged", log[i - 1]);
        }
    }

    [AvaloniaFact]
    public void OnlyRemovingTheBindingExpressionUnsubscribesTheFollower()
    {
        var (view, model) = ShowView();
        var plain = view.FindControl<Border>("Plain")!;
        var converted = view.FindControl<Border>("Converted")!;
        var reflected = view.FindControl<Border>("Reflected")!;

        ProbeExtension.Log.Clear();
        plain.ClearValue(Layoutable.WidthProperty);
        plain.Width = 5;
        model.Level = 31;
        Assert.Equal(31, plain.Width);
        Assert.DoesNotContain(ProbeExtension.Log, entry => entry.EndsWith(":Unsubscribed"));

        BindingOperations.GetBindingExpressionBase(converted, Layoutable.WidthProperty)!.Dispose();
        reflected.Bind(Layoutable.WidthProperty, new Binding(nameof(LevelViewModel.Level)));
        Assert.Equal(["Converted:Unsubscribed", "Reflected:Unsubscribed"], ProbeExtension.Log.Where(entry => entry.EndsWith(":Unsubscribed")));
    }
}
