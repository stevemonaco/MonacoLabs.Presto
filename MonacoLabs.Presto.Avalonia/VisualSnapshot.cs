using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace MonacoLabs.Presto;

/// <summary>
/// Renders a visual at device resolution into a 96-DPI bitmap, whose pixels and DIPs are one unit. An <c>Image</c> passes
/// its source rectangle in DIPs and Skia reads it in pixels, so a bitmap at any other DPI draws enlarged and cropped.
/// </summary>
internal static class VisualSnapshot
{
    public static WriteableBitmap Render(Visual visual, double scaling)
    {
        var size = visual.Bounds.Size;
        var pixels = new PixelSize(Math.Max(1, (int)Math.Ceiling(size.Width * scaling)), Math.Max(1, (int)Math.Ceiling(size.Height * scaling)));
        using var rendered = new RenderTargetBitmap(pixels, new Vector(96, 96) * scaling);
        rendered.Render(visual);
        var bitmap = new WriteableBitmap(pixels, new Vector(96, 96), rendered.Format ?? PixelFormat.Bgra8888, rendered.AlphaFormat ?? AlphaFormat.Premul);
        using var locked = bitmap.Lock();
        rendered.CopyPixels(new PixelRect(pixels), locked.Address, locked.RowBytes * pixels.Height, locked.RowBytes);
        return bitmap;
    }

    /// <summary>The size in DIPs that draws <paramref name="bitmap"/> one device pixel per pixel.</summary>
    public static Size SizeOf(Bitmap bitmap, double scaling) => new(bitmap.PixelSize.Width / scaling, bitmap.PixelSize.Height / scaling);
}
