---
id: CORE-COLORS
title: Colors and color spaces
package: MonacoLabs.Presto
namespaces:
  - MonacoLabs.Presto.Values
sources:
  - MonacoLabs.Presto/Values/PrestoColor.cs
  - MonacoLabs.Presto/Values/ColorConversions.cs
  - MonacoLabs.Presto/Values/ColorSpace.cs
  - MonacoLabs.Presto/Values/HueInterpolation.cs
  - MonacoLabs.Presto/Values/OklabColor.cs
  - MonacoLabs.Presto/Values/OklchColor.cs
  - MonacoLabs.Presto/Core/Renderer.cs
types:
  - PrestoColor
  - OklabColor
  - OklchColor
  - ColorSpace
  - HueInterpolation
tests:
  - ColorsTests
  - PremultipliedColorTests
  - ColorConversionTests
  - ColorKeyframesTests
upstream:
  - src/core/colors.js
  - tests/suites/colors.test.js
depends:
  - CORE-VALUES
---

# Colors and color spaces

## Purpose

`PrestoColor` is the core's RGBA color (0–255 channels, 0–1 alpha, upstream `ColorArray`): it parses the hex, `rgb()` and `hsl()` strings a color tween decomposes (CORE-VALUES), formats the `rgba()` string a color tween writes, and carries the CSS Color 4 math (OKLab/OKLCH conversion, gamut mapping, `Mix` in four spaces, relative-color helpers). The renderer interpolates color tweens per channel, pseudo-linearly with premultiplied alpha. Color spaces are a utility, not a per-tween setting: CORE-KEYFRAMES's `TweenKeyframesBuilder.Color` expands a tween into keyframes on a `Mix` path.

## Requirements

### Parsing

- **CORE-COLORS-001** — `PrestoColor.TryParse` shall parse `#rgb`, `#rgba`, `#rrggbb` and `#rrggbbaa` in either case, doubling each digit of the short forms.
  - Tests: `ColorsTests.ConvertsFromToColorPairs`, `ColorsTests.AnimatesATypedColorMemberToTheParsedChannels`
- **CORE-COLORS-002** — When a hex color carries alpha, the parser shall take it as the byte over 255, rounded to 3 decimals; otherwise alpha shall be 1.
  - Tests: `ColorsTests.ConvertsFromToColorPairs`
- **CORE-COLORS-003** — When the string starts with `rgb`, the parser shall read `rgb(r, g, b)` with alpha 1 or `rgba(r, g, b, a)`, each channel a non-negative decimal or a percentage of 255 and alpha a decimal or a percentage of 1, whitespace allowed around each.
  - Tests: `ColorsTests.ConvertsFromToColorPairs`, `ColorsTests.AnimatesATypedColorMemberToTheParsedChannels`, `ColorsTests.ParsesDecimalAndPercentageRgbChannels`
- **CORE-COLORS-004** — When the string starts with `hsl`, the parser shall read `hsl(h, s%, l%)` or `hsla(h, s%, l%, a)` and convert it to RGB channels rounded to integers.
  - Tests: `ColorsTests.ConvertsFromToColorPairs`, `ColorsTests.AnimatesATypedColorMemberToTheParsedChannels`
- **CORE-COLORS-005** — When an `hsl` color's saturation is 0, the parser shall set R, G and B to the lightness times 255, rounded to an integer.
  - Tests: `ColorsTests.AchromaticHslScalesToByteChannels`
- **CORE-COLORS-006** — When the string is none of these forms, `TryParse` shall return false and `Parse` shall throw `FormatException`, so a tween to a string `ValueParser.IsColor` accepts but the parser does not fails when it is created.
  - Tests: `ColorsTests.UnknownStringsFailToParse`, `ColorsTests.TweeningToAnUnparsableColorThrows`
- **CORE-COLORS-042** — When an `hsl` color has no alpha, the parser shall set alpha to 1.
  - Tests: `ColorsTests.ConvertsFromToColorPairs`

### Channels and formatting

- **CORE-COLORS-007** — `ToChannels` shall return `[R, G, B, A]` and `FromChannels` shall build the color back from the first four entries.
  - Tests: `ColorsTests.ChannelsRoundTrip`
- **CORE-COLORS-008** — `FormatRgba` and `ToString` shall write `rgba(r,g,b,a)` with no spaces, R, G and B rounded half up to integers and alpha unrounded in JavaScript number form.
  - Tests: `ColorsTests.ToStringWritesCompactRgba`, `ColorsTests.FormatRgbaRoundsColorChannelsOnly`
- **CORE-COLORS-009** (inherited) — A `PrestoColor` constructed without an alpha shall have `A` = 1; the constructor shall neither clamp nor validate its channels.
  - Tests: `ColorsTests.ToStringWritesCompactRgba`

### Color tweens

- **CORE-COLORS-010** — While a color tween is between its ends and both ends have the same alpha, the renderer shall interpolate each RGB channel as `sqrt(lerp(c₀², c₁², p))`.
  - Tests: `PremultipliedColorTests.EqualAlphasMatchTheStraightBlendBitForBit`
- **CORE-COLORS-011** — While a color tween is between ends of different alpha and is not additive, the renderer shall interpolate each RGB channel with premultiplied alpha, `sqrt(lerp(a₀·c₀², a₁·c₁², p) / lerp(a₀, a₁, p))`, so a fade from or to transparent keeps the opaque end's RGB.
  - Tests: `PremultipliedColorTests.TransparentToOpaqueTakesTheOpaqueHueAtEveryMidFrame`, `PremultipliedColorTests.OpaqueToTransparentKeepsTheStartRgbUntilTheEnd`
- **CORE-COLORS-012** — The renderer shall interpolate a color tween's alpha linearly.
  - Tests: `PremultipliedColorTests.TransparentToOpaqueTakesTheOpaqueHueAtEveryMidFrame`
- **CORE-COLORS-013** — At progress 0 and 1 the renderer shall write the from and to channels exactly.
  - Tests: `PremultipliedColorTests.EndsAreExactToTheBit`
- **CORE-COLORS-014** — When the interpolated alpha is 0 or below, the renderer shall fall back to the straight blend, and shall clamp a negative sum under the square root to 0, so no channel is ever NaN.
  - Tests: `PremultipliedColorTests.TransparentPairsAndOvershootingEasesNeverProduceNaN`
- **CORE-COLORS-015** — When a color tween's composition is `Additive`, the renderer shall use the straight blend whatever the alphas.
  - Tests: `PremultipliedColorTests.AdditiveColorTweensKeepTheStraightBlend`
- **CORE-COLORS-016** — When a color tween carries per-channel eases, the renderer shall ease each of the four channels by its own ease.
  - Tests: `PremultipliedColorTests.RetargetingAChannelFadeBetweenAlphasCarriesOnWithoutAJump`
- **CORE-COLORS-017** — The renderer shall apply the tween's `Modifier` to each of the four interpolated channels.
  - Tests: `ColorsTests.TheModifierAppliesToEveryChannelAlphaIncluded`
- **CORE-COLORS-018** — The renderer shall not round color channels; a `PrestoColor` member receives the unrounded channels and a string target the `FormatRgba` string.
  - Tests: `ObjectTargetAdapterTests.WritesColorChannelsToColorAndStringMembers`, `ColorsTests.AnimatesATypedColorMemberToTheParsedChannels`

### Conversions and gamut mapping

- **CORE-COLORS-019** — `ToOklab` shall convert through linear sRGB (the piecewise CSS transfer function) to OKLab with culori's matrices, carrying alpha.
  - Tests: `ColorConversionTests.ConversionsMatchCulori`
- **CORE-COLORS-020** — `ToOklch` shall give chroma as the OKLab a/b length and hue in degrees in [0, 360).
  - Tests: `ColorConversionTests.ConversionsMatchCulori`
- **CORE-COLORS-021** — When an OKLab or OKLCH color is inside the sRGB gamut, `FromOklab` and `FromOklch` shall return its channels unrounded, round-tripping `ToOklab`/`ToOklch` within 1e-9.
  - Tests: `ColorConversionTests.InGamutColorsRoundTrip`
- **CORE-COLORS-022** — When the color is outside the gamut, `FromOklab` and `FromOklch` shall map it as CSS Color 4 does: white for L ≥ 1, black for L ≤ 0, otherwise a bisection of chroma at constant L and hue to within 1e-4, accepting a candidate whose clip lies within deltaEOK 0.02, and returning the last candidate clipped to 0–255.
  - Tests: `ColorConversionTests.GamutMappingMatchesCulori`
- **CORE-COLORS-023** — `FromOklab` and `FromOklch` shall pass `alpha` through unchanged.
  - Tests: `ColorConversionTests.InGamutColorsRoundTrip`

### Mix

- **CORE-COLORS-024** — When `t` is 0 or 1, `Mix` shall return `from` or `to` exactly.
  - Tests: `ColorConversionTests.EndsAreExact`
- **CORE-COLORS-025** — `Mix` shall default to `ColorSpace.Oklab` and `HueInterpolation.Shorter`.
  - Tests: `ColorConversionTests.TransparentToRedIsRedAtHalfAlphaHalfway`
- **CORE-COLORS-026** — `Mix` shall interpolate the space's color channels with premultiplied alpha and alpha linearly, clamping the result's alpha to 0–1.
  - Tests: `ColorConversionTests.TransparentToRedIsRedAtHalfAlphaHalfway`, `ColorConversionTests.ExtrapolationStaysInGamut`
- **CORE-COLORS-027** (inherited) — When the interpolated alpha is exactly 0, `Mix` shall take the channels of the nearer end (`from` below `t` = 0.5) instead of dividing.
  - Tests: `ColorConversionTests.AFullyTransparentMixTakesTheNearerEndsChannels`
- **CORE-COLORS-028** — In `Srgb` and `LinearSrgb`, `Mix` shall interpolate gamma-encoded or linear-light channels, and shall gamut map a result outside sRGB through OKLab.
  - Tests: `ColorConversionTests.MixMatchesCulori`
- **CORE-COLORS-029** — In `Oklab`, `Mix` shall interpolate L, a and b along a straight line and gamut map the result.
  - Tests: `ColorConversionTests.MixMatchesCulori`
- **CORE-COLORS-030** — In `Oklch`, `Mix` shall interpolate L and C and move the hue along the arc the `HueInterpolation` picks by CSS Color 4: `Shorter` ≤ 180°, `Longer` ≥ 180°, `Increasing` and `Decreasing` in their direction.
  - Tests: `ColorConversionTests.HueModesTakeTheirArcFrom350To10`, `ColorConversionTests.MixMatchesCulori`
- **CORE-COLORS-031** — When one end's OKLCH chroma is below 4e-4, `Mix` shall treat its hue as powerless and use the other end's hue; when both are, both hues shall be 0.
  - Tests: `ColorConversionTests.WhiteToBlueInOklchKeepsBluesHue`
- **CORE-COLORS-032** — When both ends are chromatic with hues within 1e-9 and the mode is `Increasing` or `Decreasing`, `Mix` shall travel a full turn (+360° or -360°) instead of none.
  - Tests: `ColorConversionTests.EqualHuesMakeAFullTurnWhenIncreasingOrDecreasing`
- **CORE-COLORS-033** — When `t` lies outside 0–1, `Mix` shall extrapolate and return a gamut-mapped color.
  - Tests: `ColorConversionTests.ExtrapolationStaysInGamut`
- **CORE-COLORS-034** — `Mix` shall agree with the culori fixtures within 1e-6 per channel in every space and hue mode.
  - Tests: `ColorConversionTests.MixMatchesCulori`
- **CORE-COLORS-035** — The hue arc that an `Oklch` mix travels shall be the absolute difference of the resolved hues, 360 for a full turn; CORE-KEYFRAMES sizes its color segments from it.
  - Tests: `ColorKeyframesTests.PerTargetSegmentCountsFollowTheHueArc`, `ColorKeyframesTests.AnOklchFullTurnCirclesTheHue`
- **CORE-COLORS-043** — When one end of a `Mix` is fully transparent and the other is not, the result shall carry the other end's color, with only the alpha interpolating, as `Mix(rgba(0,0,0,0), rgb(255,0,0), 0.5)` gives `rgba(255,0,0,0.5)`.
  - Tests: `ColorConversionTests.TransparentToRedIsRedAtHalfAlphaHalfway`

### Relative colors

- **CORE-COLORS-036** — `WithOklch` shall replace the given OKLCH channels, keep the others and the alpha, floor chroma at 0, use hue 0 for a gray (chroma below 4e-4) whose hue is not given, and gamut map the result.
  - Tests: `ColorConversionTests.RelativeHelpersMatchCulori`
- **CORE-COLORS-037** — `ShiftOklch` shall add the given amounts to L, C and H (a gray's hue counting as 0), floor chroma at 0, keep the alpha and gamut map the result.
  - Tests: `ColorConversionTests.RelativeHelpersMatchCulori`
- **CORE-COLORS-038** — `Mix`, `ShiftOklch`, `WithOklch` and the conversions shall not allocate.
  - Tests: `AllocationTests.ColorMixAndShiftWithoutAllocating`

### Defaults

- **CORE-COLORS-039** — When `alpha` is not given, `FromOklab` and `FromOklch` shall return an opaque color (alpha 1).
  - Tests: `ColorConversionTests.FromOklabAndFromOklchDefaultToOpaque`
- **CORE-COLORS-040** — When an amount is not given, `ShiftOklch` shall leave that channel unshifted.
  - Tests: `ColorConversionTests.ShiftOklchLeavesTheChannelsItIsNotGiven`
- **CORE-COLORS-041** — When an `OklabColor` or `OklchColor` is constructed without an alpha, its alpha shall be 1.
  - Tests: `ColorConversionTests.OklabAndOklchColorsDefaultToOpaque`

## Invariants

- Every color returned by `Mix`, `FromOklab`, `FromOklch`, `WithOklch` and `ShiftOklch` has R, G and B in 0–255 and is never NaN; `Mix`'s alpha is in 0–1.
- A color tween's `Numbers` are always four channels, R, G, B and A.

## Edge cases

- Decimal and percentage `rgb` channels may be mixed in one color and are neither clamped nor rounded, so `"rgb(300, 0, 0)"` parses to R 300.
- The `rgb` and `hsl` prefixes are matched case-sensitively, while the patterns after them are case-insensitive and unanchored, so trailing text after a well-formed color is ignored by `Parse`.
- `rgb(r, g, b, a)` (four values without `rgba`) does not parse.
- Interpolated alpha below 0 from an overshooting ease is written as is; only `Mix` clamps alpha.
- `FromOklab`/`FromOklch` do not clamp `alpha`.

## Threading and lifetime

- `PrestoColor` and the conversions are immutable values and pure functions, safe on any thread.

## Reduced motion

- Not affected: color math does not depend on duration.

## Allocation

- The color tween tick allocates nothing into a `PrestoColor` member (`AllocationTests.ColorTweenTicksWithoutAllocating`); a string target allocates the `rgba()` string per frame. `ToChannels` allocates its array.

## Provenance

- Premultiplied alpha when the ends' alphas differ, because upstream's straight alpha shows a transparent end's RGB mid-fade (see "Colors interpolate with premultiplied alpha"). Equal alphas and `Additive` keep upstream's expression bit for bit.
- Decimal and percentage `rgb` channels parse where upstream throws, and achromatic `hsl` is scaled to 0–255 where upstream leaves 0–1 (see "Color strings parse as CSS does, or throw").
- `Mix`, OKLab/OKLCH, gamut mapping and the relative-color helpers have no upstream counterpart: they exist for the hue path no straight-line blend gives (see "Color spaces are a utility, not a per-tween setting"). The full turn for equal hues deviates from CSS Color 4 so a hue cycle can be written as one mix (see "Equal hues make a full turn").

## Decisions

- **Color strings parse as CSS does, or throw.** `rgb` channels accept decimals and percentages, achromatic `hsl` scales to 0–255 like the chromatic branch, and `Parse` throws `FormatException` on anything else. Reason: CSS accepts these forms, and upstream's achromatic branch is a bug that renders grays near black; a silent opaque black hid typos until they showed on screen. Rejected: upstream parity on either (it throws a `TypeError` on the `rgb` forms and keeps the unscaled grays); returning opaque black.
- **Colors interpolate with premultiplied alpha.** A color tween mixes RGB as `sqrt(lerp(a₀·c₀², a₁·c₁², p) / lerp(a₀, a₁, p))` with alpha linear, as CSS Color 4 does; equal alphas take the original expression, ends are exact and a lerped alpha of 0 or below falls back to the straight lerp. `Additive` composition and color slots inside complex values stay straight; `Mix` premultiplies the same way in every space (hue excluded). Reason: with straight alpha a fade from transparent shows the transparent color's RGB (transparent teal to gray flashed green); equal alphas stay bit-identical, so upstream tests and opaque tweens are unchanged. Rejected: anime.js's straight-alpha mix.
- **The core parses colors.** `PrestoColor` parses `#rgb`, `#rrggbb(aa)`, `rgb(a)()` and `hsl(a)()`; typed structs decompose through their codec as complex values. Reason: color strings are part of the value syntax on every target, including plain objects with no UI framework to parse them; it also lets upstream `colors.test.js` run against the core.
- **Color spaces are a utility, not a per-tween setting.** The CSS Color 4 math lives on `PrestoColor` (`Mix` in four spaces) and reaches tweens only as keyframes through CORE-KEYFRAMES's `Color` expander; tweens, the renderer, codecs, composition, `Animatable` and the compositor are unchanged, and gradients and shadows keep lerping color slots in plain sRGB. Reason: measured, the default premultiplied blend already matches OKLab lightness (midpoints within .01 L) and complementary pairs go gray in every straight-line space, so the real new capability is the hue path (OKLCH arcs, even-lightness sweeps), which keyframes express; existing tests and fixtures stay untouched. Rejected: a color space on every tween (per-tween space buffers, blending, `Animatable` retargets and codec color slots in the space), designed first and dropped once measured as buying little for its cost.
- **Own implementation, no package.** The conversions are an internal static class written from the CSS Color 4 formulas and Ottosson's OKLab matrices. Reason: the core has no package references and stays that way; the formulas are fully specified. Rejected: Wacton.Unicolor (large, object-per-color, the core's first dependency); Colorful (color science, not CSS mixing); the UI framework's HSL/HSV colors (bridge-only, wrong spaces).
- **culori is the reference implementation.** `culori` sits beside `animejs` in the fixture generator, produces `colors.json`, and the tests assert against it within 1e-6. Reason: it implements CSS Color 4 interpolation and gamut mapping, and checking against an independent implementation catches mistakes that hand-written expected values would share with the code.
- **Four spaces, no `Default` member.** `ColorSpace` has `Srgb`, `LinearSrgb`, `Oklab` and `Oklch` only; with no `Default`, the expander is opt-in and a plain color tween keeps its blend. Reason: OKLab and OKLCH supersede HSL, HWB and CIE Lab/LCH for interpolation, and the bridge renders in sRGB, so wide gamuts are pointless. Rejected: HSL, HWB, CIE Lab/LCH (deferred); Display-P3 and other wide gamuts.
- **Hue interpolation follows CSS Color 4.** `HueInterpolation` is `Shorter` (default), `Longer`, `Increasing` or `Decreasing`, applied in `Oklch` only, unwrapping the end hue by ±360° once. Reason: follows CSS Color 4.
- **Hue is powerless below chroma 4e-4.** An end with smaller chroma takes the other end's hue, and two achromatic ends take 0; the fixture generator applies the same threshold before calling culori. Reason: CSS's achromatic threshold, roughly where sRGB grays land, so white to blue stays blue instead of passing through an unrelated hue; culori treats hue as missing only at exactly zero chroma.
- **Gamut mapping is CSS Color 4's, not a clip.** Out-of-gamut results become white for L ≥ 1, black for L ≤ 0, else chroma is bisected down to within deltaEOK 0.02 of its clip; in-gamut colors and sRGB mixes of in-gamut colors skip it. Reason: OKLCH arcs and the adjusters leave sRGB easily, and a per-channel clip shifts hue and flattens sweeps. Rejected: per-channel clipping.
- **`Mix` extrapolates; its ends are exact.** `t` outside 0–1 is allowed and gamut mapped; `t` of 0 and 1 return the inputs, not a round trip. Reason: overshooting eases go past the ends, and exact ends keep completed tweens on their target.
- **Equal hues make a full turn.** Under `Increasing` or `Decreasing`, two chromatic ends with hues within 1e-9 travel ±360° instead of CSS's zero arc; a gray end does not trigger it, and the fixture generator skips the case. Reason: a full turn is the hue-cycle demo's whole point (a color mixed with itself under `Increasing` cycles the wheel).
- **Relative colors are two helpers, not syntax.** `WithOklch` replaces channels and `ShiftOklch` adds to them, both gamut mapped. Reason: derives hover, pressed and muted states from one base color in recipes and tokens, so the states stay visibly related. Rejected: CSS Color 5 relative-color string syntax.
- **A color expansion keeps the ease exact.** See CORE-KEYFRAMES.
- **Segments are fitted to the path.** See CORE-KEYFRAMES.

## Non-goals

- A per-tween color space or hue mode: color paths reach tweens only through CORE-KEYFRAMES's `Color` expander (see "Color spaces are a utility, not a per-tween setting").
- Named colors, `hwb()`, `lab()`, `oklch()` string syntax, wide-gamut output.
- Color slots inside complex values (shadows, gradients) are interpolated as plain numbers, not as colors.

## Open items

- CORE-COLORS-027 holds for `Srgb`, `LinearSrgb` and `Oklab` only: in `Oklch` a fully transparent `Mix` takes L and C from the nearer end but still interpolates the hue (hue is never premultiplied), so the result is not the nearer end's color. Either the requirement names the exception or `Mix` takes the nearer hue too.
