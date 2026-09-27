using Avalonia;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge.Compositor;

namespace PrestoSample.Showcase.Pages;

/// <summary>
/// The stress bench's third mode: <c>count</c> plain <see cref="Cell"/>s animated by the scene's own engine and drawn as
/// one grid of squares, so neither the ticks nor the draws touch the UI thread.
/// </summary>
public sealed class BenchScene(int count, AnimationParams motion) : CompositorScene
{
    private const int TintSteps = 16;

    // A fixed ramp, because the scene's brush cache is meant for a small palette.
    private static readonly Color[] Tints = [.. Enumerable.Range(0, TintSteps).Select(i => Mix(Palette.Accent.Color, Palette.Aqua.Color, i / (TintSteps - 1d)))];

    private readonly Cell[] _cells = [.. Enumerable.Range(0, count).Select(_ => new Cell())];

    /// <summary>
    /// How <paramref name="count"/> square cells fill <paramref name="size"/>: the number of columns, and the pitch of one
    /// cell, which the UI-thread modes lay their controls out on too.
    /// </summary>
    public static (int Columns, double Pitch) Grid(int count, Size size)
    {
        var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(count * size.Width / size.Height)));
        var rows = (int)Math.Ceiling((double)count / columns);
        return (columns, Math.Min(size.Width / columns, size.Height / rows));
    }

    /// <inheritdoc/>
    protected override void OnAttached() => Presto.Animate(_cells, motion);

    /// <inheritdoc/>
    protected override void OnRender(ImmediateDrawingContext context, Size size)
    {
        var (columns, pitch) = Grid(_cells.Length, size);
        for (var i = 0; i < _cells.Length; i++)
        {
            var cell = _cells[i];
            var half = pitch * .35 * cell.Scale;
            var square = new Rect(-half, -half, 2 * half, 2 * half);
            var brush = Brush(Tints[(int)Math.Round(cell.Tint * (TintSteps - 1))], cell.Opacity);
            var centre = Matrix.CreateTranslation(((i % columns) + .5) * pitch, ((i / columns) + .5) * pitch);
            using (context.PushPreTransform(Matrix.CreateRotation(cell.Rotate * Math.PI / 180) * centre)) context.FillRectangle(brush, square);
        }
    }

    private static Color Mix(Color from, Color to, double t) => Color.FromRgb(
        (byte)(from.R + ((to.R - from.R) * t)),
        (byte)(from.G + ((to.G - from.G) * t)),
        (byte)(from.B + ((to.B - from.B) * t)));

    /// <summary>One animated square: a plain object the core's reflection adapter drives, here and on the worker engine.</summary>
    public sealed class Cell
    {
        public double Opacity { get; set; } = 1;

        public double Scale { get; set; } = 1;

        public double Rotate { get; set; }

        /// <summary>0 draws the accent, 1 aqua.</summary>
        public double Tint { get; set; }
    }
}
