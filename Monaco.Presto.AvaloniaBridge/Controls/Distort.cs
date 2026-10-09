using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering.Composition;
using Avalonia.VisualTree;

namespace Monaco.Presto.AvaloniaBridge.Controls;

/// <summary>
/// Draws its child through raster distortions, in this order: a noise <see cref="Turbulence"/>, a <see cref="Goo"/> that fuses
/// nearby shapes, and a <see cref="Glitch"/>. At identity the child draws live; otherwise a snapshot of it is drawn through the
/// filters within the control's bounds, retaken whenever the child draws something new.
/// </summary>
/// <remarks>Under <see cref="PrestoDefaults.ReducedMotion"/>, read at each draw, every effect draws as identity.</remarks>
public sealed class Distort : Decorator
{
    /// <summary>Defines the <see cref="Turbulence"/> property.</summary>
    public static readonly StyledProperty<double> TurbulenceProperty = AvaloniaProperty.Register<Distort, double>(nameof(Turbulence));

    /// <summary>Defines the <see cref="TurbulenceScale"/> property.</summary>
    public static readonly StyledProperty<double> TurbulenceScaleProperty = AvaloniaProperty.Register<Distort, double>(nameof(TurbulenceScale), 32);

    /// <summary>Defines the <see cref="TurbulencePhase"/> property.</summary>
    public static readonly StyledProperty<double> TurbulencePhaseProperty = AvaloniaProperty.Register<Distort, double>(nameof(TurbulencePhase));

    /// <summary>Defines the <see cref="Goo"/> property.</summary>
    public static readonly StyledProperty<double> GooProperty = AvaloniaProperty.Register<Distort, double>(nameof(Goo));

    /// <summary>Defines the <see cref="Glitch"/> property.</summary>
    public static readonly StyledProperty<double> GlitchProperty = AvaloniaProperty.Register<Distort, double>(nameof(Glitch));

    // Each render of a visual replaces its composition draw list, the only trace Avalonia leaves that a descendant redrew.
    private static readonly PropertyInfo? DrawListProperty = typeof(CompositionVisual).Assembly
        .GetType("Avalonia.Rendering.Composition.CompositionDrawListVisual")?.GetProperty("DrawList");

    static Distort() => AffectsRender<Distort>(TurbulenceProperty, TurbulenceScaleProperty, TurbulencePhaseProperty, GooProperty, GlitchProperty);

    /// <summary>How far seeded noise displaces the child's pixels, in DIPs: 0 leaves them in place.</summary>
    public double Turbulence
    {
        get => GetValue(TurbulenceProperty);
        set => SetValue(TurbulenceProperty, value);
    }

    /// <summary>The feature size of the <see cref="Turbulence"/> noise in DIPs; defaults to 32.</summary>
    public double TurbulenceScale
    {
        get => GetValue(TurbulenceScaleProperty);
        set => SetValue(TurbulenceScaleProperty, value);
    }

    /// <summary>How far the <see cref="Turbulence"/> noise has flowed along x, in multiples of <see cref="TurbulenceScale"/>; animate it for liquid motion.</summary>
    public double TurbulencePhase
    {
        get => GetValue(TurbulencePhaseProperty);
        set => SetValue(TurbulencePhaseProperty, value);
    }

    /// <summary>The blur radius in DIPs that is thresholded back to hard edges, fusing shapes closer than it like metaballs: 0 leaves the child unchanged.</summary>
    public double Goo
    {
        get => GetValue(GooProperty);
        set => SetValue(GooProperty, value);
    }

    /// <summary>
    /// How far, in DIPs, the red and blue channels split from green and seeded horizontal bands shift: 0 leaves the child
    /// unchanged. The bands are seeded by the value, so a held strength holds still and an animated one flickers.
    /// </summary>
    public double Glitch
    {
        get => GetValue(GlitchProperty);
        set => SetValue(GlitchProperty, value);
    }

    private Snapshot? _snapshot;
    private CompositionVisual? _hidden;
    private Vector3D _hiddenScale;
    private readonly List<object?> _drawLists = [];
    private Action<TimeSpan>? _poll;
    private bool _polling;

    /// <summary>How many snapshots of the child have been taken.</summary>
    internal int Snapshots { get; private set; }

    internal PixelSize? SnapshotSize => _snapshot?.Bitmap.PixelSize;

    private bool IsIdentity => (Turbulence == 0 && Goo <= 0 && Glitch == 0) || Engine.Current.Defaults.ReducedMotion;

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        if (Child is not { } child || IsIdentity)
        {
            Release();
            return;
        }
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        if (_snapshot?.Scaling != scaling) Take(child, scaling);
        Hide(child);
        var bitmap = _snapshot!.Bitmap;
        context.Custom(new DistortDrawOperation(bitmap, new Rect(child.Bounds.Position, VisualSnapshot.SizeOf(bitmap, scaling)),
            new Rect(Bounds.Size), (Turbulence, TurbulenceScale, TurbulencePhase, Goo, Glitch), _snapshot.Retain()));
    }

    private void Take(Control child, double scaling)
    {
        _snapshot?.Dispose();
        _snapshot = new Snapshot(VisualSnapshot.Render(child, scaling), scaling);
        Snapshots++;
        Redrew(child);
        Watch();
    }

    private void Watch()
    {
        if (_polling) return;
        _polling = true;
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(_poll ??= Poll);
    }

    // The child stays in layout and keeps its own properties; only its composition visual is scaled to nothing, which Avalonia never resets.
    private void Hide(Control child)
    {
        if (_hidden is not null || ElementComposition.GetElementVisual(child) is not { } visual) return;
        _hidden = visual;
        _hiddenScale = visual.Scale;
        visual.Scale = default;
    }

    private void Poll(TimeSpan _)
    {
        _polling = false;
        if (_snapshot is null || Child is not { } child) return;
        if (Redrew(child))
        {
            _snapshot.Dispose();
            _snapshot = null;
            InvalidateVisual();
            return;
        }
        Watch();
    }

    // Records the draw lists of the child's subtree, returning whether any differs from the last record.
    private bool Redrew(Control child)
    {
        var changed = false;
        var i = 0;
        foreach (var visual in child.GetSelfAndVisualDescendants())
        {
            var drawList = ElementComposition.GetElementVisual(visual) is { } element ? DrawListProperty?.GetValue(element) : null;
            changed |= i == _drawLists.Count || !ReferenceEquals(_drawLists[i], drawList);
            if (i == _drawLists.Count) _drawLists.Add(drawList);
            else _drawLists[i] = drawList;
            i++;
        }
        if (i < _drawLists.Count)
        {
            _drawLists.RemoveRange(i, _drawLists.Count - i);
            changed = true;
        }
        return changed;
    }

    private void Release()
    {
        _snapshot?.Dispose();
        _snapshot = null;
        _drawLists.Clear();
        if (_hidden is null) return;
        _hidden.Scale = _hiddenScale;
        _hidden = null;
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ChildProperty) Release();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Release();
    }

    // Disposed once the control and every draw operation still holding it let go; draw operations are released on the render thread.
    private sealed class Snapshot(WriteableBitmap bitmap, double scaling) : IDisposable
    {
        private int _references = 1;

        public WriteableBitmap Bitmap => bitmap;
        public double Scaling => scaling;

        public Snapshot Retain()
        {
            Interlocked.Increment(ref _references);
            return this;
        }

        public void Dispose()
        {
            if (Interlocked.Decrement(ref _references) == 0) bitmap.Dispose();
        }
    }
}
