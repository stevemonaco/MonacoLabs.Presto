using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace Monaco.Presto.AvaloniaBridge.Controls;

/// <summary>A row of character cells that flip through <see cref="Charset"/> to show <see cref="Text"/>, like a departures board.</summary>
/// <remarks>Each cell is as wide as the charset's widest character plus half an em.</remarks>
public sealed class SplitFlap : Control
{
    /// <summary>The text the cells flip to.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SplitFlap, string?>(nameof(Text));

    /// <summary>How many cells to show, padding the text with spaces or truncating it; 0, the default, shows one per character of <see cref="Text"/>.</summary>
    public static readonly StyledProperty<int> CellsProperty =
        AvaloniaProperty.Register<SplitFlap, int>(nameof(Cells), validate: count => count >= 0);

    /// <summary>The characters a cell steps through, in order; a space, A to Z, 0 to 9 and <c>.:-/</c> by default.</summary>
    public static readonly StyledProperty<string> CharsetProperty =
        AvaloniaProperty.Register<SplitFlap, string>(nameof(Charset), " ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.:-/");

    /// <summary>Milliseconds one step takes; 60 by default.</summary>
    public static readonly StyledProperty<double> FlipDurationProperty =
        AvaloniaProperty.Register<SplitFlap, double>(nameof(FlipDuration), 60);

    /// <summary>Milliseconds between the starts of neighboring cells; 50, the standard <see cref="MotionTokens.StaggerStep"/>, by default.</summary>
    public static readonly StyledProperty<double> CellStaggerProperty =
        AvaloniaProperty.Register<SplitFlap, double>(nameof(CellStagger), MotionTokens.Standard.StaggerStep);

    /// <summary>The space between two cells; 2 by default.</summary>
    public static readonly StyledProperty<double> CellSpacingProperty =
        AvaloniaProperty.Register<SplitFlap, double>(nameof(CellSpacing), 2);

    /// <summary>The cells' background.</summary>
    public static readonly StyledProperty<IBrush?> CellBackgroundProperty =
        AvaloniaProperty.Register<SplitFlap, IBrush?>(nameof(CellBackground));

    /// <summary>The corner radius of each cell.</summary>
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner<SplitFlap>();

    /// <inheritdoc cref="TextElement.FontFamilyProperty"/>
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<SplitFlap>();

    /// <inheritdoc cref="TextElement.FontSizeProperty"/>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<SplitFlap>();

    /// <inheritdoc cref="TextElement.FontStyleProperty"/>
    public static readonly StyledProperty<FontStyle> FontStyleProperty = TextElement.FontStyleProperty.AddOwner<SplitFlap>();

    /// <inheritdoc cref="TextElement.FontWeightProperty"/>
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<SplitFlap>();

    /// <inheritdoc cref="TextElement.FontStretchProperty"/>
    public static readonly StyledProperty<FontStretch> FontStretchProperty = TextElement.FontStretchProperty.AddOwner<SplitFlap>();

    /// <inheritdoc cref="TextElement.ForegroundProperty"/>
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<SplitFlap>();

    private readonly StackPanel _row = new() { Orientation = Orientation.Horizontal, Spacing = 2 };
    private readonly Dictionary<char, int> _index = [];
    private string[] _glyphs = [];
    private bool _upper;
    private Cell[] _cells = [];
    private Timer? _timer;
    private bool _sized;

    /// <summary>Creates an empty board.</summary>
    public SplitFlap()
    {
        VisualChildren.Add(_row);
        LogicalChildren.Add(_row);
        IndexCharset();
    }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <inheritdoc cref="CellsProperty"/>
    public int Cells
    {
        get => GetValue(CellsProperty);
        set => SetValue(CellsProperty, value);
    }

    /// <inheritdoc cref="CharsetProperty"/>
    public string Charset
    {
        get => GetValue(CharsetProperty);
        set => SetValue(CharsetProperty, value);
    }

    /// <inheritdoc cref="FlipDurationProperty"/>
    public double FlipDuration
    {
        get => GetValue(FlipDurationProperty);
        set => SetValue(FlipDurationProperty, value);
    }

    /// <inheritdoc cref="CellStaggerProperty"/>
    public double CellStagger
    {
        get => GetValue(CellStaggerProperty);
        set => SetValue(CellStaggerProperty, value);
    }

    /// <inheritdoc cref="CellSpacingProperty"/>
    public double CellSpacing
    {
        get => GetValue(CellSpacingProperty);
        set => SetValue(CellSpacingProperty, value);
    }

    /// <inheritdoc cref="CellBackgroundProperty"/>
    public IBrush? CellBackground
    {
        get => GetValue(CellBackgroundProperty);
        set => SetValue(CellBackgroundProperty, value);
    }

    /// <inheritdoc cref="CornerRadiusProperty"/>
    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <inheritdoc cref="FontFamilyProperty"/>
    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <inheritdoc cref="FontSizeProperty"/>
    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <inheritdoc cref="FontStyleProperty"/>
    public FontStyle FontStyle
    {
        get => GetValue(FontStyleProperty);
        set => SetValue(FontStyleProperty, value);
    }

    /// <inheritdoc cref="FontWeightProperty"/>
    public FontWeight FontWeight
    {
        get => GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    /// <inheritdoc cref="FontStretchProperty"/>
    public FontStretch FontStretch
    {
        get => GetValue(FontStretchProperty);
        set => SetValue(FontStretchProperty, value);
    }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == TextProperty || property == CellsProperty)
        {
            Retarget();
        }
        else if (property == CharsetProperty)
        {
            IndexCharset();
            InvalidateCells();
            Retarget();
        }
        else if (property == FontFamilyProperty || property == FontSizeProperty || property == FontStyleProperty
                 || property == FontWeightProperty || property == FontStretchProperty)
        {
            InvalidateCells();
        }
        else if (property == CellSpacingProperty)
        {
            _row.Spacing = CellSpacing;
        }
        else if (property == CellBackgroundProperty || property == CornerRadiusProperty)
        {
            foreach (var cell in _cells) Paint(cell);
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        if (!_sized) SizeCells();
        return base.MeasureOverride(availableSize);
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Pause();
        foreach (var cell in _cells) Rest(cell, cell.Target);
    }

    private void IndexCharset()
    {
        var charset = Charset;
        _index.Clear();
        _glyphs = new string[charset.Length];
        _upper = true;
        for (var i = 0; i < charset.Length; i++)
        {
            _index.TryAdd(charset[i], i);
            _glyphs[i] = charset[i].ToString();
            if (char.IsLower(charset[i])) _upper = false;
        }
    }

    private string Glyph(char c) => _index.TryGetValue(c, out var i) ? _glyphs[i] : c.ToString();

    private string Next(string shown, string target) =>
        _index.TryGetValue(shown[0], out var from) && _index.ContainsKey(target[0]) ? _glyphs[(from + 1) % _glyphs.Length] : target;

    private void Retarget()
    {
        var source = Text ?? "";
        AutomationProperties.SetName(this, source);
        var text = _upper ? source.ToUpperInvariant() : source;
        Grow(Cells > 0 ? Cells : text.Length);
        var reduced = Engine.Current.Defaults.ReducedMotion;
        var instant = reduced || VisualRoot == null;
        if (instant) _timer?.Pause();
        else if ((_timer ??= Presto.CreateTimer(new TimerParams { Autoplay = false, OnUpdate = Advance })).Paused) _timer.Restart();
        var now = _timer?.CurrentTime ?? 0;
        List<Control>? changed = null;
        for (var i = 0; i < _cells.Length; i++)
        {
            var cell = _cells[i];
            cell.Target = Glyph(i < text.Length ? text[i] : ' ');
            if (instant)
            {
                if (cell.Shown != cell.Target) (changed ??= []).Add(cell.Root);
                Rest(cell, cell.Target);
            }
            else if (!cell.Moving && cell.Shown != cell.Target)
            {
                cell.Moving = true;
                cell.Start = now + (i * CellStagger);
            }
        }
        if (reduced && changed != null && VisualRoot != null)
        {
            Presto.Build().Opacity(1, from: 0).Duration(Recipes.Tokens.Short).Ease(Recipes.Tokens.Ease).Animate(changed);
        }
    }

    private void Advance(Timer timer)
    {
        var now = timer.CurrentTime;
        var duration = FlipDuration;
        var moving = false;
        foreach (var cell in _cells)
        {
            if (cell.Moving) Advance(cell, now, duration);
            moving |= cell.Moving;
        }
        if (!moving) timer.Pause();
    }

    private void Advance(Cell cell, double now, double duration)
    {
        if (now < cell.Start) return;
        if (!cell.Flipping)
        {
            if (cell.Shown == cell.Target || duration <= 0)
            {
                Rest(cell, cell.Target);
                return;
            }
            Begin(cell);
        }
        var progress = (now - cell.Start) / duration;
        while (progress >= 1)
        {
            cell.Shown = cell.To;
            cell.Start += duration;
            progress -= 1;
            if (cell.Shown == cell.Target)
            {
                Rest(cell, cell.Shown);
                return;
            }
            Begin(cell);
        }
        var folding = progress < .5;
        cell.TopFlap.IsVisible = folding;
        cell.BottomFlap.IsVisible = !folding;
        if (folding) cell.TopFold.AngleX = -90 * Eases.InQuad(progress * 2);
        else cell.BottomFold.AngleX = 90 * (1 - Eases.OutQuad((progress - .5) * 2));
    }

    private void Begin(Cell cell)
    {
        cell.Flipping = true;
        cell.To = Next(cell.Shown, cell.Target);
        Show(cell.Top, cell.To);
        Show(cell.Bottom, cell.Shown);
        Show(cell.TopFlap, cell.Shown);
        Show(cell.BottomFlap, cell.To);
    }

    private static void Rest(Cell cell, string glyph)
    {
        cell.Shown = glyph;
        cell.Moving = cell.Flipping = false;
        Show(cell.Top, glyph);
        Show(cell.Bottom, glyph);
        cell.TopFlap.IsVisible = cell.BottomFlap.IsVisible = false;
    }

    private static void Show(Border half, string glyph) => ((TextBlock)half.Child!).Text = glyph;

    private void Grow(int count)
    {
        if (count == _cells.Length) return;
        var cells = new Cell[count];
        Array.Copy(_cells, cells, Math.Min(count, _cells.Length));
        for (var i = _cells.Length; i < count; i++)
        {
            cells[i] = new Cell();
            _row.Children.Add(cells[i].Root);
            Paint(cells[i]);
        }
        for (var i = count; i < _cells.Length; i++) _row.Children.Remove(_cells[i].Root);
        _cells = cells;
        InvalidateCells();
    }

    private void Paint(Cell cell)
    {
        var radius = CornerRadius;
        var top = new CornerRadius(radius.TopLeft, radius.TopRight, 0, 0);
        var bottom = new CornerRadius(0, 0, radius.BottomRight, radius.BottomLeft);
        foreach (var half in cell.Halves)
        {
            half.Background = CellBackground;
            half.CornerRadius = half.VerticalAlignment == VerticalAlignment.Top ? top : bottom;
        }
    }

    private void InvalidateCells()
    {
        _sized = false;
        InvalidateMeasure();
    }

    private void SizeCells()
    {
        _sized = true;
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        double widest = 0, line = 0;
        foreach (var glyph in _glyphs)
        {
            using var layout = new TextLayout(glyph, typeface, FontSize, null);
            widest = Math.Max(widest, layout.WidthIncludingTrailingWhitespace);
            line = Math.Max(line, layout.Height);
        }
        var height = line + (FontSize / 4);
        foreach (var cell in _cells)
        {
            cell.Root.Width = widest + (FontSize / 2);
            cell.Root.Height = height;
            foreach (var half in cell.Halves)
            {
                half.Height = height / 2;
                var text = (TextBlock)half.Child!;
                text.Height = height;
                text.Padding = new Thickness(0, FontSize / 8);
            }
        }
    }

    private sealed class Cell
    {
        public readonly Panel Root = new();
        public readonly Border Top = Half(VerticalAlignment.Top);
        public readonly Border Bottom = Half(VerticalAlignment.Bottom);
        public readonly Border TopFlap = Half(VerticalAlignment.Top);
        public readonly Border BottomFlap = Half(VerticalAlignment.Bottom);
        public readonly Rotate3DTransform TopFold = new() { Depth = Recipes.FlipPerspective };
        public readonly Rotate3DTransform BottomFold = new() { Depth = Recipes.FlipPerspective };
        public readonly Border[] Halves;
        public string Shown = " ";
        public string Target = " ";
        public string To = " ";
        public double Start;
        public bool Moving;
        public bool Flipping;

        public Cell()
        {
            Halves = [Top, Bottom, TopFlap, BottomFlap];
            Root.Children.AddRange(Halves);
            AutomationProperties.SetAccessibilityView(Root, AccessibilityView.Raw);
            TopFlap.RenderTransformOrigin = new RelativePoint(.5, 1, RelativeUnit.Relative);
            TopFlap.RenderTransform = TopFold;
            BottomFlap.RenderTransformOrigin = new RelativePoint(.5, 0, RelativeUnit.Relative);
            BottomFlap.RenderTransform = BottomFold;
            Rest(this, " ");
        }

        private static Border Half(VerticalAlignment edge) => new()
        {
            VerticalAlignment = edge,
            ClipToBounds = true,
            Child = new TextBlock { VerticalAlignment = edge, TextAlignment = TextAlignment.Center },
        };
    }
}
