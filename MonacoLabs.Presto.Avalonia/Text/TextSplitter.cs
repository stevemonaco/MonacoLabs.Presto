using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Core;
using MonacoLabs.Presto.Diagnostics;
using Path = Avalonia.Controls.Shapes.Path;

namespace MonacoLabs.Presto.Text;

/// <summary>
/// A <see cref="TextBlock"/>'s text taken apart into per-line, per-word and
/// per-character pieces that are animated individually. The pieces are <see cref="Control"/>s laid out inside the block
/// itself, one row per wrapped line, and the split is redone whenever the block's width changes.
/// </summary>
/// <remarks>
/// anime.js's <c>clone</c> and <c>debug</c> options and its HTML templates have no counterpart; the piece tree is built from
/// <see cref="SplitPieceParams"/> instead. Only the block's plain text is split, so inline formatting is not preserved,
/// and the text is left to automation through <see cref="AutomationProperties.NameProperty"/> on the block where
/// anime.js leaves a visually hidden copy. While split, the block's <see cref="Visual.ClipToBounds"/> is off so pieces
/// animated past its edges stay visible.
/// </remarks>
public sealed class TextSplitter : IRevertible
{
    private const double ResizeDebounce = 150;
    private const string Nbsp = " ";

    private readonly TextBlock _host;
    private readonly SplitTextParams _parameters;
    private string _text;
    private readonly Timer _resize;
    // An effect returns either an Action to undo it or a Timer that keeps running, as upstream's effectsCleanups holds both.
    private readonly List<Func<TextSplitter, object?>> _effects = [];
    private readonly List<object?> _cleanups = [];
    private readonly List<Control> _lines = [];
    private readonly List<Control> _words = [];
    private readonly List<Control> _chars = [];
    private StackPanel? _root;
    private IDisposable? _unclip;
    private double _width;
    private double _splitWidth;
    private bool _ready;
    private bool _writing;

    internal TextSplitter(TextBlock host, SplitTextParams parameters)
    {
        if (parameters is { Outlines: true, Chars: null }) throw new ArgumentException("Outlines requires Chars.", nameof(parameters));
        Engine.Current.CurrentScope?.Register(this);
        _host = host;
        _parameters = parameters;
        _text = host.Text ?? "";
        using var origin = PrestoDiagnostics.Origin("TextSplitter");
        _resize = Presto.CreateTimer(new TimerParams
        {
            Duration = ResizeDebounce * Globals.TimeScale,
            Autoplay = false,
            OnComplete = _ =>
            {
                if (_host.Bounds.Width != _width) Split();
            },
        });

        host.SizeChanged += HandleSizeChanged;
        host.PropertyChanged += HandleTextChanged;
        if (host.Bounds.Width > 0) Split();
    }

    /// <summary>
    /// Upstream's <c>data-line</c>: the index of the wrapped line a line, word or character piece sits on, set when the
    /// piece is created so it can be read inside an effect.
    /// </summary>
    public static readonly AttachedProperty<int> LineIndexProperty =
        AvaloniaProperty.RegisterAttached<TextSplitter, Control, int>("LineIndex");

    internal static readonly AttachedProperty<int> IndexInLineProperty =
        AvaloniaProperty.RegisterAttached<TextSplitter, Control, int>("IndexInLine");

    /// <summary>The <see cref="LineHeight"/> of a control standing in for a piece, such as an exit ghost.</summary>
    internal static readonly AttachedProperty<double> LineHeightProperty =
        AvaloniaProperty.RegisterAttached<TextSplitter, Control, double>("LineHeight");

    private static readonly AttachedProperty<string?> GraphemeProperty =
        AvaloniaProperty.RegisterAttached<TextSplitter, Control, string?>("Grapheme");

    private static readonly AttachedProperty<GlyphContours?> ContoursProperty =
        AvaloniaProperty.RegisterAttached<TextSplitter, Control, GlyphContours?>("Contours");

    private static readonly AttachedProperty<TextSplitter?> SplitProperty =
        AvaloniaProperty.RegisterAttached<TextSplitter, TextBlock, TextSplitter?>("Split");

    private static readonly AttachedProperty<TextSplitter?> OwnerProperty =
        AvaloniaProperty.RegisterAttached<TextSplitter, Control, TextSplitter?>("Owner");

    /// <summary>The line pieces, empty unless <see cref="SplitTextParams.Lines"/> is set.</summary>
    public IReadOnlyList<Control> Lines => _lines;

    /// <summary>The word pieces, empty unless <see cref="SplitTextParams.Words"/> is set.</summary>
    public IReadOnlyList<Control> Words => _words;

    /// <summary>The character pieces, empty unless <see cref="SplitTextParams.Chars"/> is set.</summary>
    public IReadOnlyList<Control> Chars => _chars;

    /// <summary>The height of one line of the block's text, measured before the pieces are laid out.</summary>
    public double LineHeight { get; private set; }

    /// <summary>The text the block is split from: its text at creation or the last one set on it from outside since.</summary>
    internal string Text => _text;

    /// <summary>The index of the wrapped line <paramref name="piece"/> sits on.</summary>
    public static int GetLineIndex(Control piece) => piece.GetValue(LineIndexProperty);

    /// <summary>
    /// A delay for split pieces of one kind: <paramref name="start"/> plus the piece's line index times
    /// <paramref name="lineStep"/> plus its position among that line's pieces times <paramref name="pieceStep"/>.
    /// </summary>
    public static FunctionValue StaggerByLine(double lineStep, double pieceStep = 0, double? start = null)
    {
        var offset = start ?? 0;
        return (target, _, _) => target is Control piece
            ? offset + (GetLineIndex(piece) * lineStep) + (piece.GetValue(IndexInLineProperty) * pieceStep)
            : offset;
    }

    /// <summary>The <see cref="LineHeight"/> of the split <paramref name="piece"/> belongs to, or 0 when it is not a split piece.</summary>
    public static double LineHeightOf(Control piece) => piece.GetValue(OwnerProperty)?.LineHeight ?? piece.GetValue(LineHeightProperty);

    /// <summary>The text a character piece stands for, an outline piece's included.</summary>
    internal static string GraphemeOf(Control piece) => piece is TextBlock text ? text.Text! : piece.GetValue(GraphemeProperty)!;

    /// <summary>The flattened outline an outline piece draws, or null for any other control.</summary>
    internal static GlyphContours? ContoursOf(Control piece) => piece.GetValue(ContoursProperty);

    /// <summary>The splitter <paramref name="block"/> is split by, or null when it is not split.</summary>
    internal static TextSplitter? Of(TextBlock block) => block.GetValue(SplitProperty);

    /// <summary>
    /// The layout box of <paramref name="piece"/> in the block's coordinates, ignoring render transforms; valid from the
    /// first effect of the split.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="piece"/> is not a line, word or character of the current split.</exception>
    public Rect SlotOf(Control piece)
    {
        if (!_lines.Contains(piece) && !_words.Contains(piece) && !_chars.Contains(piece))
            throw new ArgumentException("The control is not a piece of this split.", nameof(piece));
        return UncheckedSlotOf(piece);
    }

    /// <summary><see cref="SlotOf"/> for a caller that already holds a piece of the current split.</summary>
    internal Rect UncheckedSlotOf(Control piece)
    {
        var offset = default(Point);
        for (Visual visual = piece; visual != _root; visual = visual.GetVisualParent()!) offset += visual.Bounds.Position;
        return new Rect(offset + _root!.Bounds.Position, piece.Bounds.Size);
    }

    /// <summary>
    /// Registers <paramref name="effect"/> over the pieces, running it at once when they already exist and again after
    /// every re-split.
    /// </summary>
    /// <param name="effect">Returns what to undo before the next run, or null when there is nothing to undo.</param>
    /// <remarks>
    /// A timeline captures its children's targets when they are added, so an effect that builds one over the pieces
    /// builds all of it here and returns it through the <see cref="Timer"/> overload, whose <see cref="Utils.KeepTime"/>
    /// hands the playhead on to the next one.
    /// </remarks>
    public TextSplitter AddEffect(Func<TextSplitter, Action?> effect) => Register(split => effect(split));

    /// <summary>
    /// Registers <paramref name="effect"/> the same way, handing the playback position of the timer it returns to the one
    /// the next re-split creates and reverting the last of them with the splitter.
    /// </summary>
    public TextSplitter AddEffect(Func<TextSplitter, Timer> effect)
    {
        var refreshable = Utils.KeepTime(effect);
        return Register(split => refreshable(split));
    }

    /// <summary>Splits the text again at the current width, running the effect cleanups first and the effects after.</summary>
    public TextSplitter Refresh()
    {
        Split();
        return this;
    }

    /// <summary>Runs the effect cleanups and puts the block's current text back, leaving it whole.</summary>
    public TextSplitter Revert()
    {
        _host.SizeChanged -= HandleSizeChanged;
        _host.PropertyChanged -= HandleTextChanged;
        _resize.Revert();
        RunCleanups(true);
        _lines.Clear();
        _words.Clear();
        _chars.Clear();
        _host.Inlines = null;
        // A splitter reverted before must not write over a text set since.
        if (_ready) _host.SetCurrentValue(TextBlock.TextProperty, _text);
        _host.ClearValue(AutomationProperties.NameProperty);
        if (Of(_host) == this) _host.ClearValue(SplitProperty);
        _unclip?.Dispose();
        _unclip = null;
        _width = 0;
        _ready = false;
        return this;
    }

    void IRevertible.Revert() => Revert();

    private void HandleSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width == _width) return;
        // Pieces measured one by one lose kerning, so a block sized to its text widens to its own split.
        if (_ready && Math.Abs(e.NewSize.Width - _splitWidth) < 1)
        {
            _width = e.NewSize.Width;
            return;
        }
        if (_ready) _resize.Restart(); else Split();
    }

    private void HandleTextChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != TextBlock.TextProperty || _writing) return;
        _text = _host.Text ?? "";
        if (_ready) Split();
    }

    private TextSplitter Register(Func<TextSplitter, object?> effect)
    {
        _effects.Add(effect);
        _cleanups.Add(_ready ? effect(this) : null);
        return this;
    }

    private void Split()
    {
        RunCleanups(false);
        _lines.Clear();
        _words.Clear();
        _chars.Clear();

        _width = _host.Bounds.Width;
        // Without this every piece is snapped to a whole pixel and a line ends up wider than the text it was measured from.
        var root = _root = new StackPanel { UseLayoutRounding = false };
        var lines = MeasureLines(out var baseline);
        // An embedded control sits on the baseline by its bottom edge, which would add a descent below the last line.
        TextBlock.SetBaselineOffset(root, baseline);
        for (var i = 0; i < lines.Count; i++) root.Children.Add(BuildLine(lines[i].TrimEnd(), i));

        _writing = true;
        _host.SetCurrentValue(TextBlock.TextProperty, null);
        _writing = false;
        _host.Inlines = [new InlineUIContainer(root)];
        AutomationProperties.SetName(_host, _text);
        // Above local priority so a local or style value is never overwritten, only covered until Revert.
        _unclip ??= _host.SetValue(Visual.ClipToBoundsProperty, false, BindingPriority.Animation);
        _host.SetValue(SplitProperty, this);
        // Laid out where the host's arrange will put it, so slots exist before the host's next layout pass.
        root.Measure(Size.Infinity);
        var padding = _host.Padding;
        _splitWidth = root.DesiredSize.Width + padding.Left + padding.Right;
        var free = Math.Max(0, _width - _splitWidth);
        root.Arrange(new Rect(new Point(padding.Left + free * AlignmentFactor(), padding.Top), root.DesiredSize));
        _ready = true;

        for (var i = 0; i < _effects.Count; i++) _cleanups[i] = _effects[i](this);
    }

    // Mirrors how Avalonia's TextLine offsets a line inside its paragraph.
    private double AlignmentFactor()
    {
        var rtl = _host.FlowDirection == FlowDirection.RightToLeft;
        return _host.TextAlignment switch
        {
            TextAlignment.Center => 0.5,
            TextAlignment.Right => 1,
            TextAlignment.Left => 0,
            TextAlignment.End => rtl ? 0 : 1,
            _ => rtl ? 1 : 0,
        };
    }

    private List<string> MeasureLines(out double baseline)
    {
        // Measured on a copy: the host's own layout holds the pieces by the time of a re-split.
        var measure = Copy(_text);
        measure.TextWrapping = _host.TextWrapping;
        measure.Padding = _host.Padding;
        measure.Measure(new Size(_width, double.PositiveInfinity));
        LineHeight = measure.TextLayout.TextLines[0].Height;
        baseline = measure.TextLayout.TextLines[0].Baseline;

        var lines = new List<string>();
        foreach (var line in measure.TextLayout.TextLines)
        {
            var start = line.FirstTextSourceIndex;
            // The last line's Length counts the layout's paragraph terminator, which is not part of the text.
            if (start < _text.Length) lines.Add(_text.Substring(start, Math.Min(line.Length, _text.Length - start)));
        }
        return lines;
    }

    private TextBlock Copy(string text) => new()
    {
        Text = text,
        FontFamily = _host.FontFamily,
        FontSize = _host.FontSize,
        FontStyle = _host.FontStyle,
        FontWeight = _host.FontWeight,
        FontStretch = _host.FontStretch,
        LetterSpacing = _host.LetterSpacing,
        UseLayoutRounding = false,
    };

    private Control BuildLine(string text, int index)
    {
        var line = _parameters.Words == null && _parameters.Chars == null ? Piece(text) : BuildWords(text, index);
        return _parameters.Lines is { } parameters ? Decorate(line, parameters, _lines, index) : line;
    }

    private Control BuildWords(string text, int line)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var tokens = Tokenize(text);
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (char.IsWhiteSpace(token[0]))
            {
                if (!_parameters.IncludeSpaces) row.Children.Add(Piece(token));
                continue;
            }
            // The line is trimmed, so anything after a word is the space before the next one.
            row.Children.Add(BuildWord(token, _parameters.IncludeSpaces && i + 1 < tokens.Count ? Nbsp : "", line));
        }
        return row;
    }

    private Control BuildWord(string text, string trailing, int line)
    {
        Control word;
        if (_parameters.Chars is { } charParams)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            var graphemes = Graphemes(text);
            for (var i = 0; i < graphemes.Count; i++)
            {
                var grapheme = i == graphemes.Count - 1 ? graphemes[i] + trailing : graphemes[i];
                panel.Children.Add(Decorate(CharPiece(grapheme), charParams, _chars, line));
            }
            word = panel;
        }
        else
        {
            word = Piece(text + trailing);
        }
        return _parameters.Words is { } wordParams ? Decorate(word, wordParams, _words, line) : word;
    }

    private Control Decorate(Control piece, SplitPieceParams parameters, List<Control> pieces, int line)
    {
        piece.SetValue(LineIndexProperty, line);
        piece.SetValue(IndexInLineProperty, pieces.Count > 0 && GetLineIndex(pieces[^1]) == line ? pieces[^1].GetValue(IndexInLineProperty) + 1 : 0);
        piece.SetValue(OwnerProperty, this);
        if (parameters.Class is { } className) piece.Classes.Add(className);
        pieces.Add(piece);
        return parameters.Wrap ? new Border { ClipToBounds = true, Child = piece } : piece;
    }

    private Control CharPiece(string text)
    {
        if (!_parameters.Outlines) return Piece(text);
        using var outline = GlyphOutlines.Path(text, _host);
        if (outline.IsEmpty) return Piece(text);

        var slot = Copy(text);
        slot.Measure(Size.Infinity);
        var piece = new Path { Data = GlyphOutlines.Geometry(outline), Fill = _host.Foreground, Width = slot.DesiredSize.Width, Height = slot.DesiredSize.Height };
        piece.SetValue(GraphemeProperty, text);
        piece.SetValue(ContoursProperty, GlyphContours.From(outline));
        AutomationProperties.SetAccessibilityView(piece, AccessibilityView.Raw);
        return piece;
    }

    private static TextBlock Piece(string text)
    {
        var piece = new TextBlock { Text = text };
        AutomationProperties.SetAccessibilityView(piece, AccessibilityView.Raw);
        return piece;
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var start = 0;
        while (start < text.Length)
        {
            var whitespace = char.IsWhiteSpace(text[start]);
            var end = start + 1;
            while (end < text.Length && char.IsWhiteSpace(text[end]) == whitespace) end++;
            tokens.Add(text[start..end]);
            start = end;
        }
        return tokens;
    }

    /// <summary>The graphemes a character split of <paramref name="text"/> makes pieces of, in reading order.</summary>
    internal static IEnumerable<string> CharsOf(string text) => Tokenize(text).Where(token => !char.IsWhiteSpace(token[0])).SelectMany(Graphemes);

    private static List<string> Graphemes(string text)
    {
        var graphemes = new List<string>();
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext()) graphemes.Add((string)enumerator.Current);
        return graphemes;
    }

    private void RunCleanups(bool reverting)
    {
        for (var i = 0; i < _cleanups.Count; i++)
        {
            // A timer is reverted and re-timed by KeepTime on the next run, so only a full revert ends it here.
            if (_cleanups[i] is Action cleanup) cleanup();
            else if (reverting && _cleanups[i] is Timer timer) timer.Revert();
            _cleanups[i] = null;
        }
    }
}
