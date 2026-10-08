using System.ComponentModel;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using SkiaSharp;
using SkiaSharp.Resources;
using SkiaSharp.Skottie;
using SkottieAnimation = SkiaSharp.Skottie.Animation;

namespace Monaco.Presto.AvaloniaBridge.Lottie;

/// <summary>
/// A parsed Lottie file: its size, timing and markers, plus the JSON each view builds its own Skottie animation from, so one
/// composition can be shared by any number of views.
/// </summary>
/// <remarks>
/// As an <see cref="IImage"/> it only reports its <see cref="Size"/> for layout and draws nothing, because Skottie animations are
/// not thread-safe and are drawn by the view on the render thread. dotLottie themes and state machines are not supported.
/// </remarks>
[TypeConverter(typeof(LottieCompositionConverter))]
public sealed class LottieComposition : IImage
{
    private readonly byte[] _json;

    private LottieComposition(byte[] json, IReadOnlyList<string>? animationIds = null)
    {
        _json = json;
        AnimationIds = animationIds ?? [];
        CreateAnimation().Dispose();
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            Width = Number(root, "w");
            Height = Number(root, "h");
            FrameRate = Number(root, "fr");
            InPoint = Number(root, "ip");
            OutPoint = Number(root, "op");
            var markers = root.TryGetProperty("markers", out var array) && array.ValueKind == JsonValueKind.Array
                ? array.EnumerateArray().ToArray()
                : [];
            var starts = markers.Select(Start).ToArray();
            Segments = markers.Select(m => Segment(m, starts)).ToArray();
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            throw new FormatException($"Invalid Lottie JSON: {e.Message}", e);
        }
    }

    /// <summary>The composition width (<c>w</c>).</summary>
    public double Width { get; }

    /// <summary>The composition height (<c>h</c>).</summary>
    public double Height { get; }

    /// <summary>Frames per second (<c>fr</c>).</summary>
    public double FrameRate { get; }

    /// <summary>The first frame (<c>ip</c>).</summary>
    public double InPoint { get; }

    /// <summary>The frame the composition ends at (<c>op</c>), exclusive.</summary>
    public double OutPoint { get; }

    /// <summary>The playing time from <see cref="InPoint"/> to <see cref="OutPoint"/>, in milliseconds.</summary>
    public double Duration => (OutPoint - InPoint) / FrameRate * 1000;

    /// <summary>
    /// The JSON's <c>markers</c> as named frame ranges, in file order: a marker with a duration runs from <c>tm</c> to
    /// <c>tm + dr</c>, and one without runs to the next marker's start or <see cref="OutPoint"/>.
    /// </summary>
    public IReadOnlyList<LottieSegment> Segments { get; }

    /// <summary>The ids in the manifest of the dotLottie file this came from, in manifest order; empty for plain Lottie JSON.</summary>
    public IReadOnlyList<string> AnimationIds { get; }

    /// <summary><see cref="Width"/> by <see cref="Height"/>.</summary>
    public Size Size => new(Width, Height);

    /// <summary>Parses Lottie JSON.</summary>
    /// <exception cref="FormatException"><paramref name="json"/> is not a Lottie animation Skottie can play.</exception>
    public static LottieComposition FromJson(string json) => new(Encoding.UTF8.GetBytes(json));

    /// <summary>
    /// Reads and parses Lottie JSON from <paramref name="stream"/>, or a dotLottie archive's default animation when the stream
    /// starts with a zip signature.
    /// </summary>
    /// <exception cref="FormatException">The stream does not hold a Lottie animation Skottie can play.</exception>
    public static LottieComposition FromStream(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        if (buffer.GetBuffer().AsSpan(0, (int)buffer.Length).StartsWith("PK\x03\x04"u8))
        {
            buffer.Position = 0;
            return FromDotLottie(buffer);
        }
        var json = buffer.ToArray();
        return new(json.AsSpan().StartsWith(Encoding.UTF8.Preamble) ? json[3..] : json);
    }

    /// <summary>
    /// Reads one animation from a dotLottie (<c>.lottie</c>) archive, with the images it references from the archive embedded.
    /// Themes and state machines in the archive are ignored.
    /// </summary>
    /// <param name="stream">The archive.</param>
    /// <param name="animationId">The manifest id to read; by default the manifest's initial animation, or else its first.</param>
    /// <exception cref="FormatException">The archive has no manifest, or the animation is not Lottie Skottie can play.</exception>
    /// <exception cref="ArgumentException">The manifest lists no animation <paramref name="animationId"/>.</exception>
    public static LottieComposition FromDotLottie(Stream stream, string? animationId = null)
    {
        try
        {
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var manifest = Parse(zip.GetEntry("manifest.json") ?? throw new FormatException("dotLottie archive has no manifest.json."));
            var ids = manifest["animations"]?.AsArray().Select(a => (string)a!["id"]!).ToArray() ?? [];
            animationId ??= (string?)manifest["initial"]?["animation"] ?? (string?)manifest["activeAnimationId"] ?? ids.FirstOrDefault()
                ?? throw new FormatException("dotLottie manifest lists no animations.");
            if (!ids.Contains(animationId))
                throw new ArgumentException($"dotLottie has no animation '{animationId}'. Available: {string.Join(", ", ids)}.", nameof(animationId));
            var animation = Parse(zip.GetEntry($"animations/{animationId}.json") ?? zip.GetEntry($"a/{animationId}.json")
                ?? throw new FormatException($"dotLottie archive has no JSON for animation '{animationId}'."));
            if (animation["assets"] is JsonArray assets)
                foreach (var asset in assets) EmbedImage(zip, asset!);
            return new(JsonSerializer.SerializeToUtf8Bytes(animation), ids);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or InvalidDataException)
        {
            throw new FormatException($"Invalid dotLottie archive: {e.Message}", e);
        }
    }

    /// <summary>
    /// Loads Lottie JSON, or a dotLottie archive's default animation, from an <c>avares://</c> asset, a <c>file://</c> URI or a
    /// plain file path.
    /// </summary>
    /// <exception cref="FormatException">The file is not a Lottie animation Skottie can play.</exception>
    public static LottieComposition Load(Uri uri)
    {
        using var stream = !uri.IsAbsoluteUri ? File.OpenRead(uri.OriginalString)
            : uri.IsFile ? File.OpenRead(uri.LocalPath)
            : AssetLoader.Open(uri);
        return FromStream(stream);
    }

    /// <summary>Finds the first segment called <paramref name="name"/>, ignoring case.</summary>
    public bool TryGetSegment(string name, out LottieSegment segment)
    {
        foreach (var candidate in Segments)
        {
            if (!string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
            segment = candidate;
            return true;
        }
        segment = default;
        return false;
    }

    /// <summary>Builds a new Skottie animation, which belongs to the one thread that seeks and renders it.</summary>
    internal SkottieAnimation CreateAnimation()
    {
        using var data = SKData.CreateCopy(_json);
        using var resources = new DataUriResourceProvider(preDecode: true);
        using var builder = SkottieAnimation.CreateBuilder(AnimationBuilderFlags.None).SetResourceProvider(resources);
        return builder.Build(data) ?? throw new FormatException("Skottie cannot parse this Lottie JSON.");
    }

    void IImage.Draw(DrawingContext context, Rect sourceRect, Rect destRect) { }

    private static JsonNode Parse(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        return JsonNode.Parse(stream) ?? throw new FormatException($"dotLottie entry '{entry.FullName}' is null.");
    }

    // Skottie's ResourceProvider cannot be subclassed from C#, so archive images are inlined as data URIs.
    private static void EmbedImage(ZipArchive zip, JsonNode asset)
    {
        if ((string?)asset["p"] is not { } file || file.StartsWith("data:", StringComparison.Ordinal)) return;
        var folder = ((string?)asset["u"] ?? "").TrimStart('/');
        if ((zip.GetEntry(folder + file) ?? zip.GetEntry("images/" + file) ?? zip.GetEntry("i/" + file)) is not { } entry) return;
        using var image = new MemoryStream();
        using (var stream = entry.Open()) stream.CopyTo(image);
        asset["u"] = "";
        asset["p"] = $"data:image/{Path.GetExtension(file).TrimStart('.')};base64,{Convert.ToBase64String(image.GetBuffer(), 0, (int)image.Length)}";
        asset["e"] = 1;
    }

    private static double Number(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : throw new FormatException($"Lottie JSON has no numeric '{name}'.");

    private static double Start(JsonElement marker) => marker.TryGetProperty("tm", out var start) ? start.GetDouble() : 0;

    private LottieSegment Segment(JsonElement marker, double[] starts)
    {
        var start = Start(marker);
        var duration = marker.TryGetProperty("dr", out var dr) ? dr.GetDouble() : 0;
        return new(
            marker.TryGetProperty("cm", out var comment) ? MarkerName(comment.GetString() ?? "") : "",
            start,
            duration > 0 ? start + duration : starts.Where(s => s > start).DefaultIfEmpty(OutPoint).Min());
    }

    // Some exporters write the comment as a JSON object string, {"name":"hover"}.
    private static string MarkerName(string comment)
    {
        if (!comment.TrimStart().StartsWith('{')) return comment;
        try
        {
            using var document = JsonDocument.Parse(comment);
            return document.RootElement.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
                ? name.GetString()!
                : comment;
        }
        catch (JsonException)
        {
            return comment;
        }
    }
}
