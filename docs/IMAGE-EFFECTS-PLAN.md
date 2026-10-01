# Image effects — color filters and pixel transitions

Phases 28–29 in [PLAN.md](PLAN.md). Status: **built** (deviations in §5). This plan follows the post-Phase-27 gap review, which found that
the colour and image effects every web animation library leans on (CSS `filter`: grayscale, saturate, sepia, hue-rotate,
brightness, contrast, invert; plus dissolve, pixelate and ripple transitions) cannot be expressed in Avalonia at all.
Phase 24 covered what `Visual.Effect` offers; this covers what it does not, without writing shaders.

| Phase | Scope | Size |
|---|---|---|
| 28 | `FilteredImage`: an `Image`-compatible control with animatable CSS-style colour filters, drawn through Skia colour filters | medium |
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

1. **A control, not shorthands, on a shared base.** Avalonia 12.1.2 seals `Image.Render` (found while building Phase
   37), so the filters live on a new public abstract `PrestoImageBase : Control` whose `Render` is not sealed. It takes
   `Source`, `Stretch` and `StretchDirection` from `Image`'s properties through `AddOwner` (so its XAML surface matches
   `Image` and swapping one for the other is a rename), measures and arranges one frame, and draws it plain or filtered.
   Subclasses supply what to draw through two protected virtual hooks, the current `IImage` and its source rect, and
   may override `Render`. `FilteredImage` is the thin concrete subclass (the whole `Source`); Phase 37's `SpriteSheet`
   (a grid cell) and `ImageSequence` (the current frame's bitmap) move onto the base, so frames get the filters too, and
   the internal `FramePlayer.Draw`/`Measure` helpers fold into it (`FramePlayer` keeps `Resolve`/`Play`). Each filter is a
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
5. **Identity draws plain.** When the composed matrix is the identity (every filter at its default), the base draws
   the hook's image and source rect directly, exactly as `SpriteSheet` draws a frame today, and no layer is pushed, as decision 29 in PLAN §7 removes effects at rest.
6. **Non-bitmap sources are rasterized.** A `DrawingImage` (SVG icons) or other `IImage` is rendered into a cached
   `RenderTargetBitmap` at the control's size times render scaling, invalidated on resize, scaling change or the
   drawing's own invalidation. A `CroppedBitmap` passes its source rect to `DrawBitmap`.
7. **Without a Skia lease, draw unfiltered.** `TryGetFeature<ISkiaSharpApiLeaseFeature>()` returning null (a non-Skia
   backend, headless drawing) falls back to the base's plain draw. Nothing throws.
8. **The bridge takes a package reference on `Avalonia.Skia`** (version already in `Directory.Packages.props`) for
   `ISkiaSharpApiLeaseFeature` and SkiaSharp. Every head already carries it.
9. **Compositor animation stays unsupported.** The filter values live in UI-thread render data, so a filter animates on
   the UI thread like any other `AvaloniaProperty`; `AnimateOnCompositor` naming one throws as today.
10. **Native lifetime.** Each draw operation owns its `SKColorFilter` and `SKPaint` and disposes them in
    `ICustomDrawOperation.Dispose`. If per-frame native allocation shows up in measurement, cache the filter on the
    control keyed by the matrix.

### 1.2 API

```csharp
public abstract class PrestoImageBase : Control
{
    public static readonly StyledProperty<IImage?> SourceProperty;              // Image.SourceProperty.AddOwner
    public static readonly StyledProperty<Stretch> StretchProperty;             // Image.StretchProperty.AddOwner
    public static readonly StyledProperty<StretchDirection> StretchDirectionProperty; // Image.StretchDirectionProperty.AddOwner

    protected virtual IImage? CurrentImage => Source;                          // ImageSequence: the current frame
    protected virtual Rect SourceRect(IImage image) => new(image.Size);         // SpriteSheet: the current cell
    public override void Render(DrawingContext context);                        // plain or filtered; overridable

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

public sealed class FilteredImage : PrestoImageBase { }
public class SpriteSheet : PrestoImageBase { /* Phase 37 surface unchanged */ }
public class ImageSequence : PrestoImageBase { /* Phase 37 surface unchanged */ }
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
3. **`PrestoImageBase` and `FilteredImage`.** The base with the `AddOwner` image properties, the two hooks, measure
   and arrange, filter properties, `AffectsRender`, `Render` building the composed matrix and the draw op, identity
   short-circuit, non-bitmap rasterization cache, lease fallback; `FilteredImage` over it.
4. **Move `SpriteSheet` and `ImageSequence` onto the base.** Their public surface, tests and allocation budgets stay as
   Phase 37 shipped them (the half-pixel source-rect inset for scaled cells moves into the base's draw); fold
   `FramePlayer.Draw`/`Measure` into the base. Add a pixel test that a filtered `SpriteSheet` frame is filtered.
5. **Builder extensions, recipes, README rows** (anime.js `filter` → `FilteredImage` in the cheat sheet), and a
   Platforms note once the Browser and Android heads have been smoke-run with a filtered image.

### 1.4 Tests (`ColorMatrixTests`, `FilteredImageTests`)

- `ColorMatrix`: each factory at its identity value is the identity; each at full strength matches the spec matrix;
  `Grayscale(a)` equals `Saturate(1 - a)`; `HueRotate(360)` is the identity within 1e-6; composition order is the
  documented one (brightness then invert differs from invert then brightness, and the control uses the former).
- Pixel tests, headless Skia: a red bitmap at `Grayscale = 1` renders the spec luminance gray; `Invert = 1` renders
  cyan; `HueRotate = 180` rotates a known colour to its spec value; `Tint = 1` with blue renders luminance × blue;
  all defaults render identical to a plain `Image` with the same `Source`, `Stretch` and size.
- Layout: measure and arrange match `Image` for each `Stretch`/`StretchDirection` over the same source.
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

- **`ColorMatrix` luminance weights.** `Saturate` uses the `grayscale()` weights 0.2126/0.7152/0.0722 rather than
  `feColorMatrix`'s rounded 0.213/0.715/0.072, so `Grayscale(a)` equals `Saturate(1 - a)` exactly; `HueRotate` keeps
  the published matrix. Skia's colour-matrix offsets are normalized 0..1 (checked by drawing), despite SkiaSharp's
  0..255 doc remark.
- **`ImageSequence` ignores the inherited `Source`.** It draws `Frames[CurrentFrame]` and sizes by `Frames[0]` (through an
  internal `MeasuredImage` hook on the base); setting `Source` on it has no effect. `TintColor` defaults to white (a
  plain monotone gray), `DuotoneShadow`/`DuotoneHighlight` to black/white.

- **Drawing path: the interleaved lease won (decision 4).** `ImageEffectDrawOperation` (first `ColorFilterDrawOperation`)
  leases the canvas to `SaveLayer` (bounded to the destination) with a paint carrying the effect, releases it, draws
  through `ImmediateDrawingContext.DrawBitmap`, then leases again to `Restore`. Headless Skia pixel tests confirm the pixels are
  filtered and that ancestor opacity, clip and transforms still apply; device render scaling is 1 in the headless
  app, so only a scale transform was checked. The cached-`SKImage` fallback was not needed. The native filter and paint
  are created per render and disposed with the operation; no per-control cache (decision 10) has been needed yet.
- **One image-filter chain, in a fixed order (Phase 29).** The layer paint carries a single `SKImageFilter`: the colour
  matrix (`CreateColorFilter`), then `Ripple`, then `Pixelate`, then the `Dissolve` mask as a `DstIn`
  `CreateBlendMode` over a colour-filtered noise `CreateShader`, so the mask is neither rippled nor pixelated. The mask
  moved from a `DrawRect` inside the layer into the chain for that reason; stages that are at identity are skipped.
- **Pixelate is two nearest-neighbour matrix filters.** `CreateMatrix` scales by `1/Pixelate` and back by `Pixelate`
  about the destination's top-left, both with `SKFilterMode.Nearest`, so each block is one sampled pixel rather than an
  average; Skia keeps the small intermediate (the spike test found no transform folding in SkiaSharp 3.119) and blocks
  align to the destination's top-left. The input is first edge-extended by one block and the result cropped to the
  destination, so partial blocks are opaque and sample inside the image. Blocks are in DIPs because image filters run
  in the canvas's local space.
- **Edge extension is tiled one-DIP strips.** SkiaSharp 3.119 has no crop filter with a clamp tile mode, so `Clamp`
  merges the input with `CreateTile`s of its outermost column into side pads, then of the widened outermost row into
  top and bottom pads, which clamps the corners too.
- **Ripple uses fractal noise over an edge-extended input.** The displacement input is `CreatePerlinNoiseFractalNoise`
  (centred on 0.5), not turbulence, which sums absolute noise, is not centred on 0.5 and so would shift the whole image.
  Its scale is `Ripple` itself, not multiplied by render scaling: Skia maps both the scale and the noise through the
  canvas matrix. The colour input is edge-extended by `Ripple` DIPs, so edge pixels displaced from outside read the
  image's edge rather than transparency, and the output is cropped to the destination, so the ripple never
  draws past the control's image rect.
- **Rasterized sources are cached per draw and reference-counted.** A non-bitmap source is rasterized at the
  destination size (not the whole control) times render scaling, keyed on the image, source rect, size and scaling, so
  resizes, stretch changes and sprite frames re-rasterize. The control keeps every raster drawn in a render and drops,
  once the render is over, those it did not draw (Phase 40), so a transition's outgoing and incoming drawings, or the
  cells a subclass draws through the protected `DrawImage(context, image, sourceRect)`, each keep their own instead of
  evicting one another every frame. Because `Bitmap`'s ref-counted impl is internal, each draw operation retains the
  cached `RenderTargetBitmap` and the last holder disposes it, so the render thread never draws a disposed raster.
  Only `DrawingImage` invalidation is observed (`IAffectsRender` is internal in 12.1.2); other custom `IImage`s
  re-rasterize only when the key changes.
- **Compositor animation needed no change (decision 9):** `AnimateOnCompositor` naming `GrayscaleProperty` already
  throws `NotSupportedException` through the unknown-property path.
- **Builder methods write the typed handle, not the name.** `.Grayscale(1)` and the rest write the
  `PrestoImageBase.GrayscaleProperty` key, the same as the `AvaloniaAnimationParams` indexer, so they resolve on every
  subclass. `TintColor`, `DuotoneShadow` and `DuotoneHighlight` also take a `Color`, as `ShadowColor` does.
- **Recipes.** `Develop` is an ordinary entrance (it fades in too) lasting `Long`; `FlashTint` sets `TintColor` with
  a zero-duration tween and keeps the Attention family's reduced opacity blink; `Colorize` keeps its colour change when
  reduced, as the other interactions keep colour. No `{presto:Colorize}`/`{presto:Develop}`/`{presto:FlashTint}`
  extensions were added: none of the three families has an extension for every recipe (`Grow`, `Dim`, `FlipIn`,
  `Pulse`… have none).
- **Samples.** The duotone card follows `ActualThemeVariantChanged`, but the gallery app is fixed to Dark, so the page
  flips a `ThemeVariantScope` around the card rather than the app theme. XAML reaches `Colorize`, `Develop` and the hue
  cycle through the page's `x:Static` builders (no markup extensions, above). The Storefront's product art was drawn
  controls (`Scenery`), so `Scenery.Picture` now builds it as a `DrawingImage`: every card shows a `FilteredImage`
  of it, and the two sold-out products, gray through a style setter, exercise the rasterization path; their Add is
  disabled. The Frames page binds a HueRotate slider to the adventurer `SpriteSheet`. Verified on Desktop only; hover
  was checked by the headless tests, since DevTools cannot send pointer movement. The Browser and Android heads were not
  smoke-run, so the README has no Platforms note yet.
- **Transitions draw each image through its own layer (Phase 29).** `FilteredImage` overrides `Render` while a
  transition runs and draws the outgoing and incoming images each through the same `ImageEffectDrawOperation` path,
  with its own `Stretch` placement and the colour filters: `Dissolve` and `Wipe` draw the incoming image over the
  outgoing one with its `Dissolve` or wipe mask at `1 - p` or `p`; `Pixelate` draws only the outgoing image before
  mid-way and only the incoming after, blocks peaking at `PeakPixelate`; `Crossfade` and `Ripple` draw the outgoing
  image opaque and the incoming one over it at opacity `p` (Phase 40), so an opaque pair never darkens mid-way (red →
  blue passes (128,0,128), not (64,0,128)), `Ripple` peaking at `PeakRipple` on both. The outgoing image is never
  faded or masked separately: `FilteredImage` arranges to the incoming image's placement under every `Stretch`, so
  the outgoing one, placed within the same bounds, never shows outside it, and Pixelate's midway switch cannot strand
  any of it (pinned by `TheOldImageNeverShowsBesideANewOneOfAnotherAspect`). The cost is that a source of another
  aspect resizes the control when the transition starts, re-placing the old image inside the new bounds. The wipe is
  an internal field of `ImageEffect` (an angled gradient mask in the chain), not a public standalone property, since
  `MaskSweep` already wipes any element.
- **Progress is an internal styled property.** `FilteredImage.TransitionProgressProperty` (0 to 1, default 1,
  `AffectsRender`) lives on `FilteredImage`, not the base, and is internal: `TransitionTo` animates it through an
  ordinary builder, so `Revert` returns it to 1 (the new source alone) and releases the outgoing image. It is not
  public because a user-set progress has no outgoing image to show. The noise tuning (`Seed`, `DissolveScale`,
  `DissolveSoftness`, `RippleScale`) comes from the image's own properties rather than `ImageTransition`.
- **Interruption snapshots the control.** A source change mid-transition renders the current composite into a
  `RenderTargetBitmap` the size of the control, which becomes the outgoing image drawn with `Stretch.Fill` and no
  colour filters (it already shows them); it is reference-counted like the rasterized sources and released on
  completion, revert or detach (detaching completes the animation).
- **`SourceTransition` has a string form.** `ImageTransition.Parse` takes a kind and an optional duration in ms
  (`"Dissolve"`, `"Wipe 800"`, eased `InOutQuad`); the Avalonia XAML compiler finds the static `Parse` for attributes
  and style setters, so no type converter or markup extension was added. The element form sets the tuning. A null
  `Builder` plays 600 ms. With `SourceTransition` set, a change while detached snaps, and `TransitionTo`'s own swap is
  not transitioned twice.
- **Exit ghosts do not carry the pixel effects.** The `Presence` exit ghost is a plain `Image` snapshot, so an exit
  cannot dissolve or pixelate; this is documented as unsupported (README "Pixel transitions") rather than worked
  around. The Image effects card plays `PixelateOut` on the live `FilteredImage`, awaits it, then removes the card and
  reverts the animation; its entrance uses `Presence.Enter` on the element itself.
- **Showcase: the Photo viewer's backdrop.** Neither the Connected nor the Gallery flip detail holds an image (the
  Connected hero is a lifted gradient `Border`, the Gallery flip host reparents a card), so the hero swap went to the
  Photo viewer, which already loads the three photos. Its photos slide in a `Carousel` with a `Transformable` each, so
  rather than a second copy of the photo, a dimmed `FilteredImage` backdrop (`Brightness` 0.35, `UniformToFill`,
  `SourceTransition="Dissolve 700"`) sits behind the carousel and dissolves to each photo as the filmstrip or a swipe
  selects it; the carousel pages lost their opaque background so it shows around the photo.
- **Phase 40 (as built).** Each fix was pinned first by a headless Skia pixel test on a black window. Subclasses that override `Render` get
  `protected DrawImage(context, image, sourceRect)`, which draws with the control's own `Stretch`,
  `StretchDirection`, colour filters and effects; the full overload stays `private protected` because `ImageEffect`
  and the raster type are internal. Verified on Desktop only: each `ImageTransitionKind` in the Image effects
  slideshow was screenshotted mid-way at a 16 s duration (set through DevTools) with no darkening, translucent or
  ragged edges, or end snap, and Pixelate's right and bottom partial blocks sampled fully opaque; the
  dissolve/pixelate card's blocks were checked by setting `Pixelate` to the exit's peak of 32, since the 700 ms exit
  is too quick to capture; the Storefront's sold-out cards and the Photo viewer's dimmed backdrop render as before.
  The slideshow is `UniformToFill` in a fixed frame, so the mixed-aspect case was checked only by the headless test,
  and Storefront stutter could not be measured through DevTools (the per-draw raster reuse is pinned headlessly).
