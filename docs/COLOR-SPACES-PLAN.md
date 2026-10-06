# Colour interpolation spaces

Phase 30 in [PLAN.md](PLAN.md), split into 30a (core, solid colours) and 30b (bridge, colours inside gradients and
shadows). Status: **planned**. This plan follows the post-Phase-27 gap review: every colour tween interpolates in one
fixed RGB space, so red → green passes through a dark olive, blue → yellow through gray, and a theme change from one
brand hue to another dips in lightness halfway. CSS (`color-mix`, `in oklch` gradients), Framer Motion and GSAP all offer
a perceptual space; Presto offers none.

| Phase | Scope | Size |
|---|---|---|
| 30a | Core: `ColorSpace` and `HueInterpolation` on tweens, animations, keyframes, animatables and defaults; conversion, hue, alpha and gamut rules for `ValueKind.Color`; blend composition; `Animatable` channel retargets | medium |
| 30b | Bridge: colour slots inside complex values, so gradient stops (including resampled ones) and box-shadow colours follow the space; `ShadowColor` and solid brushes come free from 30a | small–medium |

## 0. What the code does today (verified)

- **Solid colours** (`ValueKind.Color`, four channels R, G, B in 0–255 and A in 0–1) are lerped in the colour branch of
  `Renderer.Render` pseudo-linearly with premultiplied alpha (PLAN decision 117):
  `sqrt(lerp(a₀·from², a₁·to², p) / lerp(a₀, a₁, p))` per RGB channel, alpha linear. With equal alphas that is
  upstream anime.js 4.5.0's `sqrt(from² · (1 − p) + to² · p)` to the bit (`src/core/render.js`, "pseudo-linear space …
  to approximate gamma-correct blending"), which the generated fixtures and ported `colors` tests depend on.
- **Every consumer reads RGB from `tween._numbers`:** `ColorCodec.Encode`, `BrushCodec`'s solid path,
  `EffectAccessor` for `ShadowColor`, `Tween._value` (`PrestoColor.FormatRgba`) for string targets, and
  `Animatable.GetChannels`. `Animatable.Set(property, channels)` writes RGB into `_fromNumbers`/`_toNumbers` directly.
- **Gradients and box shadows are `ValueKind.Complex`,** with colours embedded as four-number slots (`BrushCodec`:
  five numbers per stop after ten header numbers; `BoxShadowsCodec`: channels at offset 5 per shadow). The core lerps
  complex numbers plainly (`Tween.ComposeComplexNumbers`), so these colours are interpolated in gamma sRGB, *not* the
  pseudo-linear space solid colours use, and with straight alpha until 30b. `BrushCodec.ColorAt`, which resamples
  stops onto the union of offsets (decision 21), lerps in gamma sRGB with premultiplied alpha. The core itself never produces colour slots in complex values: embedded hex
  or `rgb()` inside a complex string is not parsed as a colour.
- **Blend composition** (`Composer.ComposeBlendedTween`) turns a blended tween's channels into deltas from the additive
  lookup (`from = base − to`, `to = 0`) and `Additive.Update` sums `_numbers` across siblings. A colour delta still goes
  through the straight-alpha pseudo-linear branch, which computes `|from| · sqrt(1 − p)`: a negative delta loses its sign and the curve
  is not linear. Upstream has the same arithmetic. Pin this with a test before 30a changes the branch (§4).
- **Relative values** (`+=`, `-=`, `*=`) apply to numbers only; a colour string cannot be relative.
- **The compositor carries no colour.** `CompositorTrackKind` is `Translation`, `Scale`, `RotationAngle` and `Opacity`
  only, so nothing in `Compositor/` changes.
- **Per-tween options already flow one way:** `Composition` is on `TweenParams`, `Keyframe`, `AnimatableProperty`,
  `AnimationParams`, `AnimationBuilder.Composition(...)`, `PropertyBuilder(composition:)` and
  `DefaultsParams`/`PrestoDefaults`, resolved per tween in the `Animation` constructor (`key?.Composition ??
  tComposition`). The new options follow the same path.

## 1. Decisions

1. **Spaces.** `ColorSpace.Default` (upstream pseudo-linear, premultiplied), `Srgb` (plain gamma lerp, what gradients do
   today), `LinearSrgb`, `Oklab` and `Oklch`. OKLab is the recommended perceptual choice: straight, even lightness,
   no hue surprises. OKLCH is for sweeps that should travel around the hue wheel. HSL and CIE Lab/LCH are left out;
   OKLab and OKLCH supersede them, and CSS keeps them mainly for compatibility (§5).
2. **Default stays as it is.** `PrestoDefaults.ColorSpace` is `Default` (upstream's pseudo-linear blend with
   premultiplied alpha, PLAN decision 117), so every existing test, fixture and sample is untouched. An app opts in globally through `Presto.Defaults`, per animation, or per property.
3. **Convert once at setup, back once per frame.** When a tween's space is not `Default`, the `Animation` constructor
   converts both ends into the space once and keeps them in two new per-tween buffers (`_spaceFrom`, `_spaceTo`,
   allocated only for non-default spaces). The renderer lerps those and converts the result back into `_numbers` as RGB.
   Everything that reads `_numbers` (codecs, `ShadowColor`, strings, `GetChannels`) is unchanged, and the default path
   gains one branch on an enum.
4. **Ends are exact.** At progress 0 and 1 the renderer copies the RGB ends instead of converting back, so a
   completed tween, `Presto.Set`, `ReduceAll` and `Revert` land on the declared colour to the bit, not on a round trip.
5. **Hue interpolation follows CSS Color 4.** `HueInterpolation.Shorter` (default), `Longer`, `Increasing`,
   `Decreasing`, applied only in `Oklch`. The unwrap happens at setup: the end hue is shifted by ±360 so a plain lerp
   takes the chosen arc, and the per-frame path has no branches for it.
6. **Achromatic hue is powerless.** When one end's OKLCH chroma is below 4e-4 (CSS's powerless threshold, roughly
   where sRGB grays land), its hue takes the other end's; both achromatic take 0. So white → blue stays blue-ish
   instead of passing through a hue that is neither.
7. **Alpha is premultiplied in the new spaces.** As CSS does, the colour channels (L, a, b; or L, C, not h) are
   multiplied by alpha before the lerp and divided after, so transparent → red fades in red instead of through dark.
   `Default` premultiplies too (PLAN decision 117).
8. **Gamut mapping is a clip in linear sRGB.** OKLab lines between two sRGB colours stay nearly in gamut; OKLCH arcs can
   leave it. The conversion back clamps each linear channel to [0, 1] before encoding. CSS Color 4's chroma-reduction
   mapping is more faithful but iterative; it is an open question, not a default (§5).
9. **Blend composition sums deltas in the space.** A blended tween's deltas are computed from `_spaceFrom`/`_spaceTo`,
   lerped linearly (not through the pseudo-linear branch), summed by `Additive.Update` in the space, and the lookup tween
   converts the sum back. OKLCH hue deltas sum as angles. Siblings with different spaces under one lookup take the
   lookup's (the first blended tween's) space; the docs say to keep one space per blended property.
10. **Animatable retargets stay in the space.** The channel form of `Set` takes RGB (as `GetChannels` returns), converts
    the new end into the space, starts from the current `_spaceNumbers` (not a round trip through `_numbers`), and
    re-runs the hue unwrap. The scalar `PreserveVelocity` path is untouched.
11. **No relative colours, no function-based space.** Relative colour values do not exist today and stay out. The
    space is a plain enum, like `Composition` (PLAN §7 open question on function-based `composition`).
12. **Reduced motion is unaffected.** `ReduceAll` collapses durations; decision 4 makes the landing exact.
13. **30b marks colour slots through the codec.** `IReconcilingValueCodec` gains an optional
    `ReadOnlySpan<int> ColorSlots(DecomposedValue)` (offsets of four-channel colours inside the numbers). The tween
    records them at setup; `ComposeComplexNumbers` lerps other numbers as today and routes each slot through the colour
    path of decision 3. `BrushCodec.ColorAt` resamples stops in the tween's space so a resampled stop lies on the curve
    the tween will follow. `Default` for complex colours keeps today's gamma-sRGB lerp, not pseudo-linear, so gradients
    do not change behaviour silently.

## 2. API

```csharp
public enum ColorSpace { Default, Srgb, LinearSrgb, Oklab, Oklch }
public enum HueInterpolation { Shorter, Longer, Increasing, Decreasing }

// TweenParams, Keyframe, AnimatableProperty, AnimatableParams, AnimationParams, DefaultsParams:
public ColorSpace? ColorSpace { get; init; }
public HueInterpolation? HueInterpolation { get; init; }
// PrestoDefaults:
public ColorSpace ColorSpace { get; set; } = ColorSpace.Default;
public HueInterpolation HueInterpolation { get; set; } = HueInterpolation.Shorter;
```

```csharp
card.Animate(b => b.Property("Background", "#e11d48").ColorSpace(ColorSpace.Oklab));       // whole animation
card.Animate(b => b.Property("Foreground", to: "#22c55e", colorSpace: ColorSpace.Oklch,     // one property
                             hue: HueInterpolation.Longer));
Presto.Defaults.ColorSpace = ColorSpace.Oklab;                                              // app-wide
```

`AnimationBuilder.ColorSpace(ColorSpace, HueInterpolation = Shorter)`, and parameters on
`PropertyBuilder`'s full overload (`colorSpace:`, `hue:`) and `KeyframeBuilder`. `MotionTokens` gains a `ColorSpace` so recipes and `Motion`
states restyle with the tokens. XAML needs nothing new: builders reach XAML as values already, and the enums convert
from strings.

## 3. Implementation steps

**30a (core)**

1. **`ColorConversions` (internal, static, allocation-free).** sRGB ↔ linear sRGB (the piecewise transfer function),
   linear sRGB ↔ OKLab (Björn Ottosson's matrices and cube roots), OKLab ↔ OKLCH, premultiply and unpremultiply,
   linear clip. Spans in, spans out.
2. **Parameters.** The enums, the properties in §2, resolution per tween beside `Composition` in the `Animation`
   constructor and `KeyframeExpander`, builder and `PropertyBuilder` overloads, `DefaultsParams` → `PrestoDefaults`.
3. **Tween state.** `_colorSpace`, `_hue`, and `_spaceFrom`/`_spaceTo`/`_spaceNumbers` allocated at setup for
   non-default spaces only; the hue unwrap and the achromatic rule at setup.
4. **Renderer.** In the colour branch, `Default` keeps its code; any other space lerps the space buffers and converts
   into `_numbers`, with the exact-ends rule.
5. **Composition.** Blend deltas in the space (decision 9); `Additive.Update` sums the space buffers and the lookup
   converts. Replace composition needs nothing: an interrupting tween reads the target's current RGB as its from.
6. **Animatable** channel `Set` (decision 10).
7. **Fixtures.** Add `culori` to `tools/anime-fixtures/` and dump conversions and interpolation midpoints (all spaces,
   all hue modes, premultiplied alpha) to a `colors.json` beside `eases.json`; the C# tests assert within 1e-6.
   `culori` implements CSS Color 4 interpolation, so it is the reference for the rules above.

**30b (bridge)**

8. `ColorSlots` on `IReconcilingValueCodec` (default: none), recorded on the tween; `ComposeComplexNumbers` routes
   slots through the colour path; `BrushCodec` and `BoxShadowsCodec` report their slots; `ColorAt` resamples in the
   tween's space.
9. README: a Colour spaces section with the red → green strip, and the cheat-sheet note that anime.js has no equivalent
   (the CSS `color-interpolation` row).

## 4. Tests

**30a (`ColorSpaceTests`, `ColorsTests`, `CompositionTests`, `AnimatablesTests`, `AllocationTests`)**

- Before any change: a characterisation test pinning today's blended colour delta behaviour (§0), so the decision to
  keep or fix it in `Default` is made against a test, not by accident.
- Conversions round-trip sRGB → OKLab → sRGB and → OKLCH → sRGB within 1e-9 for a colour set; values match `colors.json`.
- Red → green at 50%: `Default` matches the ported upstream value; `Oklab` matches the fixture and has an OKLab
  lightness between the ends.
- Ends are exact in every space (decision 4), including after `ReduceAll`.
- `Oklch` hue from 350° to 10°: `Shorter` passes 0°, `Longer` passes 180°, `Increasing` and `Decreasing` go the stated
  way; white → blue keeps blue's hue throughout.
- Transparent → red in `Oklab` renders red at alpha 0.5 halfway; so does `Default`.
- An out-of-gamut OKLCH midpoint clips into [0, 255] with no NaN.
- Precedence: property over animation over defaults; keyframes inherit the animation's space.
- Blend: two blended `Oklab` colour tweens sum their deltas in the space and the lookup renders the fixture value.
- Replace mid-flight: an interrupting `Oklab` tween starts from the rendered RGB.
- Animatable channel `Set` in `Oklch` continues from the current colour and ends exactly.
- `AllocationTests`: an `Oklab` colour tween holds the existing per-frame budget (zero on the core path).
- Every existing parity suite unchanged.

**30b (`BrushCodecTests`, `BoxShadowsCodecTests`)**

- A two-stop gradient tween in `Oklab` samples each stop's colour at the fixture value; `Default` is unchanged.
- Resampled stops (different offsets at the two ends) lie on the `Oklab` curve.
- Solid → gradient and gradient → solid in `Oklch` end exactly.
- A box-shadow colour follows the space; its offsets and blur lerp as numbers.
- Bridge `AllocationTests` hold for a gradient tween in `Oklab`.

## 5. Samples

- **Value types page:** a *Colour spaces* card: five strips (one per space) drawn by sampling a paused tween, and a
  swatch that animates between two picked colours in the selected space, with the builder printed. Red → green and
  blue → yellow as presets.
- **Motion tokens page:** the tokens' `ColorSpace` switch restyles the recipes' colour states.
- **Storefront:** the theme accent change uses `Oklab`.

## 6. Open questions (confirm before the relevant phase)

- Whether to fix the blended colour delta sign (§0) in `Default` as a documented deviation, or leave upstream's
  behaviour and only get it right in the new spaces.
- HSL and CIE Lab/LCH for CSS completeness. Cheap once the conversion table exists; left out until something asks.
- CSS Color 4 chroma-reduction gamut mapping instead of the clip, as an opt-in, if OKLCH sweeps visibly flatten.
- Whether `Default` should eventually mean `Oklab` in the bridge (a UI library default) while the core keeps parity.
  That would change every sample's colour motion and needs its own decision.

## 7. Deviations as built

(Filled in as the phases land.)
