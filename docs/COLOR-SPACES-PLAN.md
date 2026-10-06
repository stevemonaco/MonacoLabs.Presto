# Color interpolation spaces

Phase 30 in [PLAN.md](PLAN.md). Status: **planned**. This plan replaces an earlier design that added a color space to
every tween (per-tween space buffers, blend composition and `Animatable` retargets in the space, color slots in
gradient and shadow codecs). Measuring that design against today's behavior showed it bought little. The plan is now
CSS Color 4 color math as a public utility, plus a keyframe expander that makes any color tween follow a perceptual
path through the existing renderer.

| Phase | Scope | Size |
|---|---|---|
| 30 | Core: CSS Color 4 conversions, gamut mapping and mixing on `PrestoColor`; OKLCH adjusters; a color-path keyframe expander; `culori` fixtures. Sample: hue-cycle and status-flip demos | small–medium |

## 0. Why the per-tween design was dropped (measured)

- **`Default` is not plain sRGB.** Solid colors blend pseudo-linearly with premultiplied alpha (PLAN decision 117),
  `sqrt(lerp(c²))`, which approximates linear light. Red → green at 50% renders (180,180,0), OKLab L .75, exactly the
  lightness an OKLab blend gives, (208,168,0). The olive dip (128,128,0), L .58, is plain sRGB, which only gradient and
  shadow color slots use.
- **OKLab vs `Default` is a wash in practice.** Across red → green, indigo → rose, blue-600 → orange-500 and slate →
  teal, the midpoints differ by a small hue shift and at most .01 in L; `Default` is sometimes the more saturated one.
  No one sees this in a 300 ms transition.
- **Complementary pairs go gray in every straight-line space.** Blue → yellow is C 0 in sRGB and `Default`, and C .08
  in OKLab. Only OKLCH, which travels around the hue wheel, keeps the color saturated.
- **So the new capability is the hue path, not the space.** OKLCH arcs and even-lightness hue sweeps are something
  Presto cannot do today. Both can be expressed as color keyframes, and with short enough segments `Default`'s blend
  between them is indistinguishable from the true curve (§4 pins the bound). Per-tween buffers, blend-composition deltas
  in the space, retarget rules and codec color slots are not needed for that.

## 1. Decisions

1. **Own implementation, no package.** The core has no package references and stays that way. CSS Color 4 specifies
   every formula, and Björn Ottosson publishes the OKLab matrices; the whole thing is about 300 lines in one internal
   static class. Considered and rejected: Wacton.Unicolor (complete and MIT, but large, object-per-color, and it would
   be the core's first dependency), Colorful (color science, not CSS mixing), and Avalonia's `HslColor`/`HsvColor`
   (bridge-only, wrong spaces).
2. **`culori` is the reference.** It implements CSS Color 4 interpolation and gamut mapping. It joins `animejs` in
   `tools/anime-fixtures/` as a dev dependency only, writes `colors.json`, and the C# tests assert against it within
   1e-6, the same parity method as the anime.js port.
3. **Spaces: `Srgb`, `LinearSrgb`, `Oklab`, `Oklch`.** These are the CSS `srgb`, `srgb-linear`, `oklab` and `oklch`
   interpolation spaces. HSL, HWB and CIE Lab/LCH are left out; OKLab and OKLCH supersede them for interpolation, and
   they are cheap to add later from the same conversion table (§6). Display-P3 and other wide gamuts are out: Avalonia
   renders in sRGB. There is no `Default` member; the expander is opt-in, and a plain color tween is today's blend.
4. **Hue interpolation follows CSS Color 4.** `HueInterpolation.Shorter` (default), `Longer`, `Increasing`,
   `Decreasing`, applied in `Oklch` only. The end hue is unwrapped by ±360 once, so the lerp takes the chosen arc.
5. **Powerless hue.** When one end's OKLCH chroma is below 4e-4 (CSS's achromatic threshold, roughly where sRGB grays
   land), its hue takes the other end's; both achromatic take 0. White → blue stays blue-ish instead of passing through
   an unrelated hue. culori marks achromatic hues as missing only at exactly zero chroma, so the fixture generator applies
   the same threshold before calling it, documented in the generator.
6. **Premultiplied alpha.** As CSS does and as `Default` does since decision 117: the color channels (L, a, b; or L, C,
   not h; or R, G, B) are multiplied by alpha before the lerp and divided after. Alpha lerps linearly.
7. **Gamut mapping is CSS Color 4's, not a clip.** OKLCH arcs and OKLCH adjusters leave the sRGB gamut easily, and a
   per-channel clip shifts hue and flattens sweeps. Out-of-gamut results use the spec's algorithm: L ≥ 1 is white,
   L ≤ 0 is black, otherwise binary-search the chroma down (ε 1e-4) until the clipped color is within deltaEOK 0.02
   (the JND) of the reduced one, and return the clipped color. Colors already in gamut skip the search. `Srgb` and
   `LinearSrgb` mixes of in-gamut colors never leave the gamut and skip it entirely.
8. **Mix extrapolates.** `t` outside [0, 1] is allowed (overshooting eases go past the ends), and the result is gamut
   mapped. Ends are exact: `t = 0` and `t = 1` return the inputs, not a round trip through the space.
9. **The expander keeps the ease exact.** Expanding a color tween into N equal-duration segments with the tween's ease
   on each segment would restart the ease N times. Instead, with the tween ease E, segment i's end color is
   `Mix(from, to, E((i+1)/N))` and its ease is the slice of E rescaled to 0–1:
   `e_i(s) = (E((i+s)/N) − E(i/N)) / (E((i+1)/N) − E(i/N))`. Timing is exactly E's; only the color between segment
   ends is approximated by the pseudo-linear blend. A flat slice (`Steps`, a hold) gets a hold ease. A spring resolves to
   its ease and its settling duration first, as a plain tween does. The color path is approximate only between segment
   ends, and the segment count bounds it (decision 10).
10. **Segment count is adaptive.** `Srgb`, `LinearSrgb` and `Oklab`: 4 (the blend between segment ends is
    pseudo-linear, so even `Srgb` needs a few). `Oklch`: `ceil(arc / 30°)`, at least 4, at most 24. An explicit
    `segments:` overrides. §4 pins the worst deviation of the rendered path from the true mix below deltaEOK
    0.02 for the defaults.
11. **Both ends must be known.** The expander runs at build time and cannot read a target's current color, so `from`
    is required. "From the current color" needs a per-tween setup hook, which is the dropped design (§6). Function
    values are allowed for `from` and `to` (`Stagger`, per-target colors); the expander then produces function-valued
    keyframes that resolve per target.
12. **Relative colors (CSS Color 5) become two helpers, not syntax.** `WithOklch` replaces channels and `ShiftOklch`
    adds to them, both gamut mapped. They are for deriving hover, pressed and muted states from one base color in
    recipes and `MotionTokens`, so derived states stay visibly related. There is no relative-color string syntax.
13. **Nothing changes for existing tweens.** No renderer, codec, composition, `Animatable` or compositor change; every
    existing test and fixture is untouched. Gradients and box shadows keep lerping their color slots in plain sRGB; the
    expander does not apply to complex values (§6).

## 2. API

```csharp
public enum ColorSpace { Srgb, LinearSrgb, Oklab, Oklch }
public enum HueInterpolation { Shorter, Longer, Increasing, Decreasing }

public readonly record struct OklchColor(double L, double C, double H, double A = 1);   // L 0–1, C ≥ 0, H degrees
public readonly record struct OklabColor(double L, double A, double B, double Alpha = 1);

// PrestoColor
public static PrestoColor Mix(PrestoColor from, PrestoColor to, double t,
                              ColorSpace space = ColorSpace.Oklab, HueInterpolation hue = HueInterpolation.Shorter);
public OklchColor ToOklch();
public OklabColor ToOklab();
public static PrestoColor FromOklch(double l, double c, double h, double alpha = 1);    // gamut mapped
public static PrestoColor FromOklab(double l, double a, double b, double alpha = 1);    // gamut mapped
public PrestoColor WithOklch(double? l = null, double? c = null, double? h = null);     // relative: replace
public PrestoColor ShiftOklch(double dl = 0, double dc = 0, double dh = 0);             // relative: add
// TweenValue
public static implicit operator TweenValue(PrestoColor value);                         // as its rgba() string

// TweenKeyframesBuilder: expands one from → to color tween into segments (decisions 9–11)
public TweenKeyframesBuilder Color(TweenValue from, TweenValue to, ColorSpace space = ColorSpace.Oklab,
                                   HueInterpolation hue = HueInterpolation.Shorter, TweenValue? duration = null,
                                   TweenValue? delay = null, EaseValue? ease = null, int? segments = null);

// MotionTokens (bridge): the space and hue recipes pass to Color(...) for their color states
ColorSpace ColorSpace = ColorSpace.Oklab, HueInterpolation Hue = HueInterpolation.Shorter
```

```csharp
// Status pill: blue → amber without passing through gray
pill.Animate(b => b.Property("Background", k => k.Color("#2563eb", "#f59e0b", ColorSpace.Oklch, duration: 600, ease: Eases.OutCubic)));

// Even-brightness hue cycle: a full turn at fixed L and C, looped
var start = PrestoColor.FromOklch(0.7, 0.15, 0);
glow.Animate(b => b.Property("Background", k => k.Color(start, start, ColorSpace.Oklch, HueInterpolation.Increasing,
                                                        duration: 8000, ease: Eases.None))
                   .Loop());

// Derived states
var hover = accent.ShiftOklch(dl: 0.06);
var muted = accent.WithOklch(c: 0.04);
```

**A full turn.** CSS gives `Increasing` between equal hues a zero arc. A full turn is the hue-cycle demo's whole point,
so when `from` and `to` have the same hue (within 1e-9) and the mode is `Increasing` or `Decreasing`, the expander and
`Mix` take 360°. This deviates from CSS and is recorded in §7 when built; the fixture generator skips that case.

`Mix` and the adjusters are pure and allocation-free. The expander allocates keyframes at build time only.

## 3. Implementation steps

1. **`ColorConversions` (internal, static, allocation-free).** sRGB ↔ linear sRGB (the piecewise transfer function),
   linear sRGB ↔ OKLab (Ottosson's matrices and cube roots), OKLab ↔ OKLCH, premultiply/unpremultiply, the hue unwrap
   and powerless rule, deltaEOK, and the CSS Color 4 gamut map. Doubles in, doubles out; no spans needed for four values.
2. **Public surface on `PrestoColor`** (§2), the enums and the two color structs, each in its own file under `Values/`.
3. **Expander** on `TweenKeyframesBuilder`: resolve the ease (and a spring's duration), pick N, emit N `TweenParams`
   with the segment colors and slice eases. Function-valued ends produce function-valued keyframes that resolve both
   ends per target, then mix.
4. **`MotionTokens`** gains `ColorSpace` and `Hue`; the recipes with color states use `Color(...)` with them.
5. **Fixtures.** Add `culori` to `tools/anime-fixtures/package.json`; `generate.js` writes `colors.json`: conversions for
   a color set (primaries, secondaries, grays, near-grays, a few brand colors, alpha variants), mixes at 0, .25, .5,
   .75, 1 and two extrapolated values for every space and hue mode, gamut-mapped OKLCH colors. Update the README.

## 4. Tests (`ColorConversionTests`, `ColorMixTests`, `ColorKeyframesTests`)

- Conversions match `colors.json`, and sRGB → OKLab → sRGB and → OKLCH → sRGB round-trip within 1e-9.
- `Mix` matches the fixture in every space and hue mode, including premultiplied alpha (transparent → red is red at
  alpha .5 halfway) and the powerless rule (white → blue keeps blue's hue).
- `Oklch` from 350° to 10°: `Shorter` passes 0°, `Longer` passes 180°, `Increasing` and `Decreasing` go the stated
  way; equal hues with `Increasing` make a full turn.
- Gamut mapping matches culori's `toGamut('rgb', 'oklch')` for out-of-gamut OKLCH colors; no channel is NaN or outside
  0–255; in-gamut colors pass through unchanged.
- Ends are exact: `Mix(a, b, 0) == a` and `Mix(a, b, 1) == b` to the bit; an expanded tween completes on `to` exactly,
  including under `ReduceAll`.
- The expander preserves timing: for `OutCubic`, `InOutBack` and a spring, the rendered color at 20 sample times is
  within deltaEOK 0.02 of `Mix(from, to, E(t))`, using the default segment counts. `Steps(4)` holds between steps.
- Function-valued ends resolve per target under `Stagger`.
- `WithOklch` and `ShiftOklch` match the fixture and stay in gamut.
- `AllocationTests`: `Mix` allocates nothing.

## 5. Samples

- **Hue cycle (the headline demo).** Two looping glows side by side: one through RGB keyframes red → yellow → green →
  cyan → blue → magenta, one an OKLCH full turn at fixed L and C. The RGB one visibly pulses (yellow L .97, blue L .45);
  the OKLCH one glows evenly, and white text on it stays readable all the way round. A small readout shows each one's
  current L and contrast ratio. The builder is printed.
- **Status flip.** A pill switching syncing (blue) → warning (amber) → error (red), with a space picker. `Default` passes
  through gray on the blue → amber leg; `Oklch` stays saturated.
- **Strips.** On the Value types page, a *Color spaces* card with one strip per space plus `Default`, drawn with `Mix`,
  for two picked colors; red → green and blue → yellow as presets.
- **Motion tokens page.** The tokens' `ColorSpace` switch restyles the recipes' color states; hover and pressed shades
  derived with `ShiftOklch`.

## 6. Open questions and deferred work

- **Per-tween color spaces** (the dropped design: from the current color, `Animatable` retargets and blend composition
  in the space). Revisit only if someone needs "animate from wherever it is now" along an OKLCH path.
- **Gradients and box shadows.** Their color slots still lerp in plain sRGB (decision 117's gap). A gradient-aware
  expander would need codec color slots; Avalonia also renders between stops in sRGB, so an OKLCH-looking gradient
  would need synthesized extra stops. Deferred.
- HSL, HWB and CIE Lab/LCH for CSS completeness, once something asks.
- Parsing `oklch()`/`oklab()` strings in `PrestoColor.TryParse` so XAML and string values can name them. Cheap, but it
  extends the upstream parser's accepted forms; decide when the sample needs it.

## 7. Deviations as built

(Filled in as the phase lands.)
