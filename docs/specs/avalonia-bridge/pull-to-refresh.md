---
id: BRIDGE-PULL
title: Pull to refresh
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto.Controls
sources:
  - MonacoLabs.Presto.Avalonia/Controls/PullToRefresh.cs
types:
  - PullToRefresh
tests:
  - PullToRefreshTests
upstream: []
depends:
  - CORE-ANIMATABLE
  - CORE-SPRING
  - CORE-MOTIONVALUE
  - CORE-REDUCED
  - BRIDGE-TRANSFORMS
  - BRIDGE-FOLLOW
  - BRIDGE-SHAPES
  - BRIDGE-TOKENS
  - BRIDGE-GESTURES
---

# Pull to refresh

## Purpose

A control that wraps its `Content` and lets a downward drag from the content's top pull it down, uncovering an arrow that turns and a ring that draws as the pull nears `Threshold`. Released past the threshold, the content is held there while an awaited `Refresh` runs and the ring spins, then springs back. It handles the pointer itself rather than through a `Draggable` or Avalonia's touch-only `RefreshContainer`; the arrow and ring are driven by `Follow` shorthands from the pull's `MotionValue` (BRIDGE-FOLLOW, CORE-MOTIONVALUE).

## Requirements

### Layout

- **BRIDGE-PULL-001** — The control shall host `Content` in a track that its pull translates down through the Y shorthand.
  - Tests: `PullToRefreshTests.AShortPullSpringsBackWithoutRefreshing`
- **BRIDGE-PULL-002** (inherited) — The control shall clip to its bounds by default.
  - Tests: `PullToRefreshTests.ItClipsToItsBoundsAndHasASixtyFourDipThresholdByDefault`
- **BRIDGE-PULL-003** (inherited) — The indicator (a 28 × 28 ring stroked 3 DIPs with round caps, and a down arrow stroked 2 DIPs) shall sit in a strip `Threshold` tall just above the content's top, uncovered by the pull, and shall not be hit-testable.
  - Tests: `PullToRefreshTests.TheIndicatorSitsInAThresholdTallStripAboveTheContentAndFollowsAThresholdChange`
- **BRIDGE-PULL-004** (inherited) — When `Threshold` changes, the indicator strip shall be resized and re-placed to match.
  - Tests: `PullToRefreshTests.TheIndicatorSitsInAThresholdTallStripAboveTheContentAndFollowsAThresholdChange`
- **BRIDGE-PULL-005** — The arrow and the ring shall be stroked with `Foreground`, following its changes.
  - Tests: `PullToRefreshTests.TheArrowAndTheRingAreStrokedWithTheForegroundAndFollowItsChanges`

### Starting a pull

- **BRIDGE-PULL-006** (inherited) — A press on the control shall arm a pull, joining the pointer's gesture arena (BRIDGE-GESTURES), unless a pull is already armed or a refresh is running; a mouse press shall arm it only with the left button.
  - Tests: `PullToRefreshTests.AShortPullSpringsBackWithoutRefreshing`
- **BRIDGE-PULL-007** — The pull shall join with a vertical gate (3 DIPs at 1:1 for every pointer) whose `Accepts` takes only downward travel while the nearest `ScrollViewer` between the pressed element and the control is not scrolled away from its top; when the gate drops out, the press shall be forgotten without capturing anything.
  - Tests: `PullToRefreshTests.APullIsLeftToAListScrolledAwayFromItsTop`
- **BRIDGE-PULL-008** (inherited) — When the pull wins the arena (which captures the pointer for it), the control shall from then on mark each move handled and prevent gesture recognition.
  - Tests: `PullToRefreshTests.AWonPullHandlesEachMoveAndPreventsGestureRecognition`
- **BRIDGE-PULL-009** (inherited) — When a pull starts while the content is still springing back, it shall pick the content up where it is drawn.
  - Tests: `PullToRefreshTests.APullStartedWhileTheContentSpringsBackPicksItUpWhereItIsDrawn`

### Pulling

- **BRIDGE-PULL-010** — While pulling, the drawn pull shall be the pointer's downward travel `d` (from the grab) damped to `r(1 − e^(−d / r))` with `r = 2.2 × Threshold`, never below 0, so it rises steadily slower and never reaches `r`.
  - Tests: `PullToRefreshTests.TheResistanceRisesSteadilyAndTheReleaseStartsWhereItWasDrawn`
- **BRIDGE-PULL-011** — The arrow shall turn `180° × clamp(pull / Threshold, 0, 1)`.
  - Tests: `PullToRefreshTests.TheArrowAndTheRingFollowThePull`
- **BRIDGE-PULL-012** — The ring shall draw `0.8 × clamp(pull / Threshold, 0, 1)` of its stroke through `Follow.Draw`.
  - Tests: `PullToRefreshTests.TheArrowAndTheRingFollowThePull`
- **BRIDGE-PULL-013** — `Pull` shall report the content's drawn offset each time it renders, during drags, springs and the refresh hold alike.
  - Tests: `PullToRefreshTests.AShortPullSpringsBackWithoutRefreshing`, `PullToRefreshTests.APullPastTheThresholdHoldsThereUntilTheRefreshEndsThenSpringsBack`

### Releasing

- **BRIDGE-PULL-014** — When a pull is released short of `Threshold`, the content shall spring back to 0 on a spring with bounce 0.2 and duration 600 ms, without calling `Refresh`.
  - Tests: `PullToRefreshTests.AShortPullSpringsBackWithoutRefreshing`
- **BRIDGE-PULL-015** — A release shall start its spring from where the content is drawn, since the damping applies to the travel and not to the drawn value.
  - Tests: `PullToRefreshTests.TheResistanceRisesSteadilyAndTheReleaseStartsWhereItWasDrawn`
- **BRIDGE-PULL-016** (inherited) — When the pointer's capture is lost mid-pull, the control shall settle as on a release.
  - Tests: `PullToRefreshTests.LosingTheCaptureMidPullSettlesAsOnARelease`
- **BRIDGE-PULL-017** (inherited) — The release shall mark the pointer-released event handled only when a pull took place.
  - Tests: `PullToRefreshTests.TheReleaseIsHandledOnlyWhenAPullTookPlace`

### Refreshing

- **BRIDGE-PULL-018** — When a pull is released at or past `Threshold`, the content shall spring to exactly `Threshold`, the arrow shall fade out over `Tokens.Short`, and the ring shall spin a full turn every 800 ms, linearly and looping.
  - Tests: `PullToRefreshTests.APullPastTheThresholdHoldsThereUntilTheRefreshEndsThenSpringsBack`
- **BRIDGE-PULL-019** — A release past the threshold shall call `Refresh` once and await its task, holding the content at the threshold until it ends.
  - Tests: `PullToRefreshTests.APullPastTheThresholdHoldsThereUntilTheRefreshEndsThenSpringsBack`
- **BRIDGE-PULL-020** — When the refresh task ends, the ring's spin shall be reverted to its unturned angle, the arrow shall fade back in over `Tokens.Short`, and the content shall spring back to 0.
  - Tests: `PullToRefreshTests.APullPastTheThresholdHoldsThereUntilTheRefreshEndsThenSpringsBack`
- **BRIDGE-PULL-021** (inherited) — While `Refresh` is null, a release past the threshold shall run the same hold-and-return sequence with no work in between.
  - Tests: `PullToRefreshTests.WithoutARefreshAPullPastTheThresholdHoldsAndReturnsAndTheNextPullStillWorks`
- **BRIDGE-PULL-022** (inherited) — While a refresh runs, presses shall not arm a new pull.
  - Tests: `PullToRefreshTests.APressWhileTheRefreshRunsDoesNotArmANewPull`
- **BRIDGE-PULL-023** — When the refresh task faults or is canceled, or `Refresh` throws, the control shall log it through Avalonia's `Logger` in the `Control` area (a cancellation as a warning, any other exception as an error), not rethrow it, and still stop the spinner and spring back.
  - Tests: `PullToRefreshTests.AFaultedOrCanceledRefreshIsLoggedAndTheContentStillSpringsBack`

### Reduced motion

- **BRIDGE-PULL-024** — While `Presto.Defaults.ReducedMotion` is true at a release, the content shall land on 0 or the threshold at once.
  - Tests: `PullToRefreshTests.UnderReducedMotionTheContentLandsAtOnceAndTheRingHoldsStill`
- **BRIDGE-PULL-025** — While `Presto.Defaults.ReducedMotion` is true at a release past the threshold, the arrow shall hide and later show at once and the ring shall not spin.
  - Tests: `PullToRefreshTests.UnderReducedMotionTheContentLandsAtOnceAndTheRingHoldsStill`
- **BRIDGE-PULL-026** — When the refresh ends, the spring back shall read `ReducedMotion` again, landing at once while it is set.
  - Tests: `PullToRefreshTests.UnderReducedMotionTheContentLandsAtOnceAndTheRingHoldsStill`
- **BRIDGE-PULL-029** — When `ReducedMotion` becomes set during a refresh, the ring shall stop spinning at once; when it clears during a refresh, the ring shall start spinning.
  - Tests: `PullToRefreshTests.SettingReducedMotionMidRefreshStopsTheSpin`, `PullToRefreshTests.ClearingItMidRefreshStartsTheSpin`

### Defaults

- **BRIDGE-PULL-027** — When `Threshold` is not set, it shall be 64 DIPs.
  - Tests: `PullToRefreshTests.ItClipsToItsBoundsAndHasASixtyFourDipThresholdByDefault`
- **BRIDGE-PULL-028** (inherited) — When `Foreground` is not set on the control, it shall inherit `TextElement.Foreground` from its ancestors.
  - Tests: `PullToRefreshTests.AnUnsetForegroundIsInheritedFromTheAncestorsTextForeground`

## Invariants

- The drawn pull is never negative and stays below `2.2 × Threshold`.
- While a refresh runs, the content rests at `Threshold`.
- At most one pointer drives the pull.

## Edge cases

- A drag that starts sideways, upward, or inside a scrolled-down inner `ScrollViewer` is never a pull for the rest of that press.
- The release compares the last damped pull to `Threshold`, so a pull exactly at the threshold refreshes.
- A `Threshold` change during a pull affects the damping range and the indicator mapping from the next move.
- The ring and arrow geometry, sizes and the 0.8 share are fixed; restyle by `Foreground` only.

## Threading and lifetime

- UI thread only. The Y animatable is created lazily on the track at the first pull. `Refresh` is awaited on the UI thread's synchronization context; the content is held until it completes. While it runs, the control is registered for the engine's `ReducedMotionChanged` signal (BRIDGE-REDUCED).

## Reduced motion

- Read at each release: the content lands at once, the arrow fades at once and the ring holds still while the refresh runs; a change of the flag during the refresh stops or starts the spin at once (BRIDGE-PULL-029), and the return reads the flag again when the refresh ends. The pull itself is unchanged.

## Allocation

- Per pointer move: one `Animatable.Set`; the indicator writes go through `Follow`'s mapped `MotionValue`s. A refresh allocates its animations and is off the steady path.

## Provenance

- No upstream counterpart.

## Decisions

- **`PullToRefresh` is its own control, not a restyled `RefreshContainer`.** Reason: `RefreshContainer`'s pull comes from a `PullGestureRecognizer` that takes touch and pen only, and its visualizer turns a fixed icon through its own composition animation, so neither the mouse pull, the rubber band nor the arrow and ring could be Presto's without replacing both. Rejected: restyling `RefreshContainer`.
- **The control handles the pointer itself and damps the travel, not the drawn value.** As `Sheet` does, rather than through a `Draggable`; the pull is `r(1 − e^(−d / r))` of the pointer's travel with `r = 2.2 × Threshold`. Reason: a release then springs from where the content is drawn and lands exactly on the threshold, sidestepping the `Draggable` axis modifier that is re-applied to its own output on release (it stays documented as having to be idempotent). Rejected: a `Draggable`; fixing the modifier re-application.
- **The work is an awaited `Func<Task> Refresh`.** The content is held at the threshold until the task ends. Reason: the control needs to know when the work is done. Rejected: an `ICommand`, which cannot be awaited.
- **A faulted `Refresh` is logged, not rethrown.** A faulted or canceled task is logged through Avalonia's `Logger` and the control settles as on success. Reason: the refresh runs in an `async void` continuation with no caller to catch it, so a rethrow would crash the app from the synchronization context. Rejected: rethrowing.

## Non-goals

- Pulling from the bottom or sideways, a templated look, or an `ICommand` for the work (it cannot be awaited).
- Restyling Avalonia's `RefreshContainer`.

## Open items

None.
