using System.Globalization;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// <see cref="Accordion"/> (Phase 64d): two Fluent expanders in a single-open group, each with a 60-high red body whose
/// content fades in when it enters, in a white 200x200 window.
/// </summary>
public sealed class AccordionTests : CompositorTestBase
{
    [AvaloniaFact]
    public void TheBodyHeightEasesOpen()
    {
        var (first, _) = Stage();
        var closed = first.Bounds.Height;
        var heights = new List<double>();

        first.IsExpanded = true;
        for (var i = 0; i < 30; i++)
        {
            Run(1);
            heights.Add(first.Bounds.Height);
        }

        Assert.True(heights[^1] >= closed + 60, $"the expander ended {heights[^1]} high from {closed}");
        Assert.Contains(heights, height => height > closed + 1 && height < heights[^1] - 1);
        for (var i = 1; i < heights.Count; i++) Assert.True(heights[i] >= heights[i - 1] - .01, $"Frame {i} shrank from {heights[i - 1]} to {heights[i]}.");
    }

    [AvaloniaFact]
    public void CollapsingFadesTheBodyInPlaceThenFoldsIt()
    {
        var (first, _) = Stage();
        var closed = first.Bounds.Height;
        first.IsExpanded = true;
        Run(40);
        var open = first.Bounds.Height;
        var body = Part(first, "ExpanderContent");

        first.IsExpanded = false;
        Run(3);
        Assert.True(body.IsVisible);
        Assert.InRange(body.Opacity, .01, .99);
        Assert.Equal(open, first.Bounds.Height, 0.5);

        Run(40);
        Assert.False(body.IsVisible);
        Assert.Equal(closed, first.Bounds.Height, 0.5);
    }

    [AvaloniaFact]
    public void ExpandingOneCollapsesItsSibling()
    {
        var (first, second) = Stage();
        first.IsExpanded = true;

        second.IsExpanded = true;

        Assert.False(first.IsExpanded);
        Assert.True(second.IsExpanded);
    }

    [AvaloniaFact]
    public void TheChevronSpringsRoundInPlaceOfFluentsFlip()
    {
        var (first, _) = Stage();
        var chevron = Chevron(first);
        Assert.Equal(0, Rotation(chevron), 0.5);
        Assert.IsType<TransformOperations>(Part(Header(first), "ExpandCollapseChevron").RenderTransform);

        first.IsExpanded = true;
        Run(3);
        Assert.InRange(Rotation(chevron), 1, 179);
        Run(40);
        Assert.Equal(180, Rotation(chevron), 0.5);

        first.IsExpanded = false;
        Run(40);
        Assert.Equal(0, Rotation(chevron), 0.5);
    }

    [AvaloniaFact]
    public void TheBodysChildrenEnterEachTimeItShows()
    {
        var (first, _) = Stage();
        var content = (Control)first.Content!;

        for (var round = 0; round < 2; round++)
        {
            first.IsExpanded = true;
            Run(3);
            Assert.InRange(content.Opacity, .01, .99);
            Run(40);
            Assert.Equal(1, content.Opacity);
            first.IsExpanded = false;
            Run(40);
        }
    }

    [AvaloniaFact]
    public void UnderReducedMotionItOpensAndClosesAtOnce()
    {
        Presto.Defaults.ReducedMotion = true;
        var (first, _) = Stage();
        var closed = first.Bounds.Height;
        var body = Part(first, "ExpanderContent");

        first.IsExpanded = true;
        Run(2);
        Assert.True(first.Bounds.Height >= closed + 60);
        Assert.Equal(180, Rotation(Chevron(first)), 0.5);

        first.IsExpanded = false;
        Run(2);
        Assert.False(body.IsVisible);
        Assert.Equal(closed, first.Bounds.Height, 0.5);
        Assert.Equal(0, Rotation(Chevron(first)), 0.5);
    }

    [AvaloniaFact]
    public void TheChevronIsFoundOnASemiShapedTemplate()
    {
        var expander = SemiStage();
        var icon = Part(expander, "PART_PathIcon");
        Assert.Equal(180, Accordion.GetChevron(icon));

        expander.IsExpanded = true;
        Run(3);
        Assert.InRange(Rotation(icon), 1, 179);
        Run(40);
        Assert.Equal(180, Rotation(icon), 0.5);
    }

    [AvaloniaFact]
    public void ALinkedChevronLiftsItsRenderTransformTransitionsAndUnlinkRestoresThem()
    {
        var expander = SemiStage();
        var icon = Part(expander, "PART_PathIcon");
        Assert.IsType<DoubleTransition>(Assert.Single(icon.Transitions!));

        expander.IsExpanded = true;
        Run(40);
        Assert.IsType<DoubleTransition>(Assert.Single(icon.Transitions!));

        Accordion.SetEnabled(expander, false);
        Assert.Null(Accordion.GetChevron(icon));
        Assert.Equal(2, icon.Transitions!.Count);
    }

    [AvaloniaFact]
    public void AnAppSetChevronIsKept()
    {
        var marked = new Style(x => x.OfType<Expander>().Template().OfType<PathIcon>().Name("PART_PathIcon"))
        {
            Setters = { new Setter(Accordion.ChevronProperty, 90d) },
        };
        var expander = SemiStage(marked);
        var icon = Part(expander, "PART_PathIcon");

        expander.IsExpanded = true;
        Run(40);
        Assert.Equal(90, Rotation(icon), 0.5);

        Accordion.SetEnabled(expander, false);
        Assert.Equal(90, Accordion.GetChevron(icon));
    }

    private static (Expander First, Expander Second) Stage()
    {
        var group = new StackPanel { Children = { Section(), Section() } };
        Accordion.SetGroup(group, true);
        ShowWhite(group);
        return ((Expander)group.Children[0], (Expander)group.Children[1]);
    }

    private static Expander Section()
    {
        var content = new Border { Height = 60, Background = Brushes.Red };
        Presence.SetEnter(content, Recipes.Fade());
        var expander = new Expander { Header = "h", Content = content, HorizontalAlignment = HorizontalAlignment.Stretch };
        Accordion.SetEnabled(expander, true);
        return expander;
    }

    // Like Semi's: the chevron is PART_PathIcon in the expander's template, given as its header toggle's content, with a
    // RenderTransform transition set there.
    private static Expander SemiStage(params IStyle[] styles)
    {
        var theme = new ControlTheme(typeof(Expander))
        {
            Setters =
            {
                new Setter(TemplatedControl.TemplateProperty, new FuncControlTemplate<Expander>((_, _) => new ToggleButton
                {
                    Name = "ExpanderHeader",
                    Content = new Grid
                    {
                        Children =
                        {
                            new PathIcon
                            {
                                Name = "PART_PathIcon",
                                Transitions =
                                [
                                    new TransformOperationsTransition { Property = Visual.RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(250) },
                                    new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(250) },
                                ],
                            },
                        },
                    },
                })),
            },
        };
        var expander = new Expander { Theme = theme };
        Accordion.SetEnabled(expander, true);
        var root = new StackPanel { Children = { expander } };
        root.Styles.AddRange(styles);
        ShowWhite(root);
        return expander;
    }

    private static ToggleButton Header(Expander expander) => (ToggleButton)Part(expander, "ExpanderHeader");

    private static Control Chevron(Expander expander) => Part(Header(expander), "ExpandCollapseChevronBorder");

    private static Control Part(Visual visual, string name) => visual.GetVisualDescendants().OfType<Control>().First(part => part.Name == name);

    private static double Rotation(Control chevron) =>
        double.Parse(Convert.ToString(PrestoAvalonia.Get(chevron, "Rotate"), CultureInfo.InvariantCulture)!.Replace("deg", ""), CultureInfo.InvariantCulture);
}
