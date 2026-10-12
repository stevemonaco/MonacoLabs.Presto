using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Path = Avalonia.Controls.Shapes.Path;
using MonacoLabs.Presto;
using static MonacoLabs.Presto.PrestoKeys;

namespace PrestoGallery.Showcase;

/// <summary>
/// A page's printed code, syntax highlighted, with a Copy button and an optional collapsible header.
/// The <c>compact</c> class strips it to bare highlighted code for tiles.
/// </summary>
/// <remarks>
/// The Copy button is a bare two-sheet icon at rest, since a page shows many. Hover peels the back sheet away; a copy folds
/// it in, morphs the front sheet into a check while a "Copied" label springs open beside it, and plays it all back.
/// </remarks>
public sealed class CodePanel : TemplatedControl
{
    public static readonly StyledProperty<string?> CodeProperty = AvaloniaProperty.Register<CodePanel, string?>(nameof(Code));

    public static readonly StyledProperty<CodeLanguage> LanguageProperty = AvaloniaProperty.Register<CodePanel, CodeLanguage>(nameof(Language));

    public static readonly StyledProperty<string?> HeaderProperty = AvaloniaProperty.Register<CodePanel, string?>(nameof(Header));

    public static readonly StyledProperty<bool> IsExpandedProperty = AvaloniaProperty.Register<CodePanel, bool>(nameof(IsExpanded), true);

    public static readonly Geometry FrontSheet = Geometry.Parse("M 2.5,5.5 L 10.5,5.5 L 10.5,14.5 L 2.5,14.5 Z");

    public static readonly Geometry BackSheet = Geometry.Parse("M 5.5,5.5 L 5.5,1.5 L 13.5,1.5 L 13.5,10.5 L 10.5,10.5");

    public static readonly AnimationBuilder PressIn = Presto.Build().Scale(.88).Duration(110).Ease(Eases.OutQuad);

    internal const double Spread = 2;

    private static readonly Geometry Check = Geometry.Parse("M 2.5,8.5 L 6.5,12.5 L 13.5,3.5");
    private static readonly IBrush HoverFill = new ImmutableSolidColorBrush(Palette.Border.Color);
    private static readonly IBrush CopiedFill = new ImmutableSolidColorBrush(Color.FromArgb(0x26, 0x2D, 0xD4, 0xBF));
    private static readonly IBrush CopiedEdge = new ImmutableSolidColorBrush(Color.FromArgb(0x80, 0x2D, 0xD4, 0xBF));

    private SelectableTextBlock? _text;
    private Button? _copy;
    private Path? _front;
    private Path? _back;
    private Border? _copiedClip;
    private TextBlock? _copied;
    private Timeline? _copying;

    public string? Code
    {
        get => GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    public CodeLanguage Language
    {
        get => GetValue(LanguageProperty);
        set => SetValue(LanguageProperty, value);
    }

    /// <summary>A title above the code; only a panel with one can be collapsed.</summary>
    public string? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _text = e.NameScope.Find<SelectableTextBlock>("PART_Text");
        _copy = e.NameScope.Find<Button>("PART_Copy");
        _front = e.NameScope.Find<Path>("PART_CopyFront");
        _back = e.NameScope.Find<Path>("PART_CopyBack");
        _copiedClip = e.NameScope.Find<Border>("PART_CopiedClip");
        _copied = e.NameScope.Find<TextBlock>("PART_Copied");
        if (_copy is not null)
        {
            _copy.Click += Copy;
            _copy.PointerEntered += (_, _) => Hover(true);
            _copy.PointerExited += (_, _) => Hover(false);
        }
        Highlight();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CodeProperty || change.Property == LanguageProperty) Highlight();
    }

    private void Highlight()
    {
        if (_text is null) return;
        var inlines = new InlineCollection();
        var code = Code ?? "";
        foreach (var (start, length, kind) in CodeHighlighter.Tokenize(code, Language))
        {
            var run = new Run(code.Substring(start, length));
            if (Brush(kind) is { } brush) run.Foreground = brush;
            inlines.Add(run);
        }
        _text.Inlines = inlines;
    }

    private static IBrush? Brush(CodeTokenKind kind) => kind switch
    {
        CodeTokenKind.Keyword => Palette.Violet,
        CodeTokenKind.String or CodeTokenKind.AttributeValue => Palette.Amber,
        CodeTokenKind.Number => Palette.Lime,
        CodeTokenKind.Comment => Palette.InkDim,
        CodeTokenKind.Tag => Palette.AccentBright,
        CodeTokenKind.Attribute => Palette.Aqua,
        CodeTokenKind.MarkupExtension => Palette.Violet,
        _ => null,
    };

    private async void Copy(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;
        try
        {
            await clipboard.SetTextAsync(Code ?? "");
        }
        catch (COMException)
        {
            // Another process holds the Windows clipboard (CLIPBRD_E_CANT_OPEN); a missed copy beats a crash.
            return;
        }
        PlayCopied();
    }

    private void Hover(bool over)
    {
        if (_copy is null || _front is null || _back is null || _copying is { Completed: false }) return;
        var ink = TweenValue.Of(over ? Palette.Ink : Palette.InkDim);
        _copy.Animate(new() { ["Background"] = TweenValue.Of(over ? HoverFill : Brushes.Transparent), Duration = 180 });
        _front.Animate(new() { ["Stroke"] = ink, Duration = 180 });
        // Whole device pixels at 100/150/200% and no overshoot: a sub-pixel bounce makes the thin stroke's anti-aliasing shimmer.
        _back.Animate(new()
        {
            ["Stroke"] = ink,
            [X] = over ? Spread : 0,
            [Y] = over ? -Spread : 0,
            Duration = over ? 320 : 220,
            Ease = over ? Eases.OutCubic : Eases.OutQuad,
        });
    }

    // Copying again while the check shows restarts its hold rather than replaying.
    private void PlayCopied()
    {
        if (_copy is null || _front is null || _back is null || _copiedClip is null || _copied is null) return;
        if (_copying is { Completed: false } running)
        {
            var (shown, leaving) = (running.Labels["shown"], running.Labels["leave"]);
            if (running.CurrentTime > shown && running.CurrentTime < leaving) running.Seek(shown);
            return;
        }
        var reduced = Presto.Defaults.ReducedMotion;
        double Move(double ms) => reduced ? 0 : ms;
        _copied.Measure(Size.Infinity);
        _copying = Presto.CreateTimeline(new TimelineParams { OnComplete = _ => Hover(_copy.IsPointerOver) })
            .Add(_back, new() { ["Opacity"] = 0, [Scale] = .6, [X] = 0, [Y] = 0, Duration = Move(160), Ease = Eases.OutQuad })
            .Add(_front, new() { [Data] = PrestoAvalonia.MorphTo(Check), ["Stroke"] = TweenValue.Of(Palette.Aqua), Duration = Move(280), Ease = Eases.OutCubic }, "<<")
            .Add(_front, Presto.Build().Scale(k => k.To(1.3, duration: Move(120)).To(1, duration: Move(420), ease: Eases.OutBack(3))), "<<+=60")
            .Add(_copy, new() { ["Background"] = TweenValue.Of(CopiedFill), ["BorderBrush"] = TweenValue.Of(CopiedEdge), Duration = 220 }, "<<")
            .Add(_copiedClip, new() { ["Width"] = _copied.DesiredSize.Width, Duration = Move(520), Ease = reduced ? Eases.None : Eases.Spring(bounce: .35, duration: 520) }, "<<")
            .Add(_copied, new() { ["Opacity"] = 1, [X] = TweenValue.FromTo(Move(10), 0), Duration = 260, Ease = Eases.OutCubic }, "<<+=60")
            .Label("shown")
            .Label("leave", "+=1100")
            .Add(_copied, new() { ["Opacity"] = 0, [X] = Move(-6), Duration = 160, Ease = Eases.InQuad }, "leave")
            .Add(_copiedClip, new() { ["Width"] = 0, Duration = Move(300), Ease = Eases.InOutQuint }, "leave+=60")
            .Add(_front, new() { [Data] = PrestoAvalonia.MorphTo(FrontSheet), ["Stroke"] = TweenValue.Of(Palette.InkDim), Duration = Move(320), Ease = Eases.InOutCubic }, "leave")
            .Add(_copy, new() { ["Background"] = TweenValue.Of(Brushes.Transparent), ["BorderBrush"] = TweenValue.Of(Brushes.Transparent), Duration = 300 }, "leave")
            .Add(_back, new() { ["Opacity"] = 1, [Scale] = 1, Duration = Move(320), Ease = Eases.OutBack(2) }, "leave+=180");
    }
}
