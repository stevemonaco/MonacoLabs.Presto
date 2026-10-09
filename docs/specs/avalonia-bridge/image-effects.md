---
id: BRIDGE-IMAGE
title: Image effects and pixel transitions
package: Monaco.Presto.AvaloniaBridge
namespaces:
  - Monaco.Presto.AvaloniaBridge.Controls
sources:
  - Monaco.Presto.AvaloniaBridge/Controls/PrestoImageBase.cs
  - Monaco.Presto.AvaloniaBridge/Controls/FilteredImage.cs
  - Monaco.Presto.AvaloniaBridge/Controls/ImageEffect.cs
  - Monaco.Presto.AvaloniaBridge/Controls/ImageEffectDrawOperation.cs
  - Monaco.Presto.AvaloniaBridge/Controls/ImageTransition.cs
  - Monaco.Presto.AvaloniaBridge/Controls/ImageTransitionKind.cs
  - Monaco.Presto.AvaloniaBridge/ColorMatrix.cs
types:
  - PrestoImageBase
  - FilteredImage
  - ImageTransition
  - ImageTransitionKind
tests:
  - FilteredImageTests
  - ImageTransitionTests
  - ColorMatrixTests
  - AnimationBuilderBridgeTests
  - AllocationTests
  - ReferencePagesTests
  - PhotoViewerPageTests
upstream: []
depends:
  - BRIDGE-TARGETS
  - BRIDGE-HELPERS
  - BRIDGE-THREAD
  - BRIDGE-MOTION
  - BRIDGE-RECIPES
  - CORE-ANIMATION
  - CORE-BUILDER
  - CORE-EASES
---

# Image effects and pixel transitions

## Purpose

`PrestoImageBase` is an `Image`-compatible control (Avalonia seals `Image.Render`) that sizes, stretches and draws one image region through CSS-style color filters and three noise and block effects (`Ripple`, `Pixelate`, `Dissolve`), all plain styled properties that every Presto animation path can drive. `FilteredImage` is its concrete form and adds source swaps animated as a crossfade, dissolve, pixelate, ripple or wipe. `SpriteSheet` and `ImageSequence` (BRIDGE-FRAMES) derive from the base, so their frames are filterable. The image recipes `Colorize`, `Develop` and `FlashTint` belong to BRIDGE-RECIPES. Also specifies the image filter and effect methods of `PropertyBuilderExtensions` (`Brightness` through `DissolveSoftness`).

## Requirements

### Layout and drawing

- **BRIDGE-IMAGE-001** — The image controls shall measure and arrange exactly as `Image` does for the same source, `Stretch` and `StretchDirection`, sized by the source rect of the measured image.
  - Tests: `FilteredImageTests.MeasureAndArrangeMatchImage`
- **BRIDGE-IMAGE-002** — When there is no image to draw, the control shall measure to zero and draw nothing.
  - Tests: `ImageSequenceTests.NoFramesRendersNothing`
- **BRIDGE-IMAGE-003** — The base `Render` shall draw `SourceRect(CurrentImage)` of `CurrentImage`, placed by `Stretch` and centered within the bounds, through the filters and effects.
  - Tests: `FilteredImageTests.DefaultFiltersDrawExactlyAsImage`, `SpriteSheetTests.EachFrameDrawsItsCell`
- **BRIDGE-IMAGE-004** (inherited) — When the control's width or height is zero, drawing shall draw nothing.
  - Tests: untested
- **BRIDGE-IMAGE-005** — When every filter and effect is at its identity value, the control shall draw the image plainly, pixel-identical to an `Image`, with no layer, whatever the noise tuning (`DissolveScale`, `DissolveSoftness`, `RippleScale`, `Seed`).
  - Tests: `FilteredImageTests.DefaultFiltersDrawExactlyAsImage`, `FilteredImageTests.IdentityEffectsDrawExactlyAsImageWhateverTheirNoise`
- **BRIDGE-IMAGE-006** (inherited) — The effect path shall be at identity when the composed matrix is the identity within 1e-6 per element, `Ripple` is 0, `Pixelate` is at most 1 and `Dissolve` is at most 0.
  - Tests: `FilteredImageTests.IdentityEffectsDrawExactlyAsImageWhateverTheirNoise`, `ColorMatrixTests.EachFactoryAtItsIdentityValueIsTheIdentity`
- **BRIDGE-IMAGE-007** — When an animation returns every filter to identity, the control shall draw plainly again.
  - Tests: `FilteredImageTests.AnAnimatedFilterRendersBetweenFramesAndRevertsToTheLocalValue`, `FilteredImageTests.AnAnimatedDissolveBackToZeroDrawsPlainAgain`, `FilteredImageTests.AHoverOnSaturateReturnsToTheBaseValue`
- **BRIDGE-IMAGE-008** — A filtered draw shall still be subject to ancestors' opacity, clip and render transforms.
  - Tests: `FilteredImageTests.AncestorOpacityClipAndScaleStillApplyToTheFilteredLayer`
- **BRIDGE-IMAGE-009** — When the drawn region is part of the image and is scaled, the control shall inset the sampled rect by half a source pixel so neighboring cells never bleed into its edges, filtered or not.
  - Tests: `SpriteSheetTests.DefaultInterpolationKeepsNeighboringCellsOutOfTheFrameEdges`, `SpriteSheetTests.AFilteredFrameIsFilteredWithoutNeighboringCells`
- **BRIDGE-IMAGE-010** — A filtered `CroppedBitmap` over a `Bitmap` shall be filtered from its own region of the underlying bitmap only.
  - Tests: `FilteredImageTests.ACroppedBitmapIsFilteredFromItsOwnRegionOnly`
- **BRIDGE-IMAGE-011** — When a non-bitmap `IImage` (such as a `DrawingImage`) is filtered, the control shall rasterize it at the destination size times the render scaling, reuse the raster while the image, source rect, size and scaling are unchanged, and rasterize again when any of them changes.
  - Tests: `FilteredImageTests.ADrawingIsRasterizedOnceWhileFilteringAndAgainAfterAResize`
- **BRIDGE-IMAGE-012** — When a rasterized `DrawingImage` invalidates, the control shall drop its rasters and redraw from the new drawing.
  - Tests: `FilteredImageTests.ADrawingIsRasterizedAgainWhenTheDrawingChanges`
- **BRIDGE-IMAGE-013** — Every non-bitmap source drawn in one render shall keep its own raster, and a raster not drawn in a render shall be dropped once that render is over.
  - Tests: `ImageTransitionTests.TwoDrawingsAreEachRasterizedOnceThroughATransition`, `FilteredImageTests.ASubclassDrawsCellsThroughTheFilters`
- **BRIDGE-IMAGE-014** (inherited) — When the control detaches from the visual tree, it shall release every raster it holds.
  - Tests: untested
- **BRIDGE-IMAGE-015** — A subclass overriding `Render` shall be able to draw any image region through the protected `DrawImage(context, image, sourceRect)`, which applies the control's own `Stretch`, `StretchDirection`, filters and effects.
  - Tests: `FilteredImageTests.ASubclassDrawsCellsThroughTheFilters`
- **BRIDGE-IMAGE-016** — When the drawing context offers no Skia API lease, a filtered draw shall draw the bitmap unfiltered and not throw.
  - Tests: untested

### Color filters

- **BRIDGE-IMAGE-017** — The color filters shall compose into one 5×4 matrix in the fixed order Brightness → Contrast → Saturate → Grayscale → Sepia → HueRotate → Tint → Duotone → Invert, whatever order they were set or animated in.
  - Tests: `ColorMatrixTests.ThenAppliesTheReceiverFirstAndComposeRunsBrightnessBeforeInvert`
- **BRIDGE-IMAGE-018** — `Brightness`, `Contrast`, `Saturate`, `Grayscale`, `Sepia`, `HueRotate` and `Invert` shall use the Filter Effects Level 1 matrices, except that `Saturate` uses the luminance weights 0.2126/0.7152/0.0722 so `Grayscale(a)` equals `Saturate(1 − a)`.
  - Tests: `ColorMatrixTests.FullStrengthFactoriesMatchTheSpecMatrices`, `FilteredImageTests.GrayscaleRendersTheSpecLuminance`, `FilteredImageTests.InvertTurnsRedCyan`, `FilteredImageTests.HueRotateMapsRedToItsSpecValue`
- **BRIDGE-IMAGE-019** — A `HueRotate` of 360 degrees shall be the identity.
  - Tests: `ColorMatrixTests.AFullHueTurnIsTheIdentity`
- **BRIDGE-IMAGE-020** — `Tint` shall map luminance onto `TintColor` (black stays black, white becomes the color), blended with the identity by its amount.
  - Tests: `ColorMatrixTests.FullTintMapsLuminanceOntoTheColor`, `FilteredImageTests.TintMapsLuminanceOntoTheColor`
- **BRIDGE-IMAGE-021** — `Duotone` shall map luminance onto the segment from `DuotoneShadow` (black) to `DuotoneHighlight` (white), blended with the identity by its amount.
  - Tests: `ColorMatrixTests.FullDuotoneMapsBlackToTheShadowAndWhiteToTheHighlight`
- **BRIDGE-IMAGE-022** — The amounts of `Grayscale`, `Sepia`, `Tint`, `Duotone` and `Invert` shall be clamped to 0..1; `Brightness`, `Contrast` and `Saturate` shall not be clamped.
  - Tests: untested
- **BRIDGE-IMAGE-023** — Color matrix offsets shall be applied in normalized 0..1 units.
  - Tests: `FilteredImageTests.InvertTurnsRedCyan`

### Effects

- **BRIDGE-IMAGE-024** — The effects shall apply after the color matrix, in the order Ripple → Pixelate → Dissolve, each stage skipped at its identity value.
  - Tests: `FilteredImageTests.ColorFiltersApplyToThePixelsADissolveKeeps`, `FilteredImageTests.TheDissolveMaskIsNotPixelated`
- **BRIDGE-IMAGE-025** — `Dissolve` shall hide pixels where seeded fractal noise of feature size `DissolveScale` falls below a threshold that `Dissolve` moves from all kept (0) to all gone (1), with an edge `DissolveSoftness` wide.
  - Tests: `FilteredImageTests.AHalfDissolveKeepsSomePixelsAndClearsOthers`
- **BRIDGE-IMAGE-026** — When `Dissolve` is 1 or more, the image shall be fully transparent.
  - Tests: `FilteredImageTests.AFullDissolveIsTransparent`
- **BRIDGE-IMAGE-027** — The same `Seed` shall draw the same noise pattern, and a different seed a different one, for both `Dissolve` and `Ripple`.
  - Tests: `FilteredImageTests.TheSameSeedDissolvesTheSamePixels`, `FilteredImageTests.RippleMovesPixelsTheSameWayForTheSameSeedWithinTheDestination`
- **BRIDGE-IMAGE-028** — The dissolve mask shall be neither pixelated nor rippled.
  - Tests: `FilteredImageTests.TheDissolveMaskIsNotPixelated`
- **BRIDGE-IMAGE-029** — `Pixelate` shall draw uniform blocks `Pixelate` DIPs wide, aligned to the destination's top-left, each one nearest-sampled pixel of the image.
  - Tests: `FilteredImageTests.PixelateDrawsUniformBlocksFromTheTopLeft`
- **BRIDGE-IMAGE-030** — When the block size does not divide the destination, partial right and bottom blocks shall be opaque and sampled from inside the image.
  - Tests: `FilteredImageTests.PixelateDrawsPartialEdgeBlocksOpaque`, `FilteredImageTests.PixelateSamplesPartialEdgeBlocksFromInsideTheImage`
- **BRIDGE-IMAGE-031** — `Ripple` shall displace pixels by up to `Ripple` DIPs through seeded fractal noise of feature size `RippleScale`, at any render scaling.
  - Tests: `FilteredImageTests.RippleMovesPixelsTheSameWayForTheSameSeedWithinTheDestination`
- **BRIDGE-IMAGE-032** — A rippled image shall keep its edges opaque, sampling the image's edge for pixels displaced from outside it, and draw nothing past its destination rect.
  - Tests: `FilteredImageTests.RippleKeepsTheEdgesOpaque`, `FilteredImageTests.RippleMovesPixelsTheSameWayForTheSameSeedWithinTheDestination`
- **BRIDGE-IMAGE-033** (inherited) — `RippleScale`, `DissolveScale` and `DissolveSoftness` below 1e-3 shall be treated as 1e-3.
  - Tests: untested

### Animating the properties

- **BRIDGE-IMAGE-034** — Every filter and effect shall be an ordinary styled property that affects render, so `Animate`, builders, `Motion` states, bindings, style setters and `Revert` work on it unchanged; `Revert` restores the local value.
  - Tests: `FilteredImageTests.AnAnimatedFilterRendersBetweenFramesAndRevertsToTheLocalValue`, `FilteredImageTests.AHoverOnSaturateReturnsToTheBaseValue`
- **BRIDGE-IMAGE-035** — The builder methods (`.Grayscale(1)`, `.Dissolve(…)`, …) shall write the `PrestoImageBase` property key, the same as the indexer, so they animate every subclass of the base.
  - Tests: `AnimationBuilderBridgeTests.ImageFilterMethodsWriteTheIndexerKeysAndValues`, `AnimationBuilderBridgeTests.ImageFilterOverloadsWriteTheSameKey`, `AnimationBuilderBridgeTests.AFilterMethodAnimatesEveryImageOnTheBase`
- **BRIDGE-IMAGE-036** — `AnimateOnCompositor` naming a filter property shall throw `NotSupportedException`.
  - Tests: `FilteredImageTests.AnimatingAFilterOnTheCompositorIsNotSupported`

### Source transitions

- **BRIDGE-IMAGE-037** — `TransitionTo(source, transition)` shall set `Source` at once and return the `Animation` that moves the transition from the previous image (progress 0) to `source` alone (progress 1).
  - Tests: `ImageTransitionTests.ProgressZeroDrawsTheOldImageAndOneTheNew`
- **BRIDGE-IMAGE-038** — When the transition's `Builder` is null, the transition shall last 600 ms eased with `Eases.InOutQuad`; otherwise the builder's timing shall apply.
  - Tests: untested
- **BRIDGE-IMAGE-039** — While a transition runs, the control shall arrange to the incoming image, so the outgoing image never shows outside the incoming one's placement under any `Stretch`, including after an interruption.
  - Tests: `ImageTransitionTests.TheOldImageNeverShowsBesideANewOneOfAnotherAspect`, `ImageTransitionTests.AnInterruptedTransitionNeverShowsBesideANewImageOfAnotherAspect`
- **BRIDGE-IMAGE-040** — `Crossfade` shall draw the outgoing image opaque and the incoming one over it at opacity `p`, so an opaque pair never darkens mid-way.
  - Tests: `ImageTransitionTests.AHalfCrossfadeOfAnOpaquePairDoesNotDarken`
- **BRIDGE-IMAGE-041** — `Ripple` shall crossfade as `Crossfade` does while displacing both images by `PeakRipple · sin(π·p)` DIPs.
  - Tests: `ImageTransitionTests.AnOpaqueRipplePairNeverDarkens`, `ImageTransitionTests.BothImagesContributeMidway`
- **BRIDGE-IMAGE-042** — `Dissolve` shall draw the incoming image over the outgoing one with its `Dissolve` at `1 − p`, overriding the control's own `Dissolve`.
  - Tests: `ImageTransitionTests.AHalfDissolveShowsRedAndBluePixels`
- **BRIDGE-IMAGE-043** — `Wipe` shall draw the incoming image over the outgoing one behind a linear edge at `Angle` degrees (0 sweeping left to right) with an edge `Feather` wide, revealed by `p`.
  - Tests: `ImageTransitionTests.BothImagesContributeMidway`
- **BRIDGE-IMAGE-044** — `Pixelate` shall draw only the outgoing image before `p = 0.5` and only the incoming one from then on, with blocks of `1 + (PeakPixelate − 1)·(1 − |2p − 1|)` DIPs, and its edge blocks shall stay opaque.
  - Tests: `ImageTransitionTests.PixelateBlocksTheOldImageThenTheNewOne`, `ImageTransitionTests.APixelateTransitionNeverDarkensTheEdgeBlocks`
- **BRIDGE-IMAGE-045** — The control's color filters shall apply to both images during a transition, and its noise tuning (`Seed`, `DissolveScale`, `DissolveSoftness`, `RippleScale`) shall come from the control.
  - Tests: `ImageTransitionTests.ColorFiltersApplyToBothImagesMidTransition`
- **BRIDGE-IMAGE-046** — When a transition completes, the control shall release the outgoing image and draw the new source alone.
  - Tests: `ImageTransitionTests.CompletingReleasesTheOutgoingImage`
- **BRIDGE-IMAGE-047** — When the transition's animation is reverted, the control shall snap to the new source and release the outgoing image.
  - Tests: `ImageTransitionTests.RevertSnapsToTheNewSource`
- **BRIDGE-IMAGE-048** — When a new transition starts while one is running, the control shall cancel the running animation and use a snapshot of the composite on screen, the size of the control, as the outgoing image, drawn filling the control with no color filters of its own.
  - Tests: `ImageTransitionTests.AnInterruptionStartsFromTheCompositeOnScreenAndEndsOnTheNewSource`
- **BRIDGE-IMAGE-049** (inherited) — When `TransitionTo` is given the current `Source` and no transition is running, it shall change nothing on screen and return an animation that only runs the transition's timing.
  - Tests: `ImageTransitionTests.TheSameSourceWithNoTransitionRunningChangesNothingButStillRunsTheTiming`
- **BRIDGE-IMAGE-050** — While `SourceTransition` is set and the control is attached, a change of `Source` from a non-null image, including through a binding, shall play that transition from the previous image.
  - Tests: `ImageTransitionTests.WithSourceTransitionAssigningSourcePlaysATransition`, `ImageTransitionTests.ABoundSourceChangeTransitions`
- **BRIDGE-IMAGE-051** — When `SourceTransition` is null, the control is detached, or the previous source was null, a `Source` change shall snap as `Image` does.
  - Tests: `ImageTransitionTests.WithoutSourceTransitionSourceSnaps`, `ImageTransitionTests.SettingSourceBeforeAttachDoesNotTransition`
- **BRIDGE-IMAGE-052** — `TransitionTo` shall not also trigger `SourceTransition` for its own `Source` write.
  - Tests: untested
- **BRIDGE-IMAGE-053** — When the control detaches mid-transition, it shall complete the animation and dispose the snapshot it held.
  - Tests: `ImageTransitionTests.DetachingMidTransitionReleasesTheOutgoingImage`

### ImageTransition values

- **BRIDGE-IMAGE-054** — `ImageTransition.Parse` shall accept a case-insensitive kind optionally followed by a duration in ms (invariant culture), eased with `Eases.InOutQuad`; the Avalonia XAML compiler shall use it for attributes and style setters, and the element form shall set the tuning.
  - Tests: `ImageTransitionTests.XamlGivesSourceTransitionAsAStringAStyleSetterOrAnElement`
- **BRIDGE-IMAGE-055** (inherited) — When the string is empty, has more than two parts, or its first part is not a kind, `Parse` shall throw `FormatException`; a second part that is not a number shall also throw `FormatException`.
  - Tests: untested
- **BRIDGE-IMAGE-056** (inherited) — `ImageTransition` shall compare by value.
  - Tests: `ImageTransitionTests.XamlGivesSourceTransitionAsAStringAStyleSetterOrAnElement`

### Defaults

- **BRIDGE-IMAGE-057** — When none of the color filters and effects is set, `Brightness`, `Contrast` and `Saturate` shall be 1, `Pixelate` 1, and `Grayscale`, `Sepia`, `HueRotate`, `Tint`, `Duotone`, `Invert`, `Ripple` and `Dissolve` 0, so the control draws as `Image` does (Identity draws plain).
  - Tests: `FilteredImageTests.DefaultFiltersDrawExactlyAsImage`
- **BRIDGE-IMAGE-058** — When `TintColor`, `DuotoneShadow` or `DuotoneHighlight` is not set, it shall be white, black or white respectively.
  - Tests: untested
- **BRIDGE-IMAGE-059** — When `Stretch` or `StretchDirection` is not set, it shall be `Uniform` or `Both`, as `Image`'s own properties are.
  - Tests: untested
- **BRIDGE-IMAGE-060** (inherited) — When `RippleScale` is not set, the ripple noise shall have a feature size of 32 DIPs.
  - Tests: untested
- **BRIDGE-IMAGE-061** (inherited) — When `DissolveScale` is not set, the dissolve noise shall have a feature size of 24 DIPs.
  - Tests: untested
- **BRIDGE-IMAGE-062** (inherited) — When `DissolveSoftness` is not set, the dissolve edge shall be 0.1 wide.
  - Tests: untested
- **BRIDGE-IMAGE-063** (inherited) — When `Seed` is not set, the noise shall be seeded with 0.
  - Tests: untested
- **BRIDGE-IMAGE-064** (inherited) — When an `ImageTransition`'s `Kind`, or `TransitionTo`'s kind, is not given, the transition shall be a `Crossfade`.
  - Tests: untested
- **BRIDGE-IMAGE-065** (inherited) — When `Angle` is not set, a `Wipe` shall sweep at 0°, left to right.
  - Tests: untested
- **BRIDGE-IMAGE-066** (inherited) — When `Feather` is not set, a `Wipe`'s edge shall be 0.2 of the sweep wide.
  - Tests: untested
- **BRIDGE-IMAGE-067** (inherited) — When `PeakPixelate` is not set, a `Pixelate` transition's blocks shall peak at 24 DIPs.
  - Tests: untested
- **BRIDGE-IMAGE-068** (inherited) — When `PeakRipple` is not set, a `Ripple` transition's displacement shall peak at 20 DIPs.
  - Tests: untested

## Invariants

- At rest (every property at identity, no transition running) the control draws exactly what `Image` draws.
- The internal transition progress is 1 whenever no transition is running; the outgoing image and its snapshot are held only while progress is below 1.
- Each draw operation retains the raster or snapshot it draws, and the last holder disposes it, so the render thread never draws a disposed bitmap.

## Edge cases

- `Pixelate` at or below 1 and `Dissolve` at or below 0 are identity; a negative `Ripple` displaces the other way and is not identity.
- A `TransitionTo(null, …)` transitions to nothing: the outgoing image fades, wipes or dissolves away over an empty incoming.
- A `Source` change while a transition runs and `SourceTransition` is null does not start a new one: the running transition continues from its outgoing image to the new source.
- `ImageTransition.Parse` takes a numeric kind ("1") through `Enum.TryParse`, and an undefined value draws as `Crossfade`.
- Only `DrawingImage` invalidation is observed; another custom `IImage` re-rasterizes only when its key (image, source rect, size, scaling) changes.

## Threading and lifetime

- UI thread only: properties, transitions and `Render` run on the UI thread; the draw operations run on the render thread and own their native Skia filters and paints, disposed with the operation.
- A transition is an ordinary `Animation` on the control (BRIDGE-HELPERS `Animate`) and follows its lifetime rules; detaching completes it.

## Reduced motion

- Not affected directly: filters and transitions are ordinary property animations, so `ReduceAll` lands a transition at progress 1 at once and a `WhenReduced` alternative in a transition's `Builder` applies. The image recipes' reduced forms are BRIDGE-RECIPES's.

## Allocation

- A running filter or effect tween holds the `double`-property budget on the managed side; native filters are created per render and are not budgeted.
  - Pinned by `AllocationTests.AnImageFilterTweenTicksWithinBudget`.

## Deviations from anime.js

- No upstream counterpart. CSS `filter` chains are user-ordered; these properties compose in a fixed order so each can animate on its own.

## Decisions

- **Filters live on an `Image`-compatible base control.** A public abstract `PrestoImageBase : Control` re-exposes `Image`'s `Source`, `Stretch` and `StretchDirection` and draws through an unsealed `Render`; `FilteredImage`, `SpriteSheet` and `ImageSequence` derive from it, and each filter is a plain styled property that affects render. Reason: Avalonia seals `Image.Render`, `Visual.Effect` has no color-matrix effect and `IEffect` is not extensible; plain styled properties work with every Presto animation path without a composer, accessor kind or codec, matching `Image`'s surface makes swapping a rename, and frames get the filters too. Rejected: filter shorthands on arbitrary visuals (not expressible without shaders).
- **CSS filter names and semantics, plus Tint and Duotone.** The filters are the Filter Effects Level 1 set with its matrices and ranges, plus `Tint` (a monotone) and `Duotone`. Reason: an anime.js `filter: 'grayscale(1)'` ports one-to-one; Tint and Duotone are still affine, so they stay one matrix.
- **Filters compose into one matrix in a fixed order.** Brightness → Contrast → Saturate → Grayscale → Sepia → HueRotate → Tint → Duotone → Invert, whatever order they are set in. Reason: CSS chains are user-ordered but properties cannot be; a fixed order lets each filter animate on its own. Rejected: a CSS-syntax `Filter` string.
- **Saturate uses the `grayscale()` luminance weights.** `Saturate` uses 0.2126/0.7152/0.0722 rather than `feColorMatrix`'s rounded 0.213/0.715/0.072; `HueRotate` keeps the published matrix. Reason: so `Grayscale(a)` equals `Saturate(1 − a)` exactly.
- **Identity draws plain.** When every filter and effect is at identity, the image is drawn directly with no layer. Reason: effects are removed at rest.
- **The draw interleaves the Skia lease with Avalonia's bitmap draw.** A custom draw operation leases the canvas to push the effect layer, releases it, draws through `ImmediateDrawingContext.DrawBitmap`, then leases again to restore. Reason: Avalonia's own sampling, interpolation mode and opacity apply to the bitmap, and ancestor opacity, clip and transforms still apply. Rejected: copying the bitmap into a cached `SKImage` drawn inside the lease (not needed).
- **Without a Skia lease, draw unfiltered.** Reason: non-Skia backends and headless drawing must still show the image, and nothing should throw.
- **The bridge references `Avalonia.Skia`.** Reason: it provides the lease feature and SkiaSharp, Skia is the renderer on every head the repo ships, and every head already carries it.
- **Native filters belong to each draw operation.** Each operation creates and disposes its own Skia color filter and paint, with no per-control cache. Reason: simplest ownership; a cache is to be added only if per-frame native allocation shows up in measurement, and it has not.
- **Non-bitmap sources are rasterized per draw and reference-counted.** A raster is keyed on image, source rect, destination size and scaling; rasters drawn in a render are kept, the rest dropped after it, and each draw operation retains its raster so the last holder disposes it. Reason: a transition's two images or a subclass's cells each keep their own raster instead of evicting one another every frame, and `Bitmap`'s ref-counted implementation is internal, so the render thread could otherwise draw a disposed raster. Rejected: one raster at the control's size.
- **Filters do not animate on the compositor.** `AnimateOnCompositor` naming one throws through the unknown-property path. Reason: filter values live in UI-thread render data.
- **Effects and transitions are built from Skia built-ins, not shaders.** Dissolve, Pixelate, Ripple and the transitions use Skia's noise, displacement, matrix and blend filters. Reason: no shader code is needed; earlier, pixel transitions were left out because they seemed to need a shader path. Rejected: SkSL runtime effects, until built-ins prove insufficient.
- **Each effect is also a standalone property.** `Dissolve`, `Pixelate` and `Ripple`, with their tuning, are properties of the image, not only transition kinds. Reason: a single image can be revealed or hidden through `Presence` without a second source.
- **One image-filter chain: color matrix, Ripple, Pixelate, Dissolve mask.** Stages at identity are skipped. Reason: the dissolve mask must be neither rippled nor pixelated; it was first drawn inside the layer and moved into the chain for that reason.
- **Pixelate samples nearest-neighbor from the destination's top-left.** It scales down and back up with nearest filtering over an input edge-extended by one block, cropped to the destination, with blocks in DIPs. Reason: each block is one sampled pixel rather than an average, edge extension keeps partial blocks opaque and inside the image, and image filters run in canvas-local space.
- **Ripple displaces by centered fractal noise.** The displacement scale is `Ripple` itself, not multiplied by render scaling, over an input edge-extended by `Ripple` DIPs and cropped to the destination. Reason: fractal noise is centered on 0.5, Skia already maps scale and noise through the canvas matrix, and edge extension and cropping keep the edges opaque and the ripple inside the image rect. Rejected: turbulence, which sums absolute noise and would shift the whole image.
- **Noise is seeded.** Reason: a dissolve is repeatable in tests and does not shimmer between frames.
- **Source swaps transition only when opted in.** `SourceTransition` plays on every `Source` change; without it, `Source` snaps. Reason: keeps `Image`'s default behavior.
- **`TransitionTo` returns the animation, and an interruption starts from the screen.** A change mid-transition snapshots the composite into a control-sized raster that becomes the outgoing image, drawn filling the control with no color filters, released on completion, revert or detach. Reason: the returned animation can be awaited, sequenced or reversed; starting from what is on screen avoids a jump, and the snapshot already shows the filters.
- **The incoming image draws over an opaque outgoing one.** Crossfade and Ripple draw the outgoing image opaque and the incoming at opacity `p`; Dissolve and Wipe mask the incoming over the outgoing; Pixelate switches images mid-way. Reason: an opaque pair never darkens mid-way (red → blue passes (128,0,128), not (64,0,128)). Rejected: fading both images symmetrically.
- **The control arranges to the incoming image.** Reason: the old image never shows beside a new one of another aspect, and Pixelate's mid-way switch cannot strand any of it; the cost is that a source of another aspect resizes the control when the transition starts.
- **Transition progress and the wipe stay internal.** Progress is an internal styled property (default 1) animated by an ordinary builder; the wipe is an internal field of the effect; noise tuning comes from the image, not `ImageTransition`. Reason: a user-set progress has no outgoing image to show, animating a styled property makes `Revert` return to 1 and release the outgoing image, and `MaskSweep` already wipes any element.
- **Subclasses draw through a protected `DrawImage`.** The overload taking an image and source rect is protected; the full overload stays `private protected`. Reason: the full overload exposes internal types.
- **`ImageTransition` has a string form through a static `Parse`.** A kind and an optional duration in ms, eased `InOutQuad`; a null `Builder` plays 600 ms. Reason: the Avalonia XAML compiler finds a static `Parse` for attributes and setters. Rejected: a type converter or markup extension.
- **Exit ghosts do not carry pixel effects.** An exit cannot dissolve or pixelate; the documented route is to animate the live `FilteredImage`, await it, then remove it. Reason: the `Presence` exit ghost is a plain `Image` snapshot.
- **Tint defaults to white, Duotone to black and white.** An unset `TintColor` is white, and unset `DuotoneShadow` and `DuotoneHighlight` are black and white. Reason: a white tint is a plain monotone gray, and black-to-white duotone is the identity mapping of luminance.
- **Builder methods write the typed property key.** See BRIDGE-HELPERS.

## Non-goals

- Filters on arbitrary visuals (BRIDGE-DISTORT draws a decorator's child through raster effects), a CSS-syntax `Filter` string, an image-level `Blur`, SkSL shaders, and transitions between arbitrary content.
- Pixel effects on `Presence` exit ghosts, which are plain snapshots.
- Compositor-thread animation of the filters.
- A public transition progress or a standalone wipe property (`MaskSweep` wipes any element).

## Open items

- A source of another aspect resizes the control when a transition starts, re-placing the old image inside the new bounds; whether it should resize only when the transition ends is open.
- The no-lease fallback (BRIDGE-IMAGE-016), the clamped filter amounts (BRIDGE-IMAGE-022), the 1e-3 floors (BRIDGE-IMAGE-033), raster release on detach (BRIDGE-IMAGE-014), the 600 ms default (BRIDGE-IMAGE-038), `TransitionTo` with `SourceTransition` also set (BRIDGE-IMAGE-052) and `Parse` errors (BRIDGE-IMAGE-055) have no test.
- `ImageTransition.Parse` accepts numeric and undefined kind values through `Enum.TryParse`; undefined ones render as `Crossfade`.
- A `Source` change during a running transition with `SourceTransition` null silently retargets the running transition; unspecified by the plan and untested.
- manual — the Browser and Android heads were never smoke-run with a filtered image, so the README has no Platforms note; the Desktop checks (slideshow mid-way screenshots, pixelate card) are manual on the Image effects page.
