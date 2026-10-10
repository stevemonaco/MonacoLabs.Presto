using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Diagnostics;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace MonacoLabs.Presto.Tests;

/// <summary>The <c>{presto:Follow}</c> markup extension (GAPS-PLAN §1.2 decisions 1, 9, 10 and 12).</summary>
public sealed class FollowMarkupTests : BridgeTestBase
{
    private static (FollowXamlView View, LevelViewModel Model) ShowView()
    {
        var model = new LevelViewModel();
        var view = new FollowXamlView { DataContext = model };
        Show(view);
        return (view, model);
    }

    private static Border Rendered(Control host) => host.GetVisualDescendants().OfType<Border>().Single(border => border.Height == 20);

    [AvaloniaFact]
    public void TheFirstValueSnapsAndAViewModelChangeAnimates()
    {
        var (view, model) = ShowView();
        var border = view.FindControl<Border>("Default")!;

        Assert.Equal(20, border.Width);
        Assert.Equal(BindingPriority.LocalValue, border.GetDiagnostic(Layoutable.WidthProperty).Priority);
        model.Level = 120;
        Frames.Advance(96, 16);
        Assert.InRange(border.Width, 21, 119);
        Frames.Advance(2000, 16);
        Assert.Equal(120, border.Width);
    }

    [AvaloniaTheory]
    [InlineData("Sprung")]
    [InlineData("Timed")]
    [InlineData("Built")]
    [InlineData("Tokened")]
    public void TheTimingAttributesAreHonored(string name)
    {
        FollowParams options = name switch
        {
            "Sprung" => new() { Spring = "Snappy" },
            "Timed" => new() { Duration = 300, Ease = "inQuad" },
            "Built" => new() { Duration = 300, Ease = "linear" },
            _ => new() { Token = MotionTokens.Expressive },
        };
        var model = new LevelViewModel();
        var view = new FollowXamlView { DataContext = model };
        var reference = new Border { Height = 4, HorizontalAlignment = HorizontalAlignment.Left, DataContext = model };
        Show(new StackPanel { Children = { view, reference } });
        Follow.Bind(reference, Layoutable.WidthProperty, new Binding(nameof(LevelViewModel.Level)), options);

        model.Level = 120;
        Frames.Advance(160, 16);

        var border = view.FindControl<Border>(name)!;
        Assert.InRange(border.Width, 21, 119);
        Assert.Equal(reference.Width, border.Width);
        Assert.NotEqual(view.FindControl<Border>("Default")!.Width, border.Width);
    }

    [AvaloniaFact]
    public void InsideAControlTemplateItFollowsTheTemplatedParent()
    {
        var (view, _) = ShowView();
        var templated = view.FindControl<ContentControl>("Templated")!;
        var part = templated.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_Animated");

        Assert.Equal(44, part.Width);
        templated.MinHeight = 80;
        Assert.Equal(44, part.Width);
        Frames.Advance(2000, 16);
        Assert.Equal(80, part.Width);
    }

    [AvaloniaFact]
    public void InAnItemTemplateEachRowSnapsToItsItemAnimatesItsChangesAndSnapsToANewItem()
    {
        var row = (IDataTemplate)new FollowXamlView().Resources["Row"]!;
        var item = new LevelViewModel { Level = 30 };
        var list = new ItemsControl { ItemsSource = new[] { item }, ItemTemplate = row };
        var host = new ContentControl { ContentTemplate = row, Content = item };
        Show(new StackPanel { Children = { list, host } });

        Assert.Equal((30, 30), (Rendered(list).Width, Rendered(host).Width));
        item.Level = 90;
        Frames.Advance(48, 16);
        Assert.InRange(Rendered(list).Width, 31, 89);
        host.Content = new LevelViewModel { Level = 5 };
        Assert.Equal(5, Rendered(host).Width);
    }

    [AvaloniaFact]
    public void ElementNameAndAConverterResolveInTheWrappedBinding()
    {
        var (view, _) = ShowView();
        var named = view.FindControl<Border>("Named")!;

        Assert.Equal((30, 40), (named.Width, view.FindControl<Border>("Converted")!.Width));
        view.FindControl<Slider>("Source")!.Value = 60;
        Assert.Equal(30, named.Width);
        Frames.Advance(2000, 16);
        Assert.Equal(60, named.Width);
    }

    [AvaloniaFact]
    public void AStyleSetterThrowsAndPointsAtTemplates()
    {
        var extension = new FollowExtension(new Binding(nameof(LevelViewModel.Level)));

        var error = Assert.Throws<InvalidOperationException>(() => extension.ProvideValue(new ProvidedTarget(new Setter(), nameof(Setter.Value))));

        Assert.Contains("ControlTemplate", error.Message);
        Assert.Contains("Follow.Bind", Assert.Throws<InvalidOperationException>(() => extension.ProvideValue(null!)).Message);
    }
}
