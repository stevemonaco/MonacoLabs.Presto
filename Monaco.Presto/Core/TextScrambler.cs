using System.Runtime.CompilerServices;
using System.Text;

namespace Monaco.Presto.Core;

/// <summary>
/// Port of anime.js <c>scrambleText</c>: the property value that fills a target's text with random characters and settles
/// them, one at a time along a reveal wave, on the text the scramble ends with.
/// </summary>
/// <remarks>
/// The text the scramble starts from is read from the target's <c>Text</c> property whichever property the animation
/// writes, as upstream reads <c>textContent</c>. Upstream's function-based <c>chars</c>, <c>text</c>, <c>duration</c> and
/// <c>delay</c> and its <c>onChange</c> callback are not ported.
/// </remarks>
internal static class TextScrambler
{
    private static readonly Dictionary<string, string> s_namedSets = new()
    {
        ["lowercase"] = "a-z",
        ["uppercase"] = "A-Z",
        ["numbers"] = "0-9",
        ["symbols"] = "!%#_|*+=",
        ["braille"] = "⠀-⣿",
        ["blocks"] = "▀-▟",
        ["shades"] = "░-▓",
    };

    private static readonly ConditionalWeakTable<object, string> s_originalTexts = new();

    internal static ChainedFunctionValue Create(ScrambleTextParams parameters)
    {
        var cursor = parameters.Cursor ?? "";
        var interval = 1000 * Globals.TimeScale / parameters.RevealRate;
        var settleDuration = parameters.SettleDuration ?? 300 * Globals.TimeScale;
        var characters = Expand(parameters.Chars);

        string? overrideChars = null;
        var blankStart = false;
        if (parameters.Override)
        {
            if (parameters.OverrideChars == null) overrideChars = characters;
            else if (parameters.OverrideChars.Length == 0) blankStart = true;
            else overrideChars = Expand(parameters.OverrideChars);
        }

        return (target, index, total, previous) =>
        {
            var ease = (parameters.Ease ?? (EaseValue)Eases.None).Resolve(target, index, total, out _);
            var rng = Utils.CreateSeededRandom(parameters.Seed);

            var currentText = Presto.Get(target, "Text") as string ?? "";
            if (!s_originalTexts.TryGetValue(target, out var originalText)) s_originalTexts.Add(target, originalText = currentText);
            var startingText = previous?._value as string ?? currentText;
            var settledText = parameters.Text ?? (previous != null ? startingText : originalText);
            var startLength = startingText.Length;
            var endLength = settledText.Length;

            // Starting from blank animates the target text's length only, so nothing is padded beyond it.
            var animLength = blankStart ? endLength : Math.Max(startLength, endLength);
            var delay = parameters.Delay;
            var revealDelay = parameters.RevealDelay;
            var animDuration = parameters.Duration is > 0 ? parameters.Duration.Value : ((animLength - 1) * interval) + settleDuration;
            var duration = Helpers.Round((animDuration + revealDelay) / Globals.TimeScale, 0) * Globals.TimeScale;
            var revealDelayRatio = revealDelay > 0 ? Helpers.Round(revealDelay / duration, 12) : 0;

            var from = parameters.From == ScrambleFrom.Auto ? (endLength < startLength ? ScrambleFrom.Right : ScrambleFrom.Left) : parameters.From;
            var charOrder = new int[animLength];
            if (parameters.FromIndex == null && from == ScrambleFrom.Random)
            {
                for (var i = 0; i < animLength; i++) charOrder[i] = i;
                for (var i = animLength - 1; i > 0; i--)
                {
                    var j = (int)rng(0, i);
                    (charOrder[i], charOrder[j]) = (charOrder[j], charOrder[i]);
                }
            }
            else
            {
                var spanned = (blankStart || startLength == 0 ? animLength : startLength) - 1;
                var reference = parameters.FromIndex ?? from switch
                {
                    ScrambleFrom.Right => spanned,
                    ScrambleFrom.Center => spanned / 2d,
                    _ => 0,
                };
                var indices = new int[animLength];
                for (var i = 0; i < animLength; i++) indices[i] = i;
                // The index breaks ties the way upstream's stable sort leaves them.
                Array.Sort(indices, (a, b) =>
                {
                    var distance = Math.Abs(a - reference).CompareTo(Math.Abs(b - reference));
                    return distance != 0 ? distance : a.CompareTo(b);
                });
                for (var i = 0; i < animLength; i++) charOrder[indices[i]] = i;
            }
            if (parameters.Reversed)
            {
                var last = animLength - 1;
                for (var i = 0; i < animLength; i++) charOrder[i] = last - charOrder[i];
            }

            // The fraction of the animation one character spends scrambling, and the gap between two entering that zone.
            var settleRatio = Helpers.Round(settleDuration / animDuration, 12);
            var settleSpacing = Helpers.Round((1 - settleRatio) / animLength, 12);
            var cursorZone = cursor.Length * settleSpacing;
            var stepRatio = Helpers.Round(1000 * Globals.TimeScale / (parameters.SettleRate * duration), 12);

            var charStarts = new double[animLength];
            var charEnds = new double[animLength];
            var scale = parameters.Perturbation > 0 ? parameters.Perturbation * settleRatio : 0;
            for (var c = 0; c < animLength; c++)
            {
                var startOffset = scale > 0 ? (rng(0, 2000) - 1000) / 1000 * scale : 0;
                var endOffset = scale > 0 ? (rng(0, 2000) - 1000) / 1000 * scale : 0;
                charStarts[c] = (charOrder[c] * settleSpacing) + startOffset;
                charEnds[c] = Math.Ceiling((charStarts[c] + settleRatio + endOffset) / stepRatio) * stepRatio;
            }

            // Text shrinking in a non-sequential order settles its characters only once every extra one has gone.
            if (endLength < animLength && (parameters.FromIndex != null || from == ScrambleFrom.Center))
            {
                var maxExtraEnd = 0d;
                for (var c = endLength; c < animLength; c++) maxExtraEnd = Math.Max(maxExtraEnd, charEnds[c]);
                var revealOrder = new int[endLength];
                for (var c = 0; c < endLength; c++) revealOrder[c] = c;
                Array.Sort(revealOrder, (a, b) => charOrder[a].CompareTo(charOrder[b]));
                var targetSpacing = (1 - maxExtraEnd) / endLength;
                for (var i = 0; i < endLength; i++)
                {
                    var revealTime = maxExtraEnd + (i * targetSpacing);
                    if (revealTime > charEnds[revealOrder[i]]) charEnds[revealOrder[i]] = revealTime;
                }
            }

            var charCache = new char[animLength];
            for (var c = 0; c < animLength; c++) charCache[c] = characters[(int)rng(0, characters.Length - 1)];
            var overrideCache = overrideChars == null ? null : ReferenceEquals(overrideChars, characters) ? charCache : new char[animLength];
            if (overrideCache != null && overrideCache != charCache)
            {
                for (var c = 0; c < animLength; c++) overrideCache[c] = overrideChars![(int)rng(0, overrideChars.Length - 1)];
            }

            var startText = startingText;
            if (previous == null)
            {
                if (blankStart)
                {
                    startText = "";
                }
                else if (overrideCache != null)
                {
                    var initial = new StringBuilder(startLength);
                    for (var c = 0; c < startLength; c++) initial.Append(startingText[c] == ' ' ? ' ' : overrideCache[c]);
                    startText = initial.ToString();
                }
            }

            var scrambled = "";
            var text = new StringBuilder(animLength);
            var lastValue = -1d;
            var lastStep = -1;

            string Scramble(double v)
            {
                if (v == lastValue) return scrambled;
                lastValue = v;
                if (v <= 0) return scrambled = delay > 0 ? startingText : startText;
                if (v >= 1) return scrambled = settledText;

                text.Clear();
                // The scramble characters are only drawn again on a settle rate step boundary.
                var step = (int)(v / stepRatio);
                var refresh = step != lastStep;
                if (refresh) lastStep = step;
                var linear = revealDelayRatio > 0 ? (v - revealDelayRatio) / (1 - revealDelayRatio) : v;
                var t = linear > 0 ? ease(linear) : 0;

                for (var c = 0; c < animLength; c++)
                {
                    var charStart = charStarts[c];
                    if (t >= charEnds[c])
                    {
                        if (c < endLength) text.Append(settledText[c]);
                        continue;
                    }
                    if (t <= 0 || t < charStart)
                    {
                        if (blankStart || c >= startLength) continue;
                        if (overrideChars == null)
                        {
                            text.Append(startingText[c]);
                        }
                        else if (startingText[c] == ' ')
                        {
                            text.Append(' ');
                        }
                        else
                        {
                            if (refresh) overrideCache![c] = overrideChars[(int)rng(0, overrideChars.Length - 1)];
                            text.Append(overrideCache![c]);
                        }
                        continue;
                    }
                    if ((c < endLength && settledText[c] == ' ') || (c < startLength && startingText[c] == ' '))
                    {
                        text.Append(' ');
                    }
                    else if (t - charStart < cursorZone)
                    {
                        text.Append(cursor[cursor.Length - 1 - (int)((t - charStart) / settleSpacing)]);
                    }
                    else
                    {
                        if (refresh) charCache[c] = characters[(int)rng(0, characters.Length - 1)];
                        text.Append(charCache[c]);
                    }
                }
                return scrambled = text.ToString();
            }

            return new TweenParams
            {
                From = 0,
                To = 1,
                Duration = duration,
                Delay = delay,
                Ease = Eases.None,
                TextModifier = Scramble,
            };
        };
    }

    private static string Expand(string chars)
    {
        var set = s_namedSets.GetValueOrDefault(chars, chars);
        var expanded = new StringBuilder(set.Length);
        for (var i = 0; i < set.Length; i++)
        {
            if (i + 2 < set.Length && set[i + 1] == '-' && set[i] < set[i + 2])
            {
                for (int c = set[i], last = set[i + 2]; c <= last; c++) expanded.Append((char)c);
                i += 2;
            }
            else
            {
                expanded.Append(set[i]);
            }
        }
        return expanded.ToString();
    }
}
