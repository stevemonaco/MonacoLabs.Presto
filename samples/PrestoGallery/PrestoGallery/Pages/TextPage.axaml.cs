using Avalonia.Interactivity;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Text;
using PrestoGallery.Showcase;

namespace PrestoGallery.Pages;

public partial class TextPage : ShowcasePage
{
    private TextSplitter? _split;

    public TextPage() => InitializeComponent();

    protected override void OnAttached()
    {
        #region snippet TextSplit
        _split = PrestoAvalonia.SplitText(Headline, new SplitTextParams
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
        });
        #endregion
    }

    private void OnRefresh(object? sender, RoutedEventArgs e) => _split?.Refresh();

    private void OnScramble(object? sender, RoutedEventArgs e)
    {
        #region snippet TextScramble
        Transmission.Animate(new()
        {
            ["Text"] = Presto.ScrambleText(new ScrambleTextParams
            {
                Chars = "shades",
                From = ScrambleFrom.Center,
                Cursor = "_",
            }),
        });
        #endregion
        ScrambleCode.Code = Snippets.TextScramble;
    }

    private void OnChangeText(object? sender, RoutedEventArgs e)
    {
        #region snippet TextChangeText
        Transmission.Animate(new()
        {
            ["Text"] = Presto.ScrambleText(new ScrambleTextParams
            {
                Text = "Signal acquired",
                Chars = "blocks",
            }),
        });
        #endregion
        ScrambleCode.Code = Snippets.TextChangeText;
    }

    private void OnReplay(object? sender, RoutedEventArgs e)
    {
        var cards = RevealHost.Child;
        RevealHost.Child = null;
        RevealHost.Child = cards;
    }
}
