using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Compositor;
using Monaco.Presto.AvaloniaBridge.Text;

namespace PrestoSample.Showcase.Pages;

public partial class ParticleTypePage : ShowcasePage
{
    private static readonly string[] Words = ["PRESTO", "TYPE", "MOTION", "DUST"];

    // Touched only on the render thread, from Invoke.
    private ParticleFormation? _formation;

    private int _word;

    public ParticleTypePage()
    {
        InitializeComponent();
        HowCode.Code = $"{Snippets.ParticleTypeFormation}\n\n{Snippets.ParticleTypeRepel}\n\n{Snippets.ParticleTypeCaption}";
    }

    protected override void OnAttached() => Presto.CreateTimer(new()
    {
        FrameRate = 4,
        OnUpdate = _ => Field.Invoke<ParticleScene>(_ =>
        {
            var count = _formation?.Count ?? 0;
            Dispatcher.UIThread.Post(() => CountText.Text = $"{count} particles");
        }),
    });

    #region snippet ParticleTypeFormation
    private IReadOnlyList<Point> Sample()
    {
        var outline = PrestoAvalonia.TextOutline(Words[_word], new Typeface(Caption.FontFamily, FontStyle.Normal, FontWeight.Black), 100);
        var (bounds, size) = (outline.Bounds, Field.Bounds.Size);
        var scale = Math.Min(size.Width * .85 / bounds.Width, size.Height * .6 / bounds.Height);
        var center = new Point(size.Width / 2, size.Height / 2);
        // Sampled at 100 and scaled up, so the spacing is divided by the scale to stay in canvas DIPs.
        var sampling = (GlyphSampling)(SamplingPicker.SelectedIndex + 1);
        var spacing = SpacingSlider.Value / scale;
        var points = GlyphPoints.Sample(outline, spacing, sampling);
        // Points past the scene cap get no particle, which would cut off the end of its bottom rows, so sample coarser instead.
        // The count falls with the square of the spacing, so one rescaled pass usually fits.
        var cap = ((ParticleScene)Field.Scene!).MaxLive;
        while (points.Count > cap) points = GlyphPoints.Sample(outline, spacing *= Math.Sqrt((double)points.Count / cap) * 1.02, sampling);
        return [.. points.Select(p => center + (p - bounds.Center) * scale)];
    }

    // A formation's color is fixed, so a new color starts a new scene whose particles fade in on their points.
    private void Rebuild()
    {
        Field.Scene = new ParticleScene();
        var points = Sample();
        var color = ((ISolidColorBrush)((ComboBoxItem)ColorPicker.SelectedItem!).Foreground!).Color;
        Field.Invoke<ParticleScene>(scene =>
        {
            _formation = scene.AddFormation([], color);
            _formation.Retarget(points);
        });
    }

    // Retarget flies the particles to the new points; reduced, the new points fade in where they are instead.
    private void Show()
    {
        if (Presto.Defaults.ReducedMotion)
        {
            Rebuild();
            return;
        }
        var points = Sample();
        var (transition, duration) = ((FormationTransition)TransitionPicker.SelectedIndex, DurationSlider.Value);
        Field.Invoke<ParticleScene>(_ =>
        {
            if (_formation is not { } formation) return;
            formation.Duration = duration;
            formation.Retarget(points, transition);
        });
    }
    #endregion

    #region snippet ParticleTypeRepel
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var position = e.GetPosition(Field);
        Point? repel = Inside(position) && !Presto.Defaults.ReducedMotion ? position : null;
        Field.Invoke<ParticleScene>(_ => _formation?.Repel = repel);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Field.Invoke<ParticleScene>(_ => _formation?.Repel = null);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Inside(e.GetPosition(Field))) NextWord();
    }
    #endregion

    #region snippet ParticleTypeCaption
    private void Disintegrate(object? sender, RoutedEventArgs e) => Recipes.Disintegrate(Caption);

    private void Assemble(object? sender, RoutedEventArgs e) => Recipes.Assemble(Caption);
    #endregion

    private bool Inside(Point position) => new Rect(Field.Bounds.Size).Contains(position);

    private void NextWord()
    {
        _word = (_word + 1) % Words.Length;
        Show();
    }

    private void FieldResized(object? sender, SizeChangedEventArgs e)
    {
        if (Field.Scene is null) Rebuild();
        else Show();
    }

    private void SamplingChanged(object? sender, RoutedEventArgs e)
    {
        if (Field?.Scene is not null) Show();
    }

    private void ColorChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Field?.Scene is not null) Rebuild();
    }

    // UiThread.Freeze runs below Render priority, so the retarget reaches the render thread before the dispatcher stops.
    private void Freeze(object? sender, RoutedEventArgs e)
    {
        NextWord();
        UiThread.Freeze(TimeSpan.FromSeconds(2));
    }
}
