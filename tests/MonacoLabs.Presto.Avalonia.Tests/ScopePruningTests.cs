using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Events;
using MonacoLabs.Presto.Text;

namespace MonacoLabs.Presto.Tests;

/// <summary>Bridge revertibles and Presence exit ghosts leave a long-lived scope once reverting them would restore nothing (CORE-SCOPE).</summary>
public sealed class ScopePruningTests : BridgeTestBase
{
    private const int Swaps = 100;

    private static readonly AnimationBuilder RiseIn = Presto.Build().Y(from: 20).Opacity(from: 0).Duration(200).Delay(Presto.Stagger(10));
    private static readonly AnimationBuilder DropOut = Presto.Build().Y(20).Opacity(0).Duration(150).Delay(Presto.Stagger(10));

    private static TextBlock Headline(int i)
    {
        var block = new TextBlock { Text = $"word {i}", FontSize = 20, [TextReveal.PiecesProperty] = TextPieces.Chars };
        Presence.SetArrive(block, RiseIn);
        Presence.SetExit(block, DropOut);
        return block;
    }

    private static Image[] Ghosts(Window window) =>
        OverlayLayer.GetOverlayLayer(window)!.Children.SelectMany(host => host.GetVisualDescendants()).OfType<Image>().ToArray();

    private void Swap(Scope scope, Decorator slot, int i)
    {
        scope.Execute(_ => { slot.Child = Headline(i); });
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(400, 16);
    }

    [AvaloniaFact]
    public void ARevertedSplitIsPrunedFromItsScope()
    {
        var block = TextRevealTests.Block(TextPieces.Chars);
        TextRevealTests.Show(block);
        var scope = new Scope();
        var split = scope.Execute(_ => PrestoAvalonia.SplitText(block, new SplitTextParams { Words = null, Chars = new() }));
        split.Revert();

        for (var count = scope.RevertibleCount; count < 32; count++) scope.Register(new Scope().Revert());

        Assert.Equal(0, scope.RevertibleCount);
    }

    [AvaloniaFact]
    public void RevertedBridgeRevertiblesArePrunedFromTheirScope()
    {
        var boxes = Enumerable.Range(0, 5).Select(_ => Box()).ToArray();
        var content = new Border { Height = 600 };
        var scroller = new ScrollViewer { Content = content };
        var panel = new StackPanel { Children = { scroller } };
        panel.Children.AddRange(boxes);
        Show(panel);
        var scope = new Scope();
        var revertibles = scope.Execute(_ =>
        {
            PrestoAvalonia.PointerValue(boxes[4], Orientation.Horizontal);
            return new IRevertible[]
            {
                PrestoAvalonia.CreateDraggable(boxes[0]),
                PrestoAvalonia.CreateTransformable(boxes[1]),
                PrestoAvalonia.OnScroll(new ScrollObserverParams { Target = content, Container = scroller }),
                boxes[2].AnimateLayout(100, Eases.None),
                PrestoAvalonia.AnimateContentSize(panel, 100),
                boxes[3].AnimateOnCompositor(new() { ["Opacity"] = 0, Duration = 100 }),
            };
        });
        Frames.Advance(50, 16);

        foreach (var revertible in revertibles) revertible.Revert();
        panel.Children.Remove(boxes[4]);
        for (var count = scope.RevertibleCount; count < 32; count++) scope.Register(new Scope().Revert());

        Assert.Equal(0, scope.RevertibleCount);
    }

    [AvaloniaFact]
    public void TextSwapsWithPresenceInsideAScopeStayBounded()
    {
        var slot = new Border();
        var window = Show(slot);
        var scope = new Scope();

        for (var i = 0; i < Swaps; i++) Swap(scope, slot, i);
        Assert.InRange(scope.RevertibleCount, 0, 63);

        scope.Execute(_ => { slot.Child = Headline(Swaps); });
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(50, 16);
        var arriving = (TextBlock)slot.Child!;
        Assert.NotNull(arriving.Inlines);
        Assert.NotEmpty(Ghosts(window));

        scope.Revert();
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(Ghosts(window));
        Assert.Null(arriving.Inlines);
        Assert.Equal($"word {Swaps}", arriving.Text);
    }

    [AvaloniaFact]
    public void SwappedOutBlocksAreCollectableWhileTheScopeLives()
    {
        var slot = new Border();
        var window = Show(slot);
        var scope = new Scope();

        var swappedOut = SwapOut(scope, slot, window);
        Frames.Advance(400, 16);
        Dispatcher.UIThread.RunJobs();
        // The compositor holds removed visuals until a frame it renders has committed their removal.
        window.CaptureRenderedFrame()?.Dispose();
        window.CaptureRenderedFrame()?.Dispose();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.All(swappedOut, reference => Assert.False(reference.IsAlive, "the scope kept a swapped-out block or ghost alive"));
        GC.KeepAlive(scope);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private List<WeakReference> SwapOut(Scope scope, Decorator slot, Window window)
    {
        var references = new List<WeakReference>();
        for (var i = 0; i < 4; i++)
        {
            var leaving = slot.Child;
            scope.Execute(_ => { slot.Child = Headline(i); });
            Dispatcher.UIThread.RunJobs();
            if (leaving != null) references.Add(new WeakReference(leaving));
            foreach (var ghost in Ghosts(window)) references.Add(new WeakReference(ghost));
            Frames.Advance(400, 16);
        }
        // Released entries leave at the next prune, which runs once the list reaches a power of two.
        for (int i = 4, before = 0; i < 100 && scope.RevertibleCount >= before; i++)
        {
            before = scope.RevertibleCount;
            Swap(scope, slot, i);
        }
        return references;
    }
}
