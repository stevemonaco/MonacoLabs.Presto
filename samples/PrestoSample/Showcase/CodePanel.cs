using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace PrestoSample.Showcase;

/// <summary>
/// A page's printed code, syntax highlighted, with a Copy button and an optional collapsible header.
/// The <c>compact</c> class strips it to bare highlighted code for tiles.
/// </summary>
public sealed class CodePanel : TemplatedControl
{
    public static readonly StyledProperty<string?> CodeProperty = AvaloniaProperty.Register<CodePanel, string?>(nameof(Code));

    public static readonly StyledProperty<CodeLanguage> LanguageProperty = AvaloniaProperty.Register<CodePanel, CodeLanguage>(nameof(Language));

    public static readonly StyledProperty<string?> HeaderProperty = AvaloniaProperty.Register<CodePanel, string?>(nameof(Header));

    public static readonly StyledProperty<bool> IsExpandedProperty = AvaloniaProperty.Register<CodePanel, bool>(nameof(IsExpanded), true);

    private SelectableTextBlock? _text;

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
        e.NameScope.Find<Button>("PART_Copy")?.Click += Copy;
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
        }
    }
}
