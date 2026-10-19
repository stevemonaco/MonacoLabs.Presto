using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Tempo.Views;

namespace Tempo.Tests;

public sealed class SafeAreaTests : TempoTestBase
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompactInsetsPadTheBarsWhileTheBackdropFillsTheView(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced, new Size(360, 780), "Write");
        TestFrames.Tick(60);
        var shell = Find<ShellView>(window, "Shell");
        var nav = Find<ListBox>(window, "BottomNav");
        var wordmark = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("wordmark"));
        var navWas = Top(nav, shell);
        var wordmarkWas = Top(wordmark, shell);

        shell.SafeArea = new Thickness(0, 24, 0, 34);
        TestFrames.Tick(30);

        Assert.Equal(navWas - 34, Top(nav, shell), 1);
        Assert.Equal(wordmarkWas + 24, Top(wordmark, shell), 1);
        Assert.Equal(shell.Bounds.Height, Find<Border>(window, "BottomBar").Bounds.Bottom, 1);
        var backdrop = window.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("backdrop"));
        Assert.Equal(new Rect(shell.Bounds.Size), backdrop.Bounds);

        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaFact]
    public async Task WideSideInsetsPushThePanelsIn()
    {
        var (window, vm) = await ShowAsync(reduced: true, new Size(1100, 760));
        var shell = Find<ShellView>(window, "Shell");
        var panels = Find<DockPanel>(window, "Panels");
        var was = panels.Bounds;

        shell.SafeArea = new Thickness(44, 0, 30, 0);
        TestFrames.Tick(30);

        Assert.Equal(was.Left + 44, panels.Bounds.Left, 1);
        Assert.Equal(was.Right - 30, panels.Bounds.Right, 1);

        window.Close();
        await vm.FlushAsync();
    }

    private static double Top(Visual visual, Visual relativeTo) => visual.TranslatePoint(default, relativeTo)!.Value.Y;
}
