using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using MonacoLabs.Presto.Targets;

namespace MonacoLabs.Presto.Values;

/// <summary>
/// Tweens <see cref="FontVariationSettings"/> axis by axis, parsing strings such as <c>"wght=700, wdth=85"</c> through
/// <see cref="FontVariationSettings.Parse"/>. An axis one end leaves out takes the default of the target's font, which is
/// what that end draws, so <c>wght</c> alone can tween towards <c>wght, wdth</c> without a jump.
/// </summary>
/// <remarks>
/// The channels are one per axis plus a trailing progress channel from 0 to 1, which places an axis whose missing end is
/// only resolved against the font on the tween's first frame. Written values are quantized to whole axis units, and a frame
/// that quantizes to the last one rewrites the same instance.
/// </remarks>
internal sealed class FontVariationSettingsCodec : IReconcilingValueCodec
{
    private static readonly ConditionalWeakTable<Tween, Frame> s_frames = new();

    public bool TryDecode(object value, DecomposedValue into)
    {
        if (value is not FontVariationSettings settings) return false;
        Decompose(settings, into);
        return true;
    }

    public bool TryParse(string text, DecomposedValue into)
    {
        Decompose(text.Trim() == "none" ? FontVariationSettings.Empty : FontVariationSettings.Parse(text), into);
        return true;
    }

    public void Reconcile(DecomposedValue from, DecomposedValue to)
    {
        var fromAxes = Axes(from);
        var toAxes = Axes(to);
        var tags = new List<string>(fromAxes.Keys);
        foreach (var tag in toAxes.Keys)
        {
            if (!fromAxes.ContainsKey(tag)) tags.Add(tag);
        }
        tags.Sort(string.CompareOrdinal);

        var strings = Strings(tags, progress: true);
        Fill(from, tags, fromAxes, strings, 0);
        Fill(to, tags, toAxes, strings, 1);
    }

    public object Encode(double number, Tween tween)
    {
        var frame = s_frames.GetValue(tween, static t => new Frame(t));
        // A refreshed tween decomposes its ends again, possibly into other axes.
        if (!frame.Matches(tween)) s_frames.AddOrUpdate(tween, frame = new Frame(tween));
        return frame.Write(tween);
    }

    private static void Decompose(FontVariationSettings settings, DecomposedValue into)
    {
        var variations = settings.Variations;
        var tags = new List<string>(variations.Length);
        var numbers = new double[variations.Length];
        for (var i = 0; i < variations.Length; i++)
        {
            tags.Add(variations[i].Tag.ToString());
            numbers[i] = variations[i].Value;
        }
        into.Kind = ValueKind.Complex;
        into.Numbers = numbers;
        into.Strings = Strings(tags, progress: false);
    }

    /// <summary>
    /// <c>"wght="</c>, <c>",wdth="</c>…, so a composed frame reads <c>wght=700,wdth=85</c>, then <c>";"</c> before the
    /// progress channel when there is one.
    /// </summary>
    private static string[] Strings(List<string> tags, bool progress)
    {
        var strings = new string[tags.Count + (progress ? 2 : 1)];
        for (var i = 0; i < tags.Count; i++) strings[i] = (i == 0 ? "" : ",") + tags[i] + "=";
        if (progress) strings[tags.Count] = ";";
        strings[^1] = "";
        return strings;
    }

    /// <summary>An end's axes by tag; a missing value (the number 0 the core decomposes it into) has none.</summary>
    private static Dictionary<string, double> Axes(DecomposedValue value)
    {
        var axes = new Dictionary<string, double>();
        if (value.Kind != ValueKind.Complex || value.Numbers is not { } numbers || value.Strings is not { } strings) return axes;
        for (var i = 0; i < numbers.Length && i < strings.Length; i++)
        {
            if (strings[i].EndsWith('=')) axes[strings[i].TrimStart(',')[..^1]] = numbers[i];
        }
        return axes;
    }

    private static void Fill(DecomposedValue into, List<string> tags, Dictionary<string, double> axes, string[] strings, double progress)
    {
        var numbers = new double[tags.Count + 1];
        for (var i = 0; i < tags.Count; i++) numbers[i] = axes.TryGetValue(tags[i], out var value) ? value : double.NaN;
        numbers[^1] = progress;
        into.Kind = ValueKind.Complex;
        into.Numbers = numbers;
        into.Strings = strings;
        into.Unit = null;
    }

    /// <summary>One tween's axes, as its target's font declares them, and the last settings it wrote.</summary>
    private sealed class Frame
    {
        private readonly string[] _strings;
        private readonly OpenTypeTag[] _tags;
        private readonly double?[] _defaults;
        private readonly double[] _steps;
        private readonly double[] _values;
        private readonly FontVariation[] _variations;
        private FontVariationSettings? _last;

        public Frame(Tween tween)
        {
            var count = tween._numbers.Length - 1;
            _strings = tween._strings;
            _tags = new OpenTypeTag[count];
            _defaults = new double?[count];
            _steps = new double[count];
            _values = new double[count];
            _variations = new FontVariation[count];
            var axes = AxesOf(tween.Target);
            for (var i = 0; i < count; i++)
            {
                var tag = tween._strings[i].TrimStart(',')[..^1];
                _tags[i] = OpenTypeTag.Parse(tag);
                _steps[i] = 1;
                foreach (var axis in axes)
                {
                    if (axis.Tag != _tags[i]) continue;
                    _defaults[i] = axis.DefaultValue;
                    var range = axis.MaximumValue - axis.MinimumValue;
                    if (range is > 0 and < 10) _steps[i] = range / 100;
                }
            }
        }

        public bool Matches(Tween tween) => ReferenceEquals(_strings, tween._strings);

        public FontVariationSettings Write(Tween tween)
        {
            var progress = tween._numbers[^1];
            var changed = _last == null;
            for (var i = 0; i < _values.Length; i++)
            {
                var from = tween._fromNumbers[i];
                var to = tween._toNumbers[i];
                if (double.IsNaN(from)) tween._fromNumbers[i] = from = _defaults[i] ?? (double.IsNaN(to) ? 0 : to);
                if (double.IsNaN(to)) tween._toNumbers[i] = to = _defaults[i] ?? from;
                var value = tween._numbers[i];
                if (double.IsNaN(value)) value = from + (to - from) * progress;
                value = Quantize(value, _steps[i]);
                if (value == _values[i]) continue;
                _values[i] = value;
                changed = true;
            }
            if (!changed) return _last!;
            for (var i = 0; i < _values.Length; i++) _variations[i] = new FontVariation(_tags[i], _values[i]);
            return _last = new FontVariationSettings(_variations);
        }

        /// <summary>
        /// Rounds to whole axis units, or to a hundredth of an axis spanning fewer than 10 (<c>ital</c>), since each
        /// distinct position costs a cached typeface clone that Avalonia never evicts.
        /// </summary>
        public static double Quantize(double value, double step) => Math.Round(value / step) * step;

        private static IReadOnlyList<FontVariationAxis> AxesOf(object target)
        {
            if (target is not AvaloniaObject element) return [];
            var typeface = new Typeface(element.GetValue(TextElement.FontFamilyProperty), element.GetValue(TextElement.FontStyleProperty),
                element.GetValue(TextElement.FontWeightProperty), element.GetValue(TextElement.FontStretchProperty));
            return FontManager.Current.TryGetGlyphTypeface(typeface, out var glyphTypeface) ? glyphTypeface.VariationAxes : [];
        }
    }
}
