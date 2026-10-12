---
id: LOTTIE-PLAYBACK
title: Lottie playback
package: MonacoLabs.Presto.Avalonia.Lottie
namespaces:
  - MonacoLabs.Presto.Lottie
  - MonacoLabs.Presto.Lottie.Prebuilt
sources:
  - MonacoLabs.Presto.Avalonia.Lottie/LottieComposition.cs
  - MonacoLabs.Presto.Avalonia.Lottie/LottieCompositionConverter.cs
  - MonacoLabs.Presto.Avalonia.Lottie/LottieSegment.cs
  - MonacoLabs.Presto.Avalonia.Lottie/LottieView.cs
  - MonacoLabs.Presto.Avalonia.Lottie/LottieDrawOperation.cs
  - MonacoLabs.Presto.Avalonia.Lottie/LottiePlayerHandle.cs
  - MonacoLabs.Presto.Avalonia.Lottie/LottieVisualHandler.cs
  - MonacoLabs.Presto.Avalonia.Lottie/PlayLottieExtension.cs
  - MonacoLabs.Presto.Avalonia.Lottie/RecipesExtensions.cs
  - MonacoLabs.Presto.Avalonia.Lottie/Prebuilt/LottieRecipes.cs
  - MonacoLabs.Presto.Avalonia.Lottie/Properties/AssemblyInfo.cs
types:
  - LottieComposition
  - LottieCompositionConverter
  - LottieSegment
  - LottieView
  - PlayLottieExtension
  - LottieRecipes
  - RecipesExtensions
tests:
  - LottieCompositionTests
  - DotLottieTests
  - LottieSegmentTests
  - LottieViewTests
  - LottieRecipeTests
  - LottieRenderThreadTests
  - ReferencePagesTests
upstream: []
depends:
  - BRIDGE-IMAGE
  - BRIDGE-RECIPES
  - BRIDGE-MARKUP
  - BRIDGE-TARGETS
  - CORE-BUILDER
  - CORE-REDUCED
  - CORE-COMPOSITION
---

# Lottie playback

## Purpose

Plays Lottie animations through SkiaSharp.Skottie, in a separate package so the bridge takes no Skottie dependency. A `LottieComposition` is the parsed, immutable file. A `LottieView` (a `PrestoImageBase`, see BRIDGE-IMAGE) draws one fractional `Frame` of it, so anything that can tween, seek, scroll-sync or bind a `double` plays it. `Play`/`PlaySegment`, the `PlayLottie` recipe (BRIDGE-RECIPES) and `{presto:PlayLottie}` (BRIDGE-MARKUP) are linear `Frame` tweens on the main engine. Opt-in `RenderThread` playback keeps a spinner moving while the UI thread is blocked.

## Requirements

### Composition: parsing

- **LOTTIE-PLAYBACK-001** (inherited) — The composition shall read `Width`, `Height`, `FrameRate`, `InPoint` and `OutPoint` from the top-level `w`, `h`, `fr`, `ip` and `op`, and report `Size` as `Width` by `Height` and `Duration` as `(OutPoint - InPoint) / FrameRate * 1000` milliseconds.
  - Tests: `LottieCompositionTests.MetadataComesFromTheTopLevelFields`
- **LOTTIE-PLAYBACK-002** (inherited) — When the input is not JSON, Skottie cannot build an animation from it, or any of `w`, `h`, `fr`, `ip`, `op` is missing or not a number, the factory shall throw `FormatException`.
  - Tests: `LottieCompositionTests.InvalidInputThrowsFormatException`
- **LOTTIE-PLAYBACK-003** (inherited) — When `FromStream` reads JSON that starts with a UTF-8 byte order mark, the composition shall drop the mark before parsing.
  - Tests: `LottieCompositionTests.LoadsAFileUriOrPathWithAByteOrderMark`
- **LOTTIE-PLAYBACK-004** — When the stream passed to `FromStream` starts with the zip signature `PK\x03\x04`, it shall be read as `FromDotLottie` with the default animation.
  - Tests: `DotLottieTests.FromStreamDetectsTheZipSignature`
- **LOTTIE-PLAYBACK-005** (inherited) — `Load` shall open a relative URI as a file path, a `file://` URI by its local path, and any other absolute URI (`avares://`) through the asset loader, then read the stream as `FromStream` does.
  - Tests: `LottieCompositionTests.LoadsAnAvaloniaAsset`, `LottieCompositionTests.LoadsAFileUriOrPathWithAByteOrderMark`
- **LOTTIE-PLAYBACK-006** — The type converter shall convert a string to a composition through `Load(new Uri(value, UriKind.RelativeOrAbsolute))`, so XAML can set `Composition` from a URI string.
  - Tests: `LottieCompositionTests.TheTypeConverterLoadsAUriString`, `LottieRecipeTests.TheMarkupExtensionPlaysItsSegmentOnAttach`
- **LOTTIE-PLAYBACK-007** (inherited) — As an `IImage`, the composition shall report `Size` for layout and draw nothing; only a `LottieView` draws it.
  - Tests: untested
- **LOTTIE-PLAYBACK-008** (inherited) — A composition read from plain JSON shall have empty `AnimationIds`.
  - Tests: `DotLottieTests.TheFirstAnimationIsReadByDefaultAndTheManifestIdsAreListed`

### Composition: markers and segments

- **LOTTIE-PLAYBACK-009** — The composition shall expose the JSON `markers` as `Segments` in file order, named by `cm`, starting at `tm`; a marker with `dr > 0` shall end at `tm + dr`, and one without shall end at the smallest later marker start, or `OutPoint` when none is later.
  - Tests: `LottieSegmentTests.MarkersBecomeSegmentsAndZeroDurationMarkersRunToTheNextMarkerOrTheOutPoint`
- **LOTTIE-PLAYBACK-010** (inherited) — When a marker's `cm` is a JSON object string with a string `name`, the segment shall be named by that `name`; any other `cm` shall be used as written.
  - Tests: `LottieSegmentTests.MarkersBecomeSegmentsAndZeroDurationMarkersRunToTheNextMarkerOrTheOutPoint`
- **LOTTIE-PLAYBACK-011** (inherited) — When the JSON has no `markers` array, `Segments` shall be empty.
  - Tests: `LottieSegmentTests.MissingMarkersGiveNoSegments`
- **LOTTIE-PLAYBACK-012** (inherited) — `TryGetSegment` shall return the first segment whose name equals the given name ignoring case, and false when none does.
  - Tests: `LottieSegmentTests.TryGetSegmentIgnoresCase`

### Composition: dotLottie

- **LOTTIE-PLAYBACK-013** (inherited) — `FromDotLottie` shall list the manifest's `animations[].id` in manifest order as `AnimationIds`.
  - Tests: `DotLottieTests.TheFirstAnimationIsReadByDefaultAndTheManifestIdsAreListed`
- **LOTTIE-PLAYBACK-014** — When no `animationId` is given, `FromDotLottie` shall read the manifest's `initial.animation`, else its `activeAnimationId`, else its first animation.
  - Tests: `DotLottieTests.TheManifestsInitialAnimationIsReadByDefault`, `DotLottieTests.TheFirstAnimationIsReadByDefaultAndTheManifestIdsAreListed`
- **LOTTIE-PLAYBACK-015** — When an `animationId` is given, `FromDotLottie` shall read `animations/<id>.json`, or `a/<id>.json` when that is missing.
  - Tests: `DotLottieTests.AnAnimationIsReadById`
- **LOTTIE-PLAYBACK-016** (inherited) — When the manifest lists no animation with the requested id, `FromDotLottie` shall throw `ArgumentException` naming the id and listing the available ids.
  - Tests: `DotLottieTests.AnUnknownAnimationIdThrowsListingTheAvailableIds`
- **LOTTIE-PLAYBACK-017** (inherited) — When the archive has no `manifest.json`, lists no animations, has no JSON for the chosen animation, or is not a valid zip or JSON, `FromDotLottie` shall throw `FormatException`.
  - Tests: `DotLottieTests.AMissingManifestThrowsFormatException`
- **LOTTIE-PLAYBACK-018** — `FromDotLottie` shall inline each image asset the archive holds (looked up under the asset's `u` folder, then `images/`, then `i/`) into the JSON as a base64 `data:` URI, leaving assets already given as `data:` URIs or not found in the archive unchanged.
  - Tests: `DotLottieTests.AnArchiveImageIsDrawnWhereItsLayerSits`
- **LOTTIE-PLAYBACK-019** — An image asset given as a `data:` URI in plain JSON shall be drawn.
  - Tests: `DotLottieTests.ADataUriImageInPlainJsonIsDrawn`
- **LOTTIE-PLAYBACK-020** — `FromDotLottie` shall ignore the archive's themes and state machines.
  - Tests: untested

### View: Frame and Progress

- **LOTTIE-PLAYBACK-021** — The view shall draw `Frame` as given, fractional frames included, without flooring.
  - Tests: `LottieViewTests.FractionalFramesAreDrawnAsGivenNotFloored`
- **LOTTIE-PLAYBACK-022** — While `Composition` is set, the view shall coerce `Frame` to `InPoint`..`OutPoint`; it shall always coerce `Progress` to 0..1.
  - Tests: `LottieViewTests.FrameAndProgressClampToTheComposition`
- **LOTTIE-PLAYBACK-023** — When `Frame` changes and the composition has frames, the view shall set `Progress` to `(Frame - InPoint) / (OutPoint - InPoint)`; when `Progress` changes, it shall set `Frame` to `InPoint + Progress * (OutPoint - InPoint)`. Both sync through `SetCurrentValue`.
  - Tests: `LottieViewTests.FrameAndProgressFollowEachOther`
- **LOTTIE-PLAYBACK-024** — When `Composition` changes, the view shall reset `Frame` to the new `InPoint` and `Progress` to 0, and draw by seeking relative to the new in point.
  - Tests: `LottieViewTests.ChangingTheCompositionResetsFrameToItsInPointAndSeeksFromIt`
- **LOTTIE-PLAYBACK-025** — A tween, a seek, a reverse, a timeline child, a scroll-synced animation or a motion value driving `Frame` shall redraw the frame it sets.
  - Tests: `LottieViewTests.AnAnimationSeeksPlaysAndReversesTheFrame`, `LottieViewTests.ATimelineChildDrivesTheFrame`, `LottieViewTests.AScrollSyncedAnimationDrivesTheFrame`, `LottieViewTests.AMotionValueDrivesTheFrame`

### View: drawing

- **LOTTIE-PLAYBACK-026** — The view shall measure by the composition's `Size` and place it by `Stretch` and `StretchDirection` as an image, clipped to its bounds; the inherited `Source` is not used.
  - Tests: `LottieViewTests.StretchScalesTheComposition`
- **LOTTIE-PLAYBACK-027** — The view shall draw through the base's color filters (`Grayscale`, `Tint` and the rest; see BRIDGE-IMAGE) when they are not identity.
  - Tests: `LottieViewTests.GrayscaleAppliesToTheLottieOutput`, `LottieViewTests.TintAppliesToTheLottieOutput`
- **LOTTIE-PLAYBACK-028** (inherited) — When `Composition` is null or the placed rect does not intersect the view's bounds, the view shall draw nothing.
  - Tests: untested
- **LOTTIE-PLAYBACK-029** — The view shall hold at most one Skottie player at a time, built on first draw, and release it on detach and on a `Composition` change once no pending draw still holds it.
  - Tests: `LottieViewTests.DetachingReleasesTheOnlyPlayerTheViewEverHeld`
- **LOTTIE-PLAYBACK-030** — When an immediate context (a snapshot or a Presence exit ghost) renders the view, it shall draw the current frame and release its hold on the player at once.
  - Tests: `LottieViewTests.AnExitGhostSnapshotsTheFrameWithoutKeepingAPlayer`

### View: playback verbs

- **LOTTIE-PLAYBACK-031** — `Play()` shall return an animation on the current engine that tweens `Frame` linearly (`Eases.None`) from `InPoint` to `OutPoint` over `(OutPoint - InPoint) / FrameRate * 1000 / speed` ms, and completes at `OutPoint`.
  - Tests: `LottieSegmentTests.PlayRunsTheWholeCompositionAtItsFrameRate`
- **LOTTIE-PLAYBACK-032** — `Play(marker)` shall play the segment `TryGetSegment` finds, ignoring case, timed by its frame span at the frame rate divided by `speed`.
  - Tests: `LottieSegmentTests.PlayAMarkerRunsItsSegmentAndSpeedScalesTheDuration`, `ReferencePagesTests.LottieRunsAndReverts`
- **LOTTIE-PLAYBACK-033** — When `reverse` is true, `Play` shall tween from the segment's end to its start.
  - Tests: `LottieSegmentTests.ReversePlaysTheSegmentFromItsEnd`
- **LOTTIE-PLAYBACK-034** — When `loop` is true, the animation shall repeat the segment forever.
  - Tests: `LottieSegmentTests.LoopRestartsTheSegment`
- **LOTTIE-PLAYBACK-035** — `PlaySegment(from, to)` shall tween `Frame` from `from` to `to`, backwards when `to` is the lower, over `|to - from| / FrameRate * 1000 / speed` ms.
  - Tests: `LottieSegmentTests.PlaySegmentWithToBelowFromPlaysBackwards`
- **LOTTIE-PLAYBACK-036** — When a play starts while another is tweening the view's `Frame`, the new one shall replace it and the first shall be canceled (default replace composition, CORE-COMPOSITION).
  - Tests: `LottieSegmentTests.StartingAnotherPlayReplacesTheFirst`
- **LOTTIE-PLAYBACK-037** (inherited) — When the composition has no marker of the given name (or there is no composition), `Play(marker)` and `PlayOnRenderThread(segment)` shall throw `ArgumentException` listing the composition's marker names.
  - Tests: `LottieSegmentTests.AnUnknownMarkerThrowsNamingTheMarkers`
- **LOTTIE-PLAYBACK-038** (inherited) — When the composition's `FrameRate` is not positive or `Composition` is null, a play's duration shall be 0.
  - Tests: untested

### Recipe and markup

- **LOTTIE-PLAYBACK-039** — `Recipes.PlayLottie` (the `extension(Recipes)` member) shall return the same builder as `LottieRecipes.PlayLottie` with the same arguments.
  - Tests: `LottieRecipeTests.TheRecipeReadsEachViewsComposition`
- **LOTTIE-PLAYBACK-040** — The recipe shall resolve the segment, its direction and its duration per target from that target's own composition, as `LottieView.Play` would; its default `loop` is true.
  - Tests: `LottieRecipeTests.TheRecipeReadsEachViewsComposition`, `LottieRecipeTests.TheRecipePlaysANamedSegmentInReverseAfterItsDelay`
- **LOTTIE-PLAYBACK-041** — When `delay` is given, the recipe shall set it as the builder's `Delay`.
  - Tests: `LottieRecipeTests.TheRecipePlaysANamedSegmentInReverseAfterItsDelay`
- **LOTTIE-PLAYBACK-042** (inherited) — For a target that is not a `LottieView`, the recipe shall resolve a zero-length segment at frame 0 with duration 0.
  - Tests: `LottieRecipeTests.AnyOtherTargetIsAZeroLengthSegment`
- **LOTTIE-PLAYBACK-043** (inherited) — When a named segment is missing from a target's composition, the recipe shall throw `ArgumentException` when it is animated on that target, not when it is built.
  - Tests: untested
- **LOTTIE-PLAYBACK-044** — `{presto:PlayLottie}` shall build `LottieRecipes.PlayLottie(Segment, Loop, Reverse, Speed, Delay)`, with `Loop` defaulting to true, and play it where it is set (for example `Animate.OnAttached`).
  - Tests: `LottieRecipeTests.TheMarkupExtensionPlaysItsSegmentOnAttach`
- **LOTTIE-PLAYBACK-045** — When the extension's `Duration` or `Ease` is set, it shall override the recipe's frame-rate duration or linear ease.
  - Tests: `LottieRecipeTests.TheMarkupExtensionTakesAnExplicitDurationAndEase`
- **LOTTIE-PLAYBACK-046** (inherited) — The extension shall have no exit form: on `Presence.Exit` or with `Out` set it shall build the same forward recipe.
  - Tests: untested

### Reduced motion

- **LOTTIE-PLAYBACK-047** — While reduced motion is on, `PlayLottie`, `Play` and `PlaySegment` shall jump to the segment's end frame (its start when reversed) in a zero-duration animation and not loop.
  - Tests: `LottieRecipeTests.ReducedMotionLandsOnTheSegmentsEndWithoutLooping`

### Render-thread playback

- **LOTTIE-PLAYBACK-048** — While `RenderThread` is set and the view is attached, the view shall draw through a child composition visual that owns its own Skottie player, releasing the UI-thread player.
  - Tests: `LottieRenderThreadTests.FramesAdvanceWithoutTheUIThreadEngine`
- **LOTTIE-PLAYBACK-049** — `PlayOnRenderThread(segment, loop, speed)` shall play the named segment, or the whole composition when null, on a render-thread engine of the visual's own with the same frame tween, timing and reduced form as `Play`; its default `loop` is true.
  - Tests: `LottieRenderThreadTests.FramesAdvanceWithoutTheUIThreadEngine`
- **LOTTIE-PLAYBACK-050** — Render-thread playback shall not write `Frame` back to the view.
  - Tests: `LottieRenderThreadTests.FramesAdvanceWithoutTheUIThreadEngine`
- **LOTTIE-PLAYBACK-051** — The view shall keep a play request and start it each time the visual is (re)created, that is, on attach with `RenderThread` set and when `RenderThread` turns on.
  - Tests: untested
- **LOTTIE-PLAYBACK-052** (inherited) — `StopRenderThread` shall drop the kept request and hold the frame playback reached.
  - Tests: `LottieRenderThreadTests.StopHoldsTheFrame`
- **LOTTIE-PLAYBACK-053** — When `Frame`, `Progress` or `Composition` is written while render-thread playback runs, the view shall seek the drawing to the new frame, stop playback and drop the kept request.
  - Tests: `LottieRenderThreadTests.WritingFrameSeeksAndStopsPlayback`
- **LOTTIE-PLAYBACK-054** (inherited) — When `RenderThread` turns off, the view shall remove the visual and return to drawing its own `Frame`.
  - Tests: `LottieRenderThreadTests.TogglingOffReturnsToFrameDrivenDrawing`
- **LOTTIE-PLAYBACK-055** (inherited) — When the view is detached, the render-thread visual shall pause its engine and release its player.
  - Tests: `LottieRenderThreadTests.DetachingReleasesTheRenderThreadPlayer`
- **LOTTIE-PLAYBACK-056** (inherited) — When the visual's engine runs out of children, it shall stop requesting render-thread animation frames.
  - Tests: untested
- **LOTTIE-PLAYBACK-068** (inherited) — Any change of `Frame`, `Progress` or `Composition` shall drop the kept play request, even one made while detached or with `RenderThread` off that has not started; detaching shall not drop it.
  - Tests: untested

### Platforms

- **LOTTIE-PLAYBACK-057** — The package shall load, parse and draw compositions in the Browser head with no extra build flags, trimmed or not.
  - Tests: manual — spiked with a startup probe only; the gallery's Lottie page (`samples/PrestoGallery/PrestoGallery/Pages/LottiePage.axaml`) has not been driven in the Browser head (Phase 44 open check).
- **LOTTIE-PLAYBACK-058** — In the Browser head, render-thread playback shall run on the UI thread and therefore stall with it (see Browser's blocked-UI-thread limitation is documented, not worked around).
  - Tests: manual — block the UI thread with the Lottie page's render-thread spinner on in the Browser head.

### Defaults

- **LOTTIE-PLAYBACK-059** (inherited) — When `Frame` is not set and no composition is set, the view shall hold frame 0.
  - Tests: untested
- **LOTTIE-PLAYBACK-060** (inherited) — When `Progress` is not set and no composition is set, the view shall hold progress 0.
  - Tests: untested
- **LOTTIE-PLAYBACK-061** — When `RenderThread` is not set, the view shall draw its own `Frame` on the UI-thread path, since render-thread playback is opt-in.
  - Tests: `LottieViewTests.FractionalFramesAreDrawnAsGivenNotFloored`
- **LOTTIE-PLAYBACK-062** (inherited) — When `Play` or `PlaySegment` is not given `loop`, the animation shall play the segment once and complete.
  - Tests: `LottieSegmentTests.PlayRunsTheWholeCompositionAtItsFrameRate`
- **LOTTIE-PLAYBACK-063** (inherited) — When `Play` is not given `reverse`, it shall play the segment from its start to its end.
  - Tests: `LottieSegmentTests.PlayRunsTheWholeCompositionAtItsFrameRate`
- **LOTTIE-PLAYBACK-064** (inherited) — When `Play`, `PlaySegment` or `PlayOnRenderThread` is not given `speed`, it shall play at a speed of 1, the composition's own frame rate.
  - Tests: `LottieSegmentTests.PlayRunsTheWholeCompositionAtItsFrameRate`
- **LOTTIE-PLAYBACK-065** (inherited) — When the `PlayLottie` recipe is not given a segment, or the extension's `Segment` is not set, it shall play each target's whole composition.
  - Tests: `LottieRecipeTests.TheRecipeReadsEachViewsComposition`
- **LOTTIE-PLAYBACK-066** (inherited) — When the `PlayLottie` recipe is not given `reverse` or `speed`, or the extension's `Reverse` or `Speed` is not set, it shall play forwards at a speed of 1.
  - Tests: `LottieRecipeTests.TheRecipeReadsEachViewsComposition`
- **LOTTIE-PLAYBACK-067** (inherited) — When the `PlayLottie` recipe is not given `delay`, it shall leave the builder's `Delay` unset, so the usual delay defaults apply.
  - Tests: untested

## Invariants

- A `LottieComposition` never changes after construction; it holds only the JSON bytes and metadata, and each view (and each render-thread visual) builds its own Skottie animation from it.
- With a composition set, `InPoint <= Frame <= OutPoint` and `0 <= Progress <= 1` after every write.
- The live player count (`LottiePlayerHandle.Live`) returns to its earlier value once every view that drew is detached.

## Edge cases

- `Frame` set before `Composition` is not clamped; setting the composition then resets it to `InPoint`.
- With no composition, or with `OutPoint == InPoint`, a `Frame` write does not update `Progress`, and a `Progress` write sets `Frame` to `InPoint` (0 without a composition).
- `FrameRate` 0 in the JSON is accepted: `Duration` is then infinite or NaN, while play durations are 0 (LOTTIE-PLAYBACK-038).
- `speed` is not validated: 0 gives an infinite play duration and a negative value a negative one.
- A marker without `cm` gets the name `""`, not null; a marker without `tm` starts at 0. A marker's `tm + dr` is not clamped to `OutPoint`; the view's coercion clamps the frame drawn.
- `PlaySegment` does not clamp `from`/`to`; out-of-range values are clamped by `Frame` coercion as the tween writes them.
- The first scene the render-thread visual receives only sets where a queued play starts; it does not stop that play.
- `Load` lets file and asset errors (`FileNotFoundException` and the like) propagate unwrapped.

## Threading and lifetime

- Compositions are shareable across views and threads. Seek and render of one player are serialized under a lock, because an offscreen snapshot may render on the UI thread while the render thread draws.
- `Play`, `PlaySegment` and the recipe tween on the current engine (normally `Presto.Engine` on the UI thread), so they follow DevTools' freeze and time dilation. Render-thread playback runs on an engine owned by the visual, driven by the compositor's animation frames, and ignores the main engine's freeze and dilation.
- The UI-thread player is released on detach and on a composition change; the render-thread player on detach, on `RenderThread` turning off and on a composition change.

## Reduced motion

- The reduced form lands on the segment's end frame (its start when reversed), duration 0, no loop. This deviates from `PlayFrames` (BRIDGE-FRAMES), which holds frame 0, because a Lottie segment ends on the state it plays into.

## Allocation

- Not on the core tick path. Each UI-thread render allocates one draw operation, plus a filter paint while a color filter is active; each render-thread scene message allocates a filter paint the same way. The recipe's per-target function values allocate a segment lookup per call.

## Provenance

- No upstream counterpart.

## Decisions

- **Lottie plays through Skottie; it is not interpreted.** A Presto-driven control draws through Skia's Skottie, with dotLottie folded in. Reason: Skottie costs about as much as sprite playback, and `SkiaSharp.Skottie` matches the SkiaSharp Avalonia resolves; dotLottie proved cheap in the spike. Rejected: a native interpreter (layers to Presto tweens and Avalonia shapes, as big as writing a renderer); `Avalonia.Skia.Lottie` (Avalonia 11 only, not a drop-in).
- **Lottie is a separate package sharing the XAML namespace.** `MonacoLabs.Presto.Avalonia.Lottie` maps into the bridge's XAML namespace, so one `presto:` prefix reaches it; the recipe lives in `LottieRecipes`, and an `extension(Recipes)` block makes `Recipes.PlayLottie` work. Reason: the bridge takes no Skottie dependency, and the bridge's static `Recipes` cannot see the package.
- **`Frame` is drawn as given and clamped.** `Frame` is a `double` that is not floored, coerced to `InPoint`..`OutPoint`; `Progress` mirrors it both ways, and a new `Composition` resets it to `InPoint`. Reason: Lottie is vector, unlike `SpriteSheet`'s cells.
- **An immutable, shared composition and a player per view.** `LottieView.Composition` takes a `LottieComposition` holding only the JSON bytes and metadata, read by `FromJson`, `FromStream`, `FromDotLottie`, `Load(Uri)` or the XAML converter; the inherited `Source` is unused. Each view builds its Skottie animation lazily in its draw operation, held by a ref-counted handle that serializes seek and render under a lock. Reason: views share one parse; offscreen snapshots and exit ghosts render the same operation synchronously on the UI thread while the render thread may draw. Rejected: a `Source` property (a URI, stream or JSON), as first sketched.
- **Reduced Lottie playback lands on the segment's end.** Under reduced motion `PlayLottie` and the view's play methods jump to the segment's end frame and hold it, without looping. Reason: a Lottie segment ends on the state it plays into (a toggle's "on"); a sprite loop has no such state, which is why `PlayFrames` holds frame 0 instead.
- **dotLottie is one animation, with images inlined.** A composition reads one animation (the manifest's initial or active one, else the first, or one by id); referenced images are rewritten to base64 `data:` URIs and every composition builds through `DataUriResourceProvider(preDecode: true)`; themes and state machines are not read. Reason: Skottie's `ResourceProvider` is abstract with no public constructor, so it cannot be subclassed from C# to serve images straight from the zip.
- **Render-thread playback is opt-in.** `LottieView.RenderThread` plays through a `CompositionCustomVisual` on its own engine with the same frame tween as `Play`, so loop, speed and reduced motion match; it does not write `Frame` back, writing `Frame` or `Composition` stops it, and a play request replays on attach. Reason: for spinners that must survive UI-thread stalls, as `CompositorScene` offers.
- **`SkiaSharp.Skottie` is pinned to Avalonia's SkiaSharp.** It is pinned to the exact SkiaSharp version Avalonia resolves, re-checked on every `AvaloniaVersion` bump. Reason: Skottie P/Invokes into that same `libSkiaSharp`.
- **No per-layer recoloring, expressions, or text and effects beyond Skottie.** Reason: Skottie's property observers are not in SkiaSharp's public API, and Skottie does not run expressions.
- **Filters live on an `Image`-compatible base control.** See BRIDGE-IMAGE.
- **Browser's blocked-UI-thread limitation is documented, not worked around.** See BRIDGE-THREAD.

## Non-goals

- dotLottie themes and state machines; more than one animation per composition.
- Per-layer recoloring (Skottie's property observers are not public in SkiaSharp), expressions, and Lottie text and effects beyond what Skottie draws.
- A native Lottie interpreter.
- A `Source` on the view: compositions are set through `Composition`.

## Open items

- Manual: the Lottie page has not been driven in the Browser head (Phase 44).
- `LottieSegment`'s doc says `Name` is null for an unnamed range, but a marker with no `cm` yields `""`; only `Play()`'s whole-composition range has a null name.
- `PlayLottieExtension`'s doc says it "has no exit form", but `RecipeExtension.Out` and `Presence.Exit` still accept it and play the forward recipe; nothing pins that.
- `speed <= 0` is not validated in `Play`, `PlaySegment`, `PlayOnRenderThread` or the recipe; an infinite or negative duration results.
- Reduced motion is pinned only through the recipe; nothing tests it for `Play`/`PlaySegment` or for render-thread playback, which Render-thread playback is opt-in says matches.
- Render-thread playback ignores DevTools freeze and dilation; not tested either way.
- `FrameRate` 0 makes `LottieComposition.Duration` infinite/NaN while play durations are 0; no test.
