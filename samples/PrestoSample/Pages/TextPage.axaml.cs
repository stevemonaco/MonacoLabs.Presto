using Avalonia.Controls;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Text;

namespace PrestoSample.Pages;

public partial class TextPage : UserControl
{
    private readonly TextSplitter _split;

    public TextPage()
    {
        InitializeComponent();

        _split = PrestoAvalonia.CreateScope(this).Execute(_ => PrestoAvalonia.SplitText(Headline, new SplitTextParams
        {
            Lines = new SplitPieceParams { Wrap = true },
            Chars = new SplitPieceParams { Class = "split-char" },
        })
        .AddEffect(split =>
        {
            var animation = Presto.Animate(split.Chars, new AnimationParams
            {
                ["Y"] = TweenValue.FromTo(44, 0),
                ["Opacity"] = TweenValue.FromTo(0, 1),
                Duration = 700,
                Ease = Eases.OutExpo,
                Delay = Presto.Stagger(18),
            });
            return () => animation.Revert();
        }));
    }

    private void OnRefresh(object? sender, RoutedEventArgs e) => _split.Refresh();

    private void OnScramble(object? sender, RoutedEventArgs e) =>
        Transmission.Animate(new()
        {
            ["Text"] = PrestoAvalonia.ScrambleText(new ScrambleTextParams
            {
                Chars = "shades",
                From = ScrambleFrom.Center,
                Cursor = "_",
            }),
        });

    private void OnChangeText(object? sender, RoutedEventArgs e) =>
        Transmission.Animate(new()
        {
            ["Text"] = PrestoAvalonia.ScrambleText(new ScrambleTextParams { Text = "Signal acquired", Chars = "blocks" }),
        });

    private void OnReplay(object? sender, RoutedEventArgs e)
    {
        var cards = RevealHost.Child;
        RevealHost.Child = null;
        RevealHost.Child = cards;
    }
}
