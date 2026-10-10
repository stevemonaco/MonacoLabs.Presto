using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Threading;

namespace MonacoLabs.Presto.Controls;

/// <summary>
/// An <see cref="Image"/>-like control whose drawing subclasses can change: it sizes, stretches and centers one
/// <see cref="CurrentImage"/> region, and carries CSS-style color filters that compose in the fixed order Brightness →
/// Contrast → Saturate → Grayscale → Sepia → HueRotate → Tint → Duotone → Invert, followed by a <see cref="Ripple"/>, a
/// <see cref="Pixelate"/> and a noise <see cref="Dissolve"/>.
/// </summary>
public abstract class PrestoImageBase : Control
{
    /// <summary>Defines the <see cref="Source"/> property.</summary>
    public static readonly StyledProperty<IImage?> SourceProperty = Image.SourceProperty.AddOwner<PrestoImageBase>();

    /// <summary>Defines the <see cref="Stretch"/> property.</summary>
    public static readonly StyledProperty<Stretch> StretchProperty = Image.StretchProperty.AddOwner<PrestoImageBase>();

    /// <summary>Defines the <see cref="StretchDirection"/> property.</summary>
    public static readonly StyledProperty<StretchDirection> StretchDirectionProperty = Image.StretchDirectionProperty.AddOwner<PrestoImageBase>();

    /// <summary>Defines the <see cref="Brightness"/> property.</summary>
    public static readonly StyledProperty<double> BrightnessProperty =
        AvaloniaProperty.Register<PrestoImageBase, double>(nameof(Brightness), 1);

    /// <summary>Defines the <see cref="Contrast"/> property.</summary>
    public static readonly StyledProperty<double> ContrastProperty =
        AvaloniaProperty.Register<PrestoImageBase, double>(nameof(Contrast), 1);

    /// <summary>Defines the <see cref="Saturate"/> property.</summary>
    public static readonly StyledProperty<double> SaturateProperty =
        AvaloniaProperty.Register<PrestoImageBase, double>(nameof(Saturate), 1);

    /// <summary>Defines the <see cref="Grayscale"/> property.</summary>
    public static readonly StyledProperty<double> GrayscaleProperty =
        AvaloniaProperty.Register<PrestoImageBase, double>(nameof(Grayscale));

    /// <summary>Defines the <see cref="Sepia"/> property.</summary>
    public static readonly StyledProperty<double> SepiaProperty =
        AvaloniaProperty.Register<PrestoImageBase, double>(nameof(Sepia));

    /// <summary>Defines the <see cref="HueRotate"/> property.</summary>
    public static readonly StyledProperty<double> HueRotateProperty =
        AvaloniaProperty.Register<PrestoImageBase, double>(nameof(HueRotate));

    /// <summary>Defines the <see cref="Invert"/> property.</summary>
    public static readonly StyledProperty<double> InvertProperty =
        AvaloniaProperty.Register<PrestoImageBase, double>(nameof(Invert));

    /// <summary>Defines the <see cref="Tint"/> property.</summary>
    public static readonly StyledProperty<double> TintProperty =
        AvaloniaProperty.Register<PrestoImageBase, double>(nameof(Tint));

    /// <summary>Defines the <see cref="TintColor"/> property.</summary>
    public static readonly StyledProperty<Color> TintColorProperty =
        AvaloniaProperty.Register<PrestoImageBase, Color>(nameof(TintColor), Colors.White);

    /// <summary>Defines the <see cref="Duotone"/> property.</summary>
    public static readonly StyledProperty<double> DuotoneProperty =
        AvaloniaProperty.Register<PrestoImageBase, double>(nameof(Duotone));

    /// <summary>Defines the <see cref="DuotoneShadow"/> property.</summary>
    public static readonly StyledProperty<Color> DuotoneShadowProperty =
        AvaloniaProperty.Register<PrestoImageBase, Color>(nameof(DuotoneShadow), Colors.Black);

    /// <summary>Defines the <see cref="DuotoneHighlight"/> property.</summary>
    public static readonly StyledProperty<Color> DuotoneHighlightProperty =
        AvaloniaProperty.Register<PrestoImageBase, Color>(nameof(DuotoneHighlight), Colors.White);

    /// <summary>Defines the <see cref="Ripple"/> property.</summary>
    public static readonly StyledProperty<double> RippleProperty =
        AvaloniaProperty.Register<PrestoImageBase, double>(nameof(Ripple));

    /// <summary>Defines the <see cref="RippleScale"/> property.</summary>
    public static readonly StyledProperty<double> RippleScaleProperty =
        AvaloniaProperty.Register<PrestoImageBase, double>(nameof(RippleScale), 32);

    /// <summary>Defines the <see cref="Pixelate"/> property.</summary>
    public static readonly StyledProperty<double> PixelateProperty =
        AvaloniaProperty.Register<PrestoImageBase, double>(nameof(Pixelate), 1);

    /// <summary>Defines the <see cref="Dissolve"/> property.</summary>
    public static readonly StyledProperty<double> DissolveProperty =
        AvaloniaProperty.Register<PrestoImageBase, double>(nameof(Dissolve));

    /// <summary>Defines the <see cref="DissolveScale"/> property.</summary>
    public static readonly StyledProperty<double> DissolveScaleProperty =
        AvaloniaProperty.Register<PrestoImageBase, double>(nameof(DissolveScale), 24);

    /// <summary>Defines the <see cref="DissolveSoftness"/> property.</summary>
    public static readonly StyledProperty<double> DissolveSoftnessProperty =
        AvaloniaProperty.Register<PrestoImageBase, double>(nameof(DissolveSoftness), 0.1);

    /// <summary>Defines the <see cref="Seed"/> property.</summary>
    public static readonly StyledProperty<int> SeedProperty =
        AvaloniaProperty.Register<PrestoImageBase, int>(nameof(Seed));

    static PrestoImageBase()
    {
        AffectsRender<PrestoImageBase>(SourceProperty, StretchProperty, StretchDirectionProperty,
            BrightnessProperty, ContrastProperty, SaturateProperty, GrayscaleProperty, SepiaProperty, HueRotateProperty,
            InvertProperty, TintProperty, TintColorProperty, DuotoneProperty, DuotoneShadowProperty, DuotoneHighlightProperty,
            RippleProperty, RippleScaleProperty, PixelateProperty, DissolveProperty, DissolveScaleProperty, DissolveSoftnessProperty,
            SeedProperty);
        AffectsMeasure<PrestoImageBase>(SourceProperty, StretchProperty, StretchDirectionProperty);
    }

    /// <summary>The image to show.</summary>
    public IImage? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>How the image is stretched to fill the control; defaults to <see cref="Avalonia.Media.Stretch.Uniform"/>.</summary>
    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    /// <summary>Whether the image may be scaled up, down or both.</summary>
    public StretchDirection StretchDirection
    {
        get => GetValue(StretchDirectionProperty);
        set => SetValue(StretchDirectionProperty, value);
    }

    /// <summary>A linear multiplier on each color channel, as CSS <c>brightness()</c>: 1 leaves the image unchanged, 0 is black.</summary>
    public double Brightness
    {
        get => GetValue(BrightnessProperty);
        set => SetValue(BrightnessProperty, value);
    }

    /// <summary>The contrast about mid-gray, as CSS <c>contrast()</c>: 1 leaves the image unchanged, 0 is flat gray.</summary>
    public double Contrast
    {
        get => GetValue(ContrastProperty);
        set => SetValue(ContrastProperty, value);
    }

    /// <summary>The saturation, as CSS <c>saturate()</c>: 1 leaves the image unchanged, 0 is gray, above 1 oversaturates.</summary>
    public double Saturate
    {
        get => GetValue(SaturateProperty);
        set => SetValue(SaturateProperty, value);
    }

    /// <summary>How far the image is converted to gray, as CSS <c>grayscale()</c>, from 0 (unchanged) to 1.</summary>
    public double Grayscale
    {
        get => GetValue(GrayscaleProperty);
        set => SetValue(GrayscaleProperty, value);
    }

    /// <summary>How far the image is converted to sepia, as CSS <c>sepia()</c>, from 0 (unchanged) to 1.</summary>
    public double Sepia
    {
        get => GetValue(SepiaProperty);
        set => SetValue(SepiaProperty, value);
    }

    /// <summary>The hue rotation in degrees, as CSS <c>hue-rotate()</c>.</summary>
    public double HueRotate
    {
        get => GetValue(HueRotateProperty);
        set => SetValue(HueRotateProperty, value);
    }

    /// <summary>How far the colors are inverted, as CSS <c>invert()</c>, from 0 (unchanged) to 1.</summary>
    public double Invert
    {
        get => GetValue(InvertProperty);
        set => SetValue(InvertProperty, value);
    }

    /// <summary>How far luminance is mapped onto <see cref="TintColor"/> (black stays black), from 0 (unchanged) to 1.</summary>
    public double Tint
    {
        get => GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    /// <summary>The color white maps to under <see cref="Tint"/>; defaults to white, a plain grayscale.</summary>
    public Color TintColor
    {
        get => GetValue(TintColorProperty);
        set => SetValue(TintColorProperty, value);
    }

    /// <summary>How far luminance is mapped from <see cref="DuotoneShadow"/> to <see cref="DuotoneHighlight"/>, from 0 (unchanged) to 1.</summary>
    public double Duotone
    {
        get => GetValue(DuotoneProperty);
        set => SetValue(DuotoneProperty, value);
    }

    /// <summary>The color black maps to under <see cref="Duotone"/>; defaults to black.</summary>
    public Color DuotoneShadow
    {
        get => GetValue(DuotoneShadowProperty);
        set => SetValue(DuotoneShadowProperty, value);
    }

    /// <summary>The color white maps to under <see cref="Duotone"/>; defaults to white.</summary>
    public Color DuotoneHighlight
    {
        get => GetValue(DuotoneHighlightProperty);
        set => SetValue(DuotoneHighlightProperty, value);
    }

    /// <summary>How far seeded noise displaces the image's pixels, in DIPs: 0 leaves the image unchanged.</summary>
    public double Ripple
    {
        get => GetValue(RippleProperty);
        set => SetValue(RippleProperty, value);
    }

    /// <summary>The feature size of the <see cref="Ripple"/> noise in DIPs; defaults to 32.</summary>
    public double RippleScale
    {
        get => GetValue(RippleScaleProperty);
        set => SetValue(RippleScaleProperty, value);
    }

    /// <summary>The block size in DIPs the image is pixelated into from its top-left corner: 1 or less leaves the image unchanged.</summary>
    public double Pixelate
    {
        get => GetValue(PixelateProperty);
        set => SetValue(PixelateProperty, value);
    }

    /// <summary>How far the image has dissolved into seeded noise, from 0 (visible) to 1 (gone).</summary>
    public double Dissolve
    {
        get => GetValue(DissolveProperty);
        set => SetValue(DissolveProperty, value);
    }

    /// <summary>The feature size of the <see cref="Dissolve"/> noise in DIPs; defaults to 24.</summary>
    public double DissolveScale
    {
        get => GetValue(DissolveScaleProperty);
        set => SetValue(DissolveScaleProperty, value);
    }

    /// <summary>The width of the <see cref="Dissolve"/> edge as a fraction of its threshold range; defaults to 0.1.</summary>
    public double DissolveSoftness
    {
        get => GetValue(DissolveSoftnessProperty);
        set => SetValue(DissolveSoftnessProperty, value);
    }

    /// <summary>The seed of every noise effect, so the same seed draws the same pattern.</summary>
    public int Seed
    {
        get => GetValue(SeedProperty);
        set => SetValue(SeedProperty, value);
    }

    private readonly List<Raster> _rasters = [];
    private bool _sweepPending;
    private Action? _dropUnusedRasters;
    private bool _offscreen;

    internal bool IsFiltering => !ImageEffect.IsIdentity;

    private protected ImageEffect ImageEffect => new(ColorMatrix.Compose(Brightness, Contrast, Saturate, Grayscale, Sepia, HueRotate,
        Tint, TintColor, Duotone, DuotoneShadow, DuotoneHighlight, Invert), Ripple, RippleScale, Pixelate, Dissolve, DissolveScale,
        DissolveSoftness, Seed);

    /// <summary>The image drawn now; <see cref="Source"/> unless a subclass shows something else.</summary>
    protected virtual IImage? CurrentImage => Source;

    /// <summary>The image the control is sized by; <see cref="CurrentImage"/> unless every frame should size alike.</summary>
    private protected virtual IImage? MeasuredImage => CurrentImage;

    /// <summary>The region of <paramref name="image"/> to draw; the whole image unless a subclass shows part of it.</summary>
    protected virtual Rect SourceRect(IImage image) => new(image.Size);

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        if (CurrentImage is { } image) DrawImage(context, image, SourceRect(image));
    }

    /// <summary>
    /// Draws <paramref name="sourceRect"/> of <paramref name="image"/> through the control's filters and effects, placed by its
    /// <see cref="Stretch"/> within its bounds, so a subclass overriding <see cref="Render"/> can draw images or cells with it.
    /// </summary>
    protected void DrawImage(DrawingContext context, IImage image, Rect sourceRect) =>
        DrawImage(context, image, sourceRect, Stretch, StretchDirection, ImageEffect);

    /// <summary>Draws <paramref name="sourceRect"/> of <paramref name="image"/> placed by <paramref name="stretch"/>; a bitmap <paramref name="owner"/> is retained by the draw.</summary>
    private protected void DrawImage(DrawingContext context, IImage image, Rect sourceRect, Stretch stretch, StretchDirection direction,
        in ImageEffect effect, Raster? owner = null)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;
        var (placed, scale) = Place(sourceRect.Size, stretch, direction);
        var destination = placed.Intersect(new Rect(Bounds.Size));
        var source = sourceRect.CenterRect(new Rect(destination.Size / scale));
        // Filtered scaling samples up to half a source pixel past the rect, which would pull in a neighboring sheet cell.
        if (scale != new Vector(1, 1) && sourceRect != new Rect(image.Size))
            source = source.Intersect(sourceRect.Deflate(0.5));

        if (effect.IsIdentity)
        {
            context.DrawImage(image, source, destination);
            return;
        }
        switch (image)
        {
            case Bitmap bitmap:
                Draw(context, new ImageEffectDrawOperation(bitmap, source, destination, effect, owner?.Retain()));
                break;
            case CroppedBitmap { Source: Bitmap bitmap } cropped:
                var crop = cropped.SourceRect.ToRectWithDpi(bitmap.Dpi);
                var inBitmap = source.Translate(crop.Position);
                if (scale != new Vector(1, 1)) inBitmap = inBitmap.Intersect(crop.Deflate(0.5));
                Draw(context, new ImageEffectDrawOperation(bitmap, inBitmap, destination, effect));
                break;
            default:
                var raster = Rasterize(image, source, destination.Size);
                Draw(context, new ImageEffectDrawOperation(raster.Bitmap, new Rect(destination.Size), destination, effect, raster.Retain()));
                break;
        }
    }

    /// <summary>Where content of <paramref name="size"/> lands when <paramref name="stretch"/> centers it in the bounds, before clipping to them.</summary>
    private protected (Rect Placed, Vector Scale) Place(Size size, Stretch stretch, StretchDirection direction)
    {
        var scale = stretch.CalculateScaling(Bounds.Size, size, direction);
        return (new Rect(Bounds.Size).CenterRect(new Rect(size * scale)), scale);
    }

    /// <summary>How many times a non-bitmap source has been rasterized for filtering.</summary>
    internal int Rasterizations { get; private set; }

    // Rasters not drawn in a render are dropped once it is over, so every source drawn each frame keeps its own.
    private Raster Rasterize(IImage image, Rect source, Size size)
    {
        if (!_sweepPending)
        {
            _sweepPending = true;
            Dispatcher.UIThread.Post(_dropUnusedRasters ??= DropUnusedRasters);
        }
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        foreach (var cached in _rasters)
        {
            if (cached.Image != image || cached.Source != source || cached.Size != size || cached.Scaling != scaling) continue;
            cached.Used = true;
            return cached;
        }

        var bitmap = new RenderTargetBitmap(PixelSize.FromSize(size, scaling), new Vector(96, 96) * scaling);
        using (var context = bitmap.CreateDrawingContext()) context.DrawImage(image, source, new Rect(size));
        if (image is DrawingImage drawing) drawing.Invalidated += OnRasterSourceInvalidated;
        Rasterizations++;
        var raster = new Raster(bitmap, image, source, size, scaling) { Used = true };
        _rasters.Add(raster);
        return raster;
    }

    private void DropUnusedRasters()
    {
        _sweepPending = false;
        for (var i = _rasters.Count - 1; i >= 0; i--)
        {
            if (_rasters[i].Used) _rasters[i].Used = false;
            else DropRaster(i);
        }
    }

    /// <summary>Renders what the control draws now into a bitmap the size of the control at its render scaling.</summary>
    private protected Raster Snapshot()
    {
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var size = Bounds.Size;
        var bitmap = new RenderTargetBitmap(PixelSize.FromSize(size, scaling), new Vector(96, 96) * scaling);
        _offscreen = true;
        try
        {
            using var context = bitmap.CreateDrawingContext();
            Render(context);
        }
        finally
        {
            _offscreen = false;
        }
        return new Raster(bitmap, bitmap, new Rect(size), size, scaling);
    }

    // An offscreen context renders custom operations synchronously and never disposes them.
    private protected void Draw(DrawingContext context, ICustomDrawOperation operation)
    {
        context.Custom(operation);
        if (_offscreen) operation.Dispose();
    }

    private void OnRasterSourceInvalidated(object? sender, EventArgs e)
    {
        for (var i = _rasters.Count - 1; i >= 0; i--)
            if (_rasters[i].Image == sender) DropRaster(i);
        InvalidateVisual();
    }

    private void DropRaster(int index)
    {
        var raster = _rasters[index];
        if (raster.Image is DrawingImage drawing) drawing.Invalidated -= OnRasterSourceInvalidated;
        raster.Dispose();
        _rasters.RemoveAt(index);
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        for (var i = _rasters.Count - 1; i >= 0; i--) DropRaster(i);
    }

    // Disposed once the control and every draw operation still holding it let go.
    private protected sealed class Raster(RenderTargetBitmap bitmap, IImage image, Rect source, Size size, double scaling) : IDisposable
    {
        private int _references = 1;

        public RenderTargetBitmap Bitmap => bitmap;
        public IImage Image => image;
        public Rect Source => source;
        public Size Size => size;
        public double Scaling => scaling;
        public bool Used { get; set; }

        public Raster Retain()
        {
            Interlocked.Increment(ref _references);
            return this;
        }

        // Draw operations are released on the render thread.
        public void Dispose()
        {
            if (Interlocked.Decrement(ref _references) == 0) bitmap.Dispose();
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize) => LayoutSize(availableSize, StretchDirection);

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize) => LayoutSize(finalSize, StretchDirection.Both);

    private Size LayoutSize(Size available, StretchDirection direction) =>
        MeasuredImage is { } image ? Stretch.CalculateSize(available, SourceRect(image).Size, direction) : default;
}
