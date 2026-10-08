using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Monaco.Presto.AvaloniaBridge.Compositor;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// <see cref="LayoutAnimateMode.PositionAndSizeCorrected"/>: a blue card doubling in size from 40x20 at (20, 20) keeps its
/// red 10x10 avatar at its size and at the same alignment inside the card as it is drawn.
/// </summary>
/// <remarks>The pixel probes count only exact colors, so an edge blended on a fractional pixel can lose one pixel.</remarks>
public sealed class CorrectedLayoutTransitionTests : CompositorTestBase
{
    private const double Duration = 800;
    private const double Avatar = 10;

    [AvaloniaFact]
    public void APlainGrowShrinksTheChildMidFlight()
    {
        var (card, _) = ShowCard(HorizontalAlignment.Left, VerticalAlignment.Top);
        _ = card.AnimateLayout(Duration, Eases.None, LayoutAnimateMode.PositionAndSize);

        Grow(card);
        var (_, halfway) = Halfway(card);

        Assert.InRange(halfway.Width, 0, Avatar * .9);
    }

    [AvaloniaTheory]
    [InlineData(HorizontalAlignment.Left, VerticalAlignment.Top)]
    [InlineData(HorizontalAlignment.Center, VerticalAlignment.Center)]
    [InlineData(HorizontalAlignment.Right, VerticalAlignment.Bottom)]
    public void TheChildKeepsItsAlignmentInsideTheCardAsItIsDrawn(HorizontalAlignment horizontal, VerticalAlignment vertical)
    {
        var (card, avatar) = ShowCard(horizontal, vertical);
        _ = card.AnimateLayout(Duration, Eases.None, LayoutAnimateMode.PositionAndSizeCorrected);

        Grow(card);
        var (drawn, halfway) = Halfway(card);

        AssertAligned(drawn, halfway, horizontal, vertical);
        Assert.True(Pump(() => Extent(card, Colors.Blue) == new Rect(20, 20, 80, 40)), "the card never reached its layout size");
        Assert.Equal(SlotOf(avatar), Box(card));
    }

    [AvaloniaFact]
    public async Task TheChildIsBackAtIdentityWithNothingDrivingItOnceTheMoveLands()
    {
        var (card, avatar) = ShowCard(HorizontalAlignment.Center, VerticalAlignment.Center);
        var transition = card.AnimateLayout(200, Eases.None, LayoutAnimateMode.PositionAndSizeCorrected);

        Grow(card);
        Tick();
        Assert.Same(transition, CompositorRegistry.Holder(avatar, CompositorTrackKind.Scale));
        Frames.Advance(400);
        await transition.Then();

        AssertReleased(avatar);
        Assert.True(Pump(() => Box(card) == SlotOf(avatar)), "the avatar did not land in its slot");
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancelingOrRevertingMidFlightRestoresTheChild(bool revert)
    {
        var (card, avatar) = ShowCard(HorizontalAlignment.Center, VerticalAlignment.Center);
        var transition = card.AnimateLayout(Held, Eases.None, LayoutAnimateMode.PositionAndSizeCorrected);
        Grow(card);
        Tick();
        Assert.Same(transition, CompositorRegistry.Holder(avatar, CompositorTrackKind.Scale));

        if (revert) transition.Revert();
        else transition.Cancel();
        Tick();

        AssertReleased(avatar);
        Assert.Equal(SlotOf(avatar), Box(card));
        Assert.Equal(new Rect(20, 20, 80, 40), Extent(card, Colors.Blue));
    }

    [AvaloniaFact]
    public void AGrowInterruptedMidFlightKeepsTheCorrection()
    {
        var (card, _) = ShowCard(HorizontalAlignment.Center, VerticalAlignment.Center);
        _ = card.AnimateLayout(Duration, Eases.None, LayoutAnimateMode.PositionAndSizeCorrected);
        Grow(card);
        Halfway(card);

        card.Width = 100;
        card.Height = 60;
        var (drawn, halfway) = Halfway(card);

        AssertAligned(drawn, halfway, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    [AvaloniaFact]
    public void APositionOnlyMoveLeavesTheChildAlone()
    {
        var (card, avatar) = ShowCard(HorizontalAlignment.Left, VerticalAlignment.Top);
        _ = card.AnimateLayout(Held, Eases.None, LayoutAnimateMode.PositionAndSizeCorrected);

        Canvas.SetLeft(card, 40);
        Tick();

        Assert.Null(CompositorRegistry.Holder(avatar, CompositorTrackKind.Scale));
    }

    [AvaloniaFact]
    public void ACorrectedGrowTakesTheScaleOverFromACompositorAnimationOnTheChild()
    {
        var (card, avatar) = ShowCard(HorizontalAlignment.Center, VerticalAlignment.Center);
        _ = avatar.AnimateOnCompositor(new() { ["Scale"] = 2, Duration = 100, Ease = "linear" });
        Assert.True(Pump(() => Box(card).Width == Avatar * 2), "the avatar never scaled up");
        var transition = card.AnimateLayout(Duration, Eases.None, LayoutAnimateMode.PositionAndSizeCorrected);

        Grow(card);
        var (_, halfway) = Halfway(card);

        Assert.Same(transition, CompositorRegistry.Holder(avatar, CompositorTrackKind.Scale));
        AssertAvatarSized(halfway);
    }

    /// <summary>A blue 40x20 card at (20, 20) holding a red 10x10 avatar, in a 140x100 window ticked once so both have composition visuals.</summary>
    private static (Border Card, Border Avatar) ShowCard(HorizontalAlignment horizontal, VerticalAlignment vertical)
    {
        var avatar = new Border { Background = Brushes.Red, Width = Avatar, Height = Avatar, HorizontalAlignment = horizontal, VerticalAlignment = vertical };
        var card = new Border { Background = Brushes.Blue, Width = 40, Height = 20, Child = avatar };
        Canvas.SetLeft(card, 20);
        Canvas.SetTop(card, 20);
        new Window { Content = new Canvas { Children = { card } }, Background = Brushes.White, Width = 140, Height = 100 }.Show();
        Tick();
        return (card, avatar);
    }

    private static void Grow(Border card)
    {
        card.Width = 80;
        card.Height = 40;
    }

    /// <summary>The card's and the avatar's boxes in the first frame past halfway through a <see cref="Duration"/> move, ticking on to 70% of it.</summary>
    private static (Rect Card, Rect Avatar) Halfway(Visual card)
    {
        TickFor(Duration / 2 + Frame);
        var halfway = (Extent(card, Colors.Blue)!.Value, Box(card));
        TickFor(Duration * .2 - Frame);
        return halfway;
    }

    private static void AssertAvatarSized(Rect avatar)
    {
        Assert.InRange(avatar.Width, Avatar - 1, Avatar + 1);
        Assert.InRange(avatar.Height, Avatar - 1, Avatar + 1);
    }

    private static void AssertAligned(Rect card, Rect avatar, HorizontalAlignment horizontal, VerticalAlignment vertical)
    {
        AssertAvatarSized(avatar);
        Assert.InRange(card.Width, 41, 79);
        var x = horizontal switch
        {
            HorizontalAlignment.Left => card.Left,
            HorizontalAlignment.Right => card.Right - Avatar,
            _ => card.Center.X - (Avatar / 2),
        };
        var y = vertical switch
        {
            VerticalAlignment.Top => card.Top,
            VerticalAlignment.Bottom => card.Bottom - Avatar,
            _ => card.Center.Y - (Avatar / 2),
        };
        Assert.InRange(avatar.Left, x - 1, x + 1);
        Assert.InRange(avatar.Top, y - 1, y + 1);
    }

    private static void AssertReleased(Visual avatar)
    {
        Assert.Null(CompositorRegistry.Holder(avatar, CompositorTrackKind.Scale));
        var element = ElementComposition.GetElementVisual(avatar)!;
        Assert.Equal(new Vector3D(1, 1, 1), element.Scale);
        Assert.Equal(default, element.CenterPoint);
    }
}
