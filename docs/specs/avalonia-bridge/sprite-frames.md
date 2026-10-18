---
id: BRIDGE-FRAMES
title: Sprite sheets and image sequences
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto.Controls
sources:
  - MonacoLabs.Presto.Avalonia/Controls/SpriteSheet.cs
  - MonacoLabs.Presto.Avalonia/Controls/ImageSequence.cs
  - MonacoLabs.Presto.Avalonia/Controls/FramePlayer.cs
  - MonacoLabs.Presto.Avalonia/Prebuilt/Sprites.cs
types:
  - SpriteSheet
  - ImageSequence
tests:
  - SpriteSheetTests
  - ImageSequenceTests
  - RecipeTests
  - RecipeMarkupTests
  - BridgeAllocationTests
  - FilteredImageTests
upstream: []
depends:
  - BRIDGE-IMAGE
  - BRIDGE-TARGETS
  - BRIDGE-RECIPES
  - BRIDGE-MARKUP
  - CORE-ANIMATION
  - CORE-REDUCED
---

# Sprite sheets and image sequences

## Purpose

`SpriteSheet` shows one cell of a grid image and `ImageSequence` one image of a list, both selected by a `double` `Frame` shown floored, so an ordinary linear tween gives every frame equal time and seeks, reverses, loops and joins timelines like any animation. Both derive from `PrestoImageBase` (BRIDGE-IMAGE), so frames are sized, stretched and filtered as one image. `Play(fps)` is a thin wrapper over the `Recipes.PlayFrames` builder (BRIDGE-RECIPES), whose XAML form is `{presto:PlayFrames}` (BRIDGE-MARKUP). Also specifies `Recipes.PlayFrames`.

## Requirements

### Frame model

- **BRIDGE-FRAMES-001** — The frame shown shall be `floor(Frame)` clamped to `0 … frameCount − 1`.
  - Tests: `SpriteSheetTests.APartialLastRowHonorsFrameCount`, `ImageSequenceTests.CurrentFrameTracksFrameAndFrames`
- **BRIDGE-FRAMES-002** — When `Wrap` is true, the frame shown shall be `floor(Frame)` modulo the frame count, wrapping negative positions from the end.
  - Tests: `SpriteSheetTests.WrapWrapsInsteadOfHoldingTheLastFrame`
- **BRIDGE-FRAMES-003** (inherited) — When the frame count is zero or `Frame` is NaN, the frame shown shall be 0.
  - Tests: `ImageSequenceTests.NoFramesRendersNothing`
- **BRIDGE-FRAMES-004** — `CurrentFrame` shall be the index shown, updated and raised when `Frame`, `Wrap` or the frame count's inputs change (`FrameCount`, `Columns`, `Rows` on a sheet; `Frames` on a sequence).
  - Tests: `ImageSequenceTests.CurrentFrameTracksFrameAndFrames`, `SpriteSheetTests.APartialLastRowHonorsFrameCount`
- **BRIDGE-FRAMES-005** — A linear tween of `Frame` from 0 to the frame count shall give every frame the same time (±1 sample on a fixed grid) and end on the last frame.
  - Tests: `SpriteSheetTests.ALinearTweenGivesEveryFrameEqualTimeAndEndsOnTheLast`

### SpriteSheet

- **BRIDGE-FRAMES-006** — A sheet shall split `Source` evenly into `Columns` × `Rows` cells and read frames left to right, then top to bottom.
  - Tests: `SpriteSheetTests.EachFrameDrawsItsCell`
- **BRIDGE-FRAMES-007** — When `FrameCount` is set, it shall be the frame count instead of every cell, for a partial last row.
  - Tests: `SpriteSheetTests.APartialLastRowHonorsFrameCount`
- **BRIDGE-FRAMES-008** — When `FrameSize` is set, the sheet shall draw that size from the top-left of each cell, leaving the cell's right and bottom margins out.
  - Tests: `SpriteSheetTests.FrameSizeDrawsFromTheTopLeftOfEachCellWithoutItsMargin`
- **BRIDGE-FRAMES-009** — A sheet shall measure, arrange and stretch as one frame, not the whole sheet.
  - Tests: `SpriteSheetTests.MeasureAndStretchApplyToOneFrameNotTheSheet`
- **BRIDGE-FRAMES-010** — A scaled frame shall show none of its neighboring cells at its edges, plain or filtered.
  - Tests: `SpriteSheetTests.DefaultInterpolationKeepsNeighboringCellsOutOfTheFrameEdges`, `SpriteSheetTests.AFilteredFrameIsFilteredWithoutNeighboringCells`
- **BRIDGE-FRAMES-011** — A sheet's frame shall draw through the base's filters and effects.
  - Tests: `SpriteSheetTests.AFilteredFrameIsFilteredWithoutNeighboringCells`, `FilteredImageTests.ASpriteSheetFrameDissolves`
- **BRIDGE-FRAMES-012** (inherited) — When `Columns` or `Rows` is set to 0 or less, the property system shall reject the value with an `ArgumentException`.
  - Tests: `SpriteSheetTests.ColumnsOrRowsOfZeroOrLessAreRejected`

### ImageSequence

- **BRIDGE-FRAMES-013** — A sequence shall draw `Frames[CurrentFrame]` and ignore the inherited `Source`.
  - Tests: `ImageSequenceTests.EachFrameDrawsItsImage`
- **BRIDGE-FRAMES-014** — A sequence shall measure and arrange to its first frame's size with `Stretch` applied, whatever frame it shows.
  - Tests: `ImageSequenceTests.MeasureReturnsTheFrameSizeWithStretchApplied`
- **BRIDGE-FRAMES-015** (inherited) — When `Frames` is null or empty, a sequence shall measure to zero, show frame 0 and draw nothing.
  - Tests: `ImageSequenceTests.NoFramesRendersNothing`
- **BRIDGE-FRAMES-016** — `FromAssets(uriFormat, count, start)` shall decode `count` bitmaps up front on the calling thread, from `uriFormat` formatted with each index from `start` in the invariant culture.
  - Tests: `ImageSequenceTests.FromAssetsDecodesEachFormattedIndexFromStart`

### Playing

- **BRIDGE-FRAMES-017** — `Play(fps, loop, reverse)` shall return the `Animation` that tweens `Frame` linearly from 0 to the frame count over `frameCount · 1000 / fps` ms per iteration, looping while `loop` is true and played backwards when `reverse` is true.
  - Tests: `SpriteSheetTests.PlayGivesEachFrameAThousandMillisecondsOverFps`, `SpriteSheetTests.PlayReversedAndSoughtShowsTheExpectedFrames`
- **BRIDGE-FRAMES-018** — A played animation shall seek like any animation, showing the frame its position floors to.
  - Tests: `SpriteSheetTests.PlayReversedAndSoughtShowsTheExpectedFrames`
- **BRIDGE-FRAMES-019** — Reverting a played animation shall restore the `Frame` value it started from.
  - Tests: `SpriteSheetTests.RevertRestoresFrame`
- **BRIDGE-FRAMES-020** — `Recipes.PlayFrames` shall read the frame count per target when the animation is built (`EffectiveFrameCount` of a sheet, `FrameCount` of a sequence, 1 for any other target).
  - Tests: `RecipeTests.PlayFramesPlaysAnImageSequencesFrames`, `RecipeMarkupTests.PlayFramesReadsEachSheetsFrameCount`
- **BRIDGE-FRAMES-021** — When `delay` is given, `Recipes.PlayFrames` shall delay the start by that many ms.
  - Tests: `RecipeTests.PlayFramesDelaysOnlyWhenGivenADelayAndDefaultsToTwentyFourFramesASecond`
- **BRIDGE-FRAMES-022** — While `PrestoDefaults.ReducedMotion` is set when the animation is built, `PlayFrames` and `Play` shall set `Frame` to 0 at once, without looping or reversing.
  - Tests: `RecipeTests.ReducedPlayFramesHoldsFrameZero`

### Defaults

- **BRIDGE-FRAMES-023** (inherited) — When `Columns` and `Rows` are not set, a sheet shall treat its whole `Source` as one cell.
  - Tests: `SpriteSheetTests.WithoutColumnsAndRowsTheWholeSourceIsOneFrame`
- **BRIDGE-FRAMES-024** (inherited) — When `Frame` is not set, a sheet or sequence shall show frame 0.
  - Tests: `SpriteSheetTests.AnUnsetFrameShowsTheFirstFrameAndAnUnsetWrapClamps`, `ImageSequenceTests.AnUnsetFrameShowsTheFirstImage`
- **BRIDGE-FRAMES-025** (inherited) — When `Wrap` is not set, the frame shown shall clamp to the frame range rather than wrap.
  - Tests: `SpriteSheetTests.AnUnsetFrameShowsTheFirstFrameAndAnUnsetWrapClamps`
- **BRIDGE-FRAMES-026** — When `fps`, `loop` and `reverse` are not given, `Play` shall play 24 frames a second, looping, forwards.
  - Tests: `SpriteSheetTests.PlayWithoutArgumentsLoopsForwardsAtTwentyFourFramesASecond`
- **BRIDGE-FRAMES-027** — When `start` is not given, `FromAssets` shall format indices from 0.
  - Tests: `ImageSequenceTests.FromAssetsStartsAtZeroWhenNoStartIsGiven`

## Invariants

- `CurrentFrame` is always a valid frame index (0 when there are no frames), whatever `Frame` holds.
- A sheet's drawn region is always computed from `CurrentFrame`; no `CroppedBitmap` or other object is created per frame.

## Edge cases

- A `FrameCount` larger than `Columns · Rows` selects cells past the grid, whose source rect lies outside the image.
- Mutating a `Frames` list in place does not re-resolve `CurrentFrame`; assign a new list. Shrinking it in place below `CurrentFrame` leaves an index past its end.
- `Play` with `fps` 0 gives an infinite duration; with no frames it gives a zero duration.
- The frame count `Play` uses is read when it is called; frames added later are not played until it is called again.

## Threading and lifetime

- UI thread, like the base. `Play` animates on `Presto.Engine` through the bridge's ordinary `Animate` path and follows its detach rules (BRIDGE-HELPERS).
- `FromAssets` holds every decoded frame in memory, about `count · width · height · 4` bytes; the caller owns the list.

## Reduced motion

- `PlayFrames`/`Play` hold frame 0 under `ReducedMotion` (unlike `PlayLottie`, which holds the segment's end; see Decisions). `ReduceAll` lands a hand-built `Frame` tween on its last frame like any animation.

## Allocation

- A playing sheet stays within 112 bytes per tick: a `double` tween plus `CurrentFrame`'s change args on ticks that reach a new frame.
  - Pinned by `BridgeAllocationTests.APlayingSpriteSheetTicksWithinBudget`.

## Provenance

- No upstream counterpart.

## Decisions

- **`Frame` is a `double` shown floored.** Both controls expose a `double` `Frame` and show `floor(Frame)` clamped to the last frame (or wrapped with `Wrap`), with a read-only `int` `CurrentFrame` for bindings. Reason: the bridge rounds a number written to an `int` property, which would give the first and last frames half the time of the others; flooring lets a plain linear tween give every frame equal time with no `Steps` ease, so seek, reverse, loops and timelines just work. Rejected: an `int` `Frame`.
- **Both controls share one frame model on `PrestoImageBase`.** `SpriteSheet` and `ImageSequence` derive from `PrestoImageBase` and keep the frame logic in the internal static `FramePlayer`, with no shared interface; earlier `SpriteSheet : Image` and `ImageSequence : Control`, replaced because `Image.Render` is sealed. Reason: filters apply to frames, and the base re-exposes `Image`'s `Source`/`Stretch`/`StretchDirection`.
- **A sheet draws a source rect, with no per-frame allocation.** `SpriteSheet` measures and arranges to one frame and draws `DrawImage(source, sourceRect, destRect)`, insetting the rect by half a pixel when scaled. Reason: allocation-free playback; filtered sampling reads up to half a source pixel past the rect, which would bleed the neighboring cell. Rejected: a `CroppedBitmap` per frame.
- **`Frames` is observed by assignment only** (see Edge cases). Reason: `IReadOnlyList` carries no change notification, and observing `INotifyCollectionChanged` would serve only the lists that happen to raise it.
- **`ImageSequence` ignores the inherited `Source`.** It draws `Frames[CurrentFrame]` and sizes to `Frames[0]`. Reason: not recorded.
- **`Play` and `PlayFrames` are thin wrappers.** `Play(fps, loop, reverse)` returns the linear `Animation` tweening `Frame` 0 → frame count over `frameCount · 1000 / fps` ms; `Recipes.PlayFrames` is the same builder with `To` and `Duration` as function values reading each target's frame count. Reason: function values make one builder work from XAML on sheets of different lengths.
- **Decoding is eager and the caller's cost.** `FromAssets` decodes every frame up front on the calling thread, and its docs state the `N · w · h · 4` byte cost. Reason: a `Bitmap` decodes on construction. Rejected: lazy or streamed decoding, out of scope.
- **Reduced playback holds frame 0.** `PlayFrames`'s reduced form sets `Frame` to 0 with zero duration, no loop or reverse; `ReduceAll` lands a hand-built tween on its last frame like any animation. Reason: a sprite loop has no end state to land on, unlike a Lottie segment (LOTTIE-PLAYBACK).
- **Filters live on an `Image`-compatible base control.** See BRIDGE-IMAGE.

## Non-goals

- Lazy or streamed decoding of sequences.
- Fractional (interpolated) frames: `Frame` is floored, unlike `LottieView.Frame`, which is vector and drawn as given (LOTTIE-PLAYBACK).
- An exit form of `{presto:PlayFrames}`.

## Open items

None.