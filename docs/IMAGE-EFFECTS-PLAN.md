# Image effects — color filters and pixel transitions

Phases 28–29 in [PLAN.md](PLAN.md). Status: **planned**. This plan follows the post-Phase-27 gap review, which found that
the colour and image effects every web animation library leans on (CSS `filter`: grayscale, saturate, sepia, hue-rotate,
brightness, contrast, invert; plus dissolve, pixelate and ripple transitions) cannot be expressed in Avalonia at all.
Phase 24 covered what `Visual.Effect` offers; this covers what it does not, without writing shaders.

| Phase | Scope | Size |
|---|---|---|
| 28 | `FilteredImage`: a drop-in `Image` with animatable CSS-style colour filters, drawn through Skia colour filters | medium |
| 29 | Pixel transitions on `FilteredImage`: crossfade, dissolve, pixelate, ripple and wipe between two sources, and the same effects as standalone properties | medium |

## 0. What Avalonia 12.1.2 offers (verified against the source)

- **`Visual.Effect`** has two implementations, `BlurEffect` and `DropShadowEffect` (plus `DropShadowDirectionEffect`).
  There is no colour-matrix, hue or saturation effect, and `IEffect` is not implementable in a way the Skia backend
  consumes: `DrawingContextImpl.Effects.cs` switches on the known types.
- **`RenderOptions.BitmapBlendingMode`** (all the CSS blend modes) applies only inside `DrawingContextImpl.DrawBitmap`
  (`paint.BlendMode = RenderOptions.BitmapBlendingMode.ToSKBlendMode()`), i.e. to an `Image` or an image brush tile
  blending onto what is beneath it in the same layer. It is not a layer blend mode for arbitrary visuals.
- **`ISkiaSharpApiLeaseFeature`** is available to an `ICustomDrawOperation` through
  `ImmediateDrawingContext.TryGetFeature`. The lease hands out the live `SKCanvas`, `SKSurface` and `GRContext` and, on
  dispose, restores the canvas matrix it found. `ImmediateDrawingContext.DrawBitmap(Bitmap, Rect[, Rect])` draws an
  Avalonia bitmap on the same canvas outside a lease.
- **Skia's built-in filters need no shader code:** `SKColorFilter.CreateColorMatrix` (every CSS colour filter is a 5×4
  matrix in the Filter Effects spec), `SKShader.CreatePerlinNoiseFractalNoise`/`Turbulence`,
  `SKImageFilter.CreateDisplacementMapEffect`, `SKImageFilter.CreateShader`, and blend modes on `SaveLayer` paints.
  Skia is the renderer on every head this repo ships, and the bridge test project already renders headless through
  Skia (`UseHeadlessDrawing = false`), so pixel tests are possible.

## 1. Phase 28 — `FilteredImage`

### 1.1 Decisions

1. **A control, not shorthands.** `FilteredImage` derives from `Avalonia.Controls.Image`, so `Source`, `Stretch`,
   `StretchDirection`, measure and arrange are inherited and it is a drop-in replacement in XAML. Each filter is a
   plain `StyledProperty<double>` (or `Color`) with `AffectsRender`, so everything Presto already does with an
   `AvaloniaProperty` works unchanged: `Animate`, the typed indexer, builders, `Motion` states, `Presence`, recipes,
   bindings, style setters, `Animation`-priority writes and `Revert`. No composer, no new accessor kind, no codec.
   Filtering arbitrary visuals is out of scope (§3).
2. **Filters are CSS names and CSS semantics.** `Brightness`, `Contrast`, `Saturate`, `Grayscale`, `Sepia`,
   `HueRotate` (degrees), `Invert`, matching the Filter Effects Level 1 matrices and ranges, so an anime.js
   `filter: 'grayscale(1)'` has a one-to-one port. Two additions CSS lacks, both still affine and so still one matrix:
   `Tint` (amount) with `TintColor`, a monotone that maps luminance onto the colour; and `Duotone` (amount) with
   `DuotoneShadow` and `DuotoneHighlight`, which maps luminance onto the segment between the two colours.
3. **One matrix, fixed order.** The filters compose into a single 5×4 matrix per render, in a documented fixed order:
   `Brightness` → `Contrast` → `Saturate` → `Grayscale` → `Sepia` → `HueRotate` → `Tint` → `Duotone` → `Invert`.
   CSS chains are user-ordered; properties cannot be, and a fixed order is what lets each filter animate on its own. A
   CSS-syntax `Filter` string property is an open question (§4).
4. **Drawing path.** `Render` records one `ICustomDrawOperation` holding the composed matrix and the bitmap. On the
   render thread it leases the canvas, `SaveLayer`s with an `SKColorFilter` paint, releases the lease, draws the bitmap
   with `ImmediateDrawingContext.DrawBitmap` (so Avalonia's own sampling, interpolation mode and opacity apply),
   leases again and `Restore`s. The spike (step 1) confirms this interleaving is legal; the fallback is to copy the
   bitmap's pixels once per `Source` into a cached `SKImage` and draw that inside the lease.
5. **Identity draws plain.** When the composed matrix is the identity (every filter at its default), `Render` defers to
   `Image.Render` and no layer is pushed, as decision 29 in PLAN §7 removes effects at rest.
6. **Non-bitmap sources are rasterized.** A `DrawingImage` (SVG icons) or other `IImage` is rendered into a cached
   `RenderTargetBitmap` at the control's size times render scaling, invalidated on resize, scaling change or the
   drawing's own invalidation. A `CroppedBitmap` passes its source rect to `DrawBitmap`.
7. **Without a Skia lease, draw unfiltered.** `TryGetFeature<ISkiaSharpApiLeaseFeature>()` returning null (a non-Skia
   backend, headless drawing) falls back to `Image.Render`. Nothing throws.
8. **The bridge takes a package reference on `Avalonia.Skia`** (version already in `Directory.Packages.props`) for
   `ISkiaSharpApiLeaseFeature` and SkiaSharp. Every head already carries it.
9. **Compositor animation stays unsupported.** The filter values live in UI-thread render data, so a filter animates on
   the UI thread like any other `AvaloniaProperty`; `AnimateOnCompositor` naming one throws as today.
10. **Native lifetime.** Each draw operation owns its `SKColorFilter` and `SKPaint` and disposes them in
    `ICustomDrawOperation.Dispose`. If per-frame native allocation shows up in measurement, cache the filter on the
    control keyed by the matrix.

### 1.2 API

```csharp
public class FilteredImage : Image
{
    public static readonly StyledProperty<double> BrightnessProperty;   // 1 = identity, 0 = black, >1 brighter
    public static readonly StyledProperty<double> ContrastProperty;     // 1 = identity
    public static readonly StyledProperty<double> SaturateProperty;     // 1 = identity, 0 = gray, >1 oversaturated
    public static readonly StyledProperty<double> GrayscaleProperty;    // 0 = identity, 1 = gray
    public static readonly StyledProperty<double> SepiaProperty;        // 0 = identity
    public static readonly StyledProperty<double> HueRotateProperty;    // degrees
    public static readonly StyledProperty<double> InvertProperty;       // 0 = identity
    public static readonly StyledProperty<double> TintProperty;         // 0 = identity
    public static readonly StyledProperty<Color> TintColorProperty;
    public static readonly StyledProperty<double> DuotoneProperty;      // 0 = identity
    public static readonly StyledProperty<Color> DuotoneShadowProperty;
    public static readonly StyledProperty<Color> DuotoneHighlightProperty;
}
```

```xml
<presto:FilteredImage Source="/Assets/photo.jpg" Grayscale="1"
                      presto:Motion.Hover="{x:Static local:Motions.Colorize}" />
```

```csharp
photo.Animate(b => b.Property(FilteredImage.SepiaProperty, from: 0.8, to: 0).Property(FilteredImage.BrightnessProperty, from: 1.6, to: 1));
```

Typed builder methods (`.Grayscale(1)`, `.HueRotate(180)`, …) are generated in `PropertyBuilderExtensions` with the
usual four overloads, resolving to the `FilteredImage` properties by name. New recipes: `Recipes.Colorize` (a hover
from grayscale to colour), `Recipes.Develop` (a photo-developing entrance: sepia, brightness and blur settling), and
`Recipes.FlashTint` (attention). Each supplies a `WhenReduced` alternative per decision 35.

### 1.3 Implementation steps

1. **Spike (half a day, throwaway).** In the Desktop sample: a custom draw op that leases, `SaveLayer`s with a grayscale
   colour filter, releases, calls `DrawBitmap`, leases and restores. Confirm the image is filtered, clipping and
   opacity of ancestors still apply, render scaling is right, and it works in the headless test app. Record which
   drawing path (decision 4) won in §5 of this document.
2. **`ColorMatrix` (internal, pure).** A 5×4 `float` matrix struct with `Multiply`, `IsIdentity` and one factory per
   filter from the Filter Effects spec, plus `Tint` and `Duotone`. No Avalonia dependency beyond `Color`.
3. **`FilteredImage`.** Properties, `AffectsRender`, `Render` building the composed matrix and the draw op, identity
   short-circuit, non-bitmap rasterization cache, lease fallback.
4. **Builder extensions, recipes, README rows** (anime.js `filter` → `FilteredImage` in the cheat sheet), and a
   Platforms note once the Browser and Android heads have been smoke-run with a filtered image.

### 1.4 Tests (`ColorMatrixTests`, `FilteredImageTests`)

- `ColorMatrix`: each factory at its identity value is the identity; each at full strength matches the spec matrix;
  `Grayscale(a)` equals `Saturate(1 - a)`; `HueRotate(360)` is the identity within 1e-6; composition order is the
  documented one (brightness then invert differs from invert then brightness, and the control uses the former).
- Pixel tests, headless Skia: a red bitmap at `Grayscale = 1` renders the spec luminance gray; `Invert = 1` renders
  cyan; `HueRotate = 180` rotates a known colour to its spec value; `Tint = 1` with blue renders luminance × blue;
  all defaults render identical to a plain `Image`.
- Animation: `Presto.Animate` on `GrayscaleProperty` renders intermediate frames and `Revert` restores the local value.
- A `Motion.Hover` state on `SaturateProperty` returns to the base value (decision 26 applies unchanged).
- Identity draws plain: an internal `IsFiltering` flag is false at rest and after an animation back to identity.
- `DrawingImage` source is rasterized once and re-rasterized after a resize.
- `AllocationTests`: a running `Grayscale` animation holds the `double`-property budget on the managed side; the
  native filter per frame is noted, not budgeted.
- `AnimateOnCompositor` naming `Grayscale` throws `NotSupportedException`.

### 1.5 Samples

- **New Reference page "Image effects":** one photo with a slider per filter and the builder or XAML it corresponds to
  printed beside it; a row of thumbnails that colorize on hover (`Motion.Hover` + `Recipes.Colorize`); a "develop"
  entrance on `Presence.Initial`; a duotone card whose two colours animate with the app theme; a hue cycle loop on
  `Animate.OnAttached`.
- **Storefront:** out-of-stock products render grayscale and colorize on hover.
- **Assets:** three or four CC0 photographs added under `samples/PrestoSample/Assets/Photos/`, with their source and
  licence in a `README.md` beside them.

## 2. Phase 29 — Pixel transitions

### 2.1 Decisions

1. **Transitions live on `FilteredImage`.** They are the "shader and pixel transitions" of PLAN §8, built from Skia's
   built-in noise, displacement and blend modes instead of shaders. Colour filters apply to both images during a
   transition.
2. **Each effect is also a standalone property,** so it can reveal or hide a single image through `Presence` without a
   second source:
   - `Dissolve` (0 = visible, 1 = gone) with `DissolveScale` and `DissolveSoftness`: alpha is thresholded against
     fractal noise, a clamped affine function of the noise and so one colour matrix over the noise shader, applied as a
     `DstIn` mask layer.
   - `Pixelate` (block size in DIPs, 1 = identity): the bitmap drawn into a scaled-down layer and back up with
     nearest-neighbour sampling.
   - `Ripple` (displacement in DIPs, 0 = identity) with `RippleScale`: a displacement map image filter over turbulence.
3. **Source swaps are opt-in through one property.** `SourceTransition` (an `ImageTransition` value: kind plus an
   `AnimationBuilder` for timing) plays whenever `Source` changes: the old bitmap is held as the outgoing image, the
   new one incoming, and a progress value drives the kind's curve. Without it, `Source` changes snap as `Image` does.
   Kinds: `Crossfade`, `Dissolve`, `Pixelate` (blocks grow to a peak mid-way, then shrink on the new image), `Ripple`
   (peaks mid-way under a crossfade), `Wipe` (angle and feather, as `MaskSweep`).
4. **A code API returns the animation.** `image.TransitionTo(source, kind, builder)` returns the `Animation` so it can be
   awaited, sequenced in a timeline or reversed. An interrupted transition starts from what is on screen: the
   current composite becomes the outgoing image.
5. **Noise is seeded** (`Seed` property) so a dissolve is repeatable in tests and does not shimmer between frames.

### 2.2 Tests

- Each standalone property at its identity value renders identical to a plain `Image`, and at its end value renders
  fully transparent (`Dissolve = 1`) or measurably changed.
- `Dissolve` with a fixed seed renders the same pixels twice.
- A `SourceTransition` swap: at progress 0 the old image is drawn, at 1 the new one, and in between both contribute;
  the outgoing bitmap is released when the transition completes.
- Interrupting a transition with a third source starts from the current composite and ends on the third.
- `TransitionTo` returns an animation that `Then()` completes and `Revert` snaps to the new source.

### 2.3 Samples

- **Image effects page:** a slideshow with a transition-kind picker and the builder printed; a card that dissolves in
  on `Presence.Enter` and pixelates out on `Presence.Exit` (the exit ghost is a snapshot, so the pixelate exit uses the
  `Pixelate` property on the element before removal, or is documented as unsupported on ghosts).
- **Gallery flip or Connected showcase:** the detail hero swaps images with a `Dissolve` transition.

## 3. Not in these phases

- **Filters on arbitrary visuals.** A `FilterPresenter` decorator that snapshots its child (the machinery the exit
  ghosts use) and draws the snapshot filtered while any filter is off identity, swapping the live child back at rest.
  Fine for transitions ("grayscale out when disabled"), wrong for content that changes while filtered. Decide after
  Phase 28 whether demand justifies it.
- **Pre-baked crossfade.** Every Phase 28 filter except `HueRotate` is affine in its amount, so for opaque pixels
  `filter(t) = lerp(original, filter(1), t)`: a filtered copy rendered once and faded in with `Opacity` is exact and
  could run on the compositor. Worth adding as a `FilteredImage` mode only if UI-thread filtering proves too costly
  on Browser or Android.
- **Blend-mode overlays.** `RenderOptions.BitmapBlendingMode` on an overlay `Image` can colorize what is beneath it
  (Color, Luminosity, Multiply). It is limited to bitmaps and to one layer, so it is left as a documented technique,
  not an API.
- **SkSL runtime effects** (true shaders) remain a non-goal until these phases show what the built-in filters cannot do.

## 4. Open questions (confirm before the relevant phase)

- A CSS-syntax `Filter` string property (`"grayscale(1) blur(4px)"`) that the core animates as a complex value, for
  anime.js parity. It would need a user-ordered chain, so it would either bypass the fixed-order properties or be
  rejected when both are set.
- Whether `Blur` belongs on `FilteredImage` as an image filter (cheaper, clipped to the image) beside the existing
  `Blur` effect shorthand, which blurs the whole element.
- Whether the Phase 29 transitions should also accept arbitrary content (a `TransitioningContentControl`-style host),
  which depends on the `FilterPresenter` decision in §3 and overlaps Phase 31's page transitions.

## 5. Deviations as built

(Filled in as the phases land.)
