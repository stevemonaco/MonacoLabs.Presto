---
id: BRIDGE-SUBMIT
title: Progress ring and submit button
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto
  - MonacoLabs.Presto.Controls
sources:
  - MonacoLabs.Presto.Avalonia/Controls/ProgressRing.cs
  - MonacoLabs.Presto.Avalonia/SubmitButton.cs
  - MonacoLabs.Presto.Avalonia/SubmitState.cs
types:
  - ProgressRing
  - SubmitButton
  - SubmitState
tests:
  - ProgressRingTests
  - SubmitButtonTests
upstream: []
depends:
  - CORE-PRESTO
  - CORE-REDUCED
  - BRIDGE-FOLLOW
  - BRIDGE-SHAPES
  - BRIDGE-CODECS
  - BRIDGE-RECIPES
  - CORE-SPRING
  - BRIDGE-TOKENS
---

# Progress ring and submit button

## Purpose

Two value-and-state prebuilts. `ProgressRing` is a determinate ring, whole or segmented, that draws its `Value` as it is and doubles as a hold-to-confirm control that raises `HoldStarted`, `HoldCanceled` and `Confirmed` and executes its `Command` when full; easing a value onto it is `Follow`'s job (`{presto:Follow}`, `Follow.Bind`, or `Follow.Draw` on a bare shape, all in BRIDGE-FOLLOW). `SubmitButton` is an attached recipe on any `Button` that collapses it to a spinner while work runs, draws a check or cross with `Recipes.DrawIn` (BRIDGE-RECIPES), and springs back to its own values.

## Requirements

### Progress ring: drawing

- **BRIDGE-SUBMIT-001** — The ring shall draw one track arc and one fill arc per segment, the fills over the tracks, stroked `Thickness` wide with round caps in `Track` and `Fill`.
  - Tests: `ProgressRingTests.ItDrawsItsValueAcrossItsSegments`
- **BRIDGE-SUBMIT-002** — The arcs shall run clockwise from the top on a circle of radius `(min(width, height) − Thickness) / 2` centered in the ring's arranged size.
  - Tests: `ProgressRingTests.ItDrawsItsValueAcrossItsSegments`
- **BRIDGE-SUBMIT-003** — With one segment, the arc shall be the whole circle with no gap; with several, each shall span `360° / Segments` less `Gap`, the gap clamped to the segment's span and split evenly at both ends.
  - Tests: `ProgressRingTests.ItDrawsItsValueAcrossItsSegments`
- **BRIDGE-SUBMIT-004** — Segment `i` of `n` shall draw the share `clamp(Value · n − i, 0, 1)` of its arc from its start, written through the shape's `"Draw"` dashes.
  - Tests: `ProgressRingTests.ItDrawsItsValueAcrossItsSegments`
- **BRIDGE-SUBMIT-005** — When `Value` changes, the ring shall redraw at once with no easing of its own.
  - Tests: `ProgressRingTests.AHoldFillsTheRingAndConfirmsAtOneThenRewindsOnRelease`
- **BRIDGE-SUBMIT-006** (inherited) — When `Segments`, `Fill` or `Track` changes, the ring shall rebuild its arcs; when `Gap` or `Thickness` changes, it shall reshape them on the next arrange.
  - Tests: untested
- **BRIDGE-SUBMIT-007** (inherited) — When `Segments` is set to 0 or less, the setter shall throw `ArgumentException`.
  - Tests: untested
- **BRIDGE-SUBMIT-008** (inherited) — The ring shall measure to zero and take the size it is arranged at.
  - Tests: untested
- **BRIDGE-SUBMIT-009** (inherited) — The ring shall be hit anywhere in its bounds, its hole included.
  - Tests: `ProgressRingTests.AHoldFillsTheRingAndConfirmsAtOneThenRewindsOnRelease`

### Progress ring: hold to confirm

- **BRIDGE-SUBMIT-010** (inherited) — While `HoldDuration` is greater than 0, the ring shall be focusable; otherwise it shall not be, and it shall take no holds.
  - Tests: `ProgressRingTests.SpaceHoldsTheRingWhileFocused`
- **BRIDGE-SUBMIT-011** — When pressed with the left button (or touch) while `HoldDuration` is positive, the ring shall capture the pointer, handle the press and animate `Value` linearly to 1 over `HoldDuration × max(0, 1 − Value)`.
  - Tests: `ProgressRingTests.AHoldFillsTheRingAndConfirmsAtOneThenRewindsOnRelease`
- **BRIDGE-SUBMIT-012** — When Space or Enter goes down while the ring is focused, `HoldDuration` is positive and no hold is running, the ring shall start a hold and handle the key; a repeated key-down during a hold shall not restart it.
  - Tests: `ProgressRingTests.SpaceHoldsTheRingWhileFocused`
- **BRIDGE-SUBMIT-013** — When a hold reaches 1, the ring shall raise `Confirmed` once.
  - Tests: `ProgressRingTests.AHoldFillsTheRingAndConfirmsAtOneThenRewindsOnRelease`
- **BRIDGE-SUBMIT-014** — When the hold's pointer capture is lost or Space or Enter goes up, the ring shall stop the hold and rewind `Value` to what it was when the hold began over `Tokens.Short` on `Tokens.Ease`, whether or not it confirmed.
  - Tests: `ProgressRingTests.AHoldFillsTheRingAndConfirmsAtOneThenRewindsOnRelease`, `ProgressRingTests.ReleasingBeforeOneRewindsWithoutConfirming`
- **BRIDGE-SUBMIT-015** (inherited) — When the rewind lands, its animation shall be reverted, so `Value` ends on its own value.
  - Tests: `ProgressRingTests.ReleasingBeforeOneRewindsWithoutConfirming`
- **BRIDGE-SUBMIT-016** — Releasing before the ring is full shall not raise `Confirmed`.
  - Tests: `ProgressRingTests.ReleasingBeforeOneRewindsWithoutConfirming`
- **BRIDGE-SUBMIT-017** — While `Presto.Defaults.ReducedMotion` is true, a hold shall still fill over its full time, and the rewind shall land at once.
  - Tests: `ProgressRingTests.ReleasingBeforeOneRewindsWithoutConfirming`
- **BRIDGE-SUBMIT-043** — When a hold starts, the ring shall raise `HoldStarted` once, before `Value` begins to move.
  - Tests: `ProgressRingTests.AHoldRaisesHoldStartedOnceByPointerAndByKey`
- **BRIDGE-SUBMIT-044** — When a hold stops before `Value` reaches 1, the ring shall raise `HoldCanceled` once as its rewind starts; a hold that raised `Confirmed` shall raise no `HoldCanceled` when let go.
  - Tests: `ProgressRingTests.ReleasingShortRaisesHoldCanceledOnceAndAConfirmedReleaseRaisesNone`
- **BRIDGE-SUBMIT-045** — When a hold reaches 1, the ring shall raise `Confirmed` and then, if `Command.CanExecute(CommandParameter)` is true, execute `Command` with `CommandParameter`.
  - Tests: `ProgressRingTests.AConfirmedHoldExecutesTheCommandAfterConfirmed`
- **BRIDGE-SUBMIT-046** — While `Command` is set and `CanExecute(CommandParameter)` is false, the ring shall be disabled and take no holds; it shall query again on `CanExecuteChanged` and when `Command` or `CommandParameter` changes.
  - Tests: `ProgressRingTests.ACommandThatCannotExecuteDisablesTheRingAndFollowsCanExecuteChanged`

### Submit button: state

- **BRIDGE-SUBMIT-018** — When `State` first changes on a button, the button shall get a layer holding a spinner (a three-quarter arc) and a mark, both 20 × 20, stroked 2.5 DIPs with round caps and joins in the button's `Foreground`, centered and not hit-testable.
  - Tests: `SubmitButtonTests.RunShowsBusyThenTheOutcomeThenSpringsBack`
- **BRIDGE-SUBMIT-019** — The layer shall be appended as a visual and logical child of the button, re-appended at each collapse if a re-template dropped it, and arranged at the button's bounds whenever they change.
  - Tests: untested
- **BRIDGE-SUBMIT-020** — When the state becomes `Busy`, `Succeeded` or `Failed` while the button is not collapsed, the button shall record its width, corner radius and background, animate `Width` from its width to its height and `CornerRadius` to half its height over `Tokens.Medium` on `Tokens.Ease`, and fade its presenter's content to 0 over the shorter of that and `Tokens.Short`.
  - Tests: `SubmitButtonTests.RunShowsBusyThenTheOutcomeThenSpringsBack`
- **BRIDGE-SUBMIT-021** — When the state becomes `Busy`, the mark shall be erased, the spinner shall fade in over `Tokens.Short` and spin with `Recipes.Spinner()` unless it already spins.
  - Tests: `SubmitButtonTests.RunShowsBusyThenTheOutcomeThenSpringsBack`
- **BRIDGE-SUBMIT-022** — When the state leaves `Busy`, the spinner shall pause, fade out over `Tokens.Short` and then have its spin reverted.
  - Tests: `SubmitButtonTests.RunShowsBusyThenTheOutcomeThenSpringsBack`
- **BRIDGE-SUBMIT-023** — When the state becomes `Succeeded` or `Failed`, the mark shall become `Draws.Check` or `Draws.Cross` and draw in from nothing with `Recipes.DrawIn()`.
  - Tests: `SubmitButtonTests.RunShowsBusyThenTheOutcomeThenSpringsBack`
- **BRIDGE-SUBMIT-024** — When the state becomes `Succeeded` with a `SuccessBrush`, or `Failed` with a `FailureBrush`, the button's `Background` shall animate to that brush over `Tokens.Medium`; with no brush, the background shall be left alone.
  - Tests: `SubmitButtonTests.RunShowsBusyThenTheOutcomeThenSpringsBack`
- **BRIDGE-SUBMIT-025** — When the state becomes `Idle` while collapsed, the mark shall erase with `Recipes.DrawOut` over `Tokens.Short`, the content shall fade back to 1 over `Tokens.Medium`, and `Width`, `CornerRadius` and (when tinted) `Background` shall return to their recorded values on `Springs.Gentle`.
  - Tests: `SubmitButtonTests.RunShowsBusyThenTheOutcomeThenSpringsBack`
- **BRIDGE-SUBMIT-026** — When the expand and the content fade land, their animations shall be reverted, so the button ends on its own local or styled width, corner radius, background and content opacity.
  - Tests: `SubmitButtonTests.RunShowsBusyThenTheOutcomeThenSpringsBack`
- **BRIDGE-SUBMIT-027** (inherited) — When the state becomes `Idle` while not collapsed, the button shall not change size.
  - Tests: untested
- **BRIDGE-SUBMIT-028** (inherited) — When the state moves between `Succeeded` and `Failed`, or from either back to `Busy`, the button shall stay collapsed and swap the mark or spinner without re-collapsing.
  - Tests: untested

### Submit button: Run

- **BRIDGE-SUBMIT-029** (inherited) — `Run` shall set `State` to `Busy` with `SetCurrentValue`, so a binding on it survives, and await `work` without rethrowing its fault or cancellation.
  - Tests: `SubmitButtonTests.AFaultedRunFailsWithoutThrowing`
- **BRIDGE-SUBMIT-030** — When `work` ends, `Run` shall set `Succeeded` if it ran to completion and is not a `Task<bool>` returning false, and `Failed` otherwise.
  - Tests: `SubmitButtonTests.RunShowsBusyThenTheOutcomeThenSpringsBack`, `SubmitButtonTests.AFaultedRunFailsWithoutThrowing`
- **BRIDGE-SUBMIT-031** — `Run` shall hold the outcome for `Tokens.Medium + Tokens.Long` of engine time with `Presto.Delay`, then set `Idle` and return whether the work succeeded.
  - Tests: `SubmitButtonTests.RunShowsBusyThenTheOutcomeThenSpringsBack`, `SubmitButtonTests.AFaultedRunFailsWithoutThrowing`
- **BRIDGE-SUBMIT-047** — When `Run` is called on a button while an earlier `Run` on it is in progress, the earlier run shall stop writing `State`, and still await its work, hold and return its own outcome.
  - Tests: `SubmitButtonTests.ASecondRunTakesTheButtonOverAndTheFirstStillReturnsItsOutcome`

### Submit button: reduced motion

- **BRIDGE-SUBMIT-032** — While `Presto.Defaults.ReducedMotion` is true at a state change, the collapse, spinner fade, tint, content fade and expand shall land at once, and the spinner shall not spin.
  - Tests: `SubmitButtonTests.ReducedStatesSwapWithoutMotion`
- **BRIDGE-SUBMIT-033** — While `Presto.Defaults.ReducedMotion` is true, the mark shall be drawn in on its first frame, as `Recipes.DrawIn` does under the flag.
  - Tests: `SubmitButtonTests.ReducedStatesSwapWithoutMotion`

### Defaults

- **BRIDGE-SUBMIT-034** (inherited) — When `Value` is not set, the ring shall draw no fill.
  - Tests: untested
- **BRIDGE-SUBMIT-035** (inherited) — When `Segments` is not set, the ring shall draw one whole arc.
  - Tests: untested
- **BRIDGE-SUBMIT-036** (inherited) — When `Gap` is not set, the gap between segments shall be 6°.
  - Tests: untested
- **BRIDGE-SUBMIT-037** (inherited) — When `Thickness` is not set, the arcs shall be stroked 4 DIPs wide.
  - Tests: untested
- **BRIDGE-SUBMIT-038** (inherited) — When `Fill` is not set, the fill arcs shall be opaque white.
  - Tests: untested
- **BRIDGE-SUBMIT-039** (inherited) — When `Track` is not set, the track arcs shall be white at 40 % opacity (`#66FFFFFF`).
  - Tests: untested
- **BRIDGE-SUBMIT-040** (inherited) — When `HoldDuration` is not set, it shall be 0 and the ring shall take no holds.
  - Tests: untested
- **BRIDGE-SUBMIT-048** — When `Command` and `CommandParameter` are not set, they shall be null and the ring shall take holds as it did without them.
  - Tests: untested
- **BRIDGE-SUBMIT-041** (inherited) — When `SubmitButton.State` is not set, the button shall be `Idle`.
  - Tests: untested
- **BRIDGE-SUBMIT-042** (inherited) — When a binding to `SubmitButton.State` names no mode, it shall bind two-way.
  - Tests: untested

## Invariants

- A ring's fills never draw more than their arcs, whatever `Value` is.
- A submit button that returns to `Idle` from a collapse ends on its own values, not on recorded copies of them.
- One submit layer per button, created on the first state change.

## Edge cases

- `Value` outside 0–1 is not clamped as a property; each segment's share is.
- A hold started at `Value` 1 or above takes 0 ms and confirms at once.
- A key-up of Space or Enter with no hold running does nothing; a pointer press during a key hold starts a new hold from the current value.
- The width a collapse records is the button's rendered width, so a button mid-layout-animation expands back towards that frame's width before reverting.
- A faulted `work` passed to `Run` stays unobserved unless the caller observes it.

## Threading and lifetime

- UI thread only. The ring's hold and rewind are bridge animations on the ring's `Value`; the ring subscribes to its `Command`'s `CanExecuteChanged` only while attached to the visual tree; the submit button's parts and its latest run's token live in private attached properties on the button for its lifetime. `Run`'s continuations resume on the caller's synchronization context.

## Reduced motion

- Ring: the drawing is immediate anyway; a hold keeps its full time (the wait is the confirmation), the rewind lands at once. Easing a bound value is `Follow`'s and snaps there (BRIDGE-FOLLOW).
- Submit button: read at each state change, every state shows at once and the spinner stands still. `Run`'s outcome hold keeps its length.

## Allocation

- The ring redraws by writing dash arrays on `Value` changes; its geometry is rebuilt only when the arranged size, `Segments`, `Gap` or `Thickness` change. The submit button allocates its animations per state change, off the tick path.

## Provenance

- No upstream counterpart.

## Decisions

- **The ring draws its value as it is.** `ProgressRing` has no easing of its own; easing comes from `{presto:Follow}`, `Follow.Bind` or `Follow.Draw` (BRIDGE-FOLLOW). Reason: those already snap under reduced motion and draw a `MotionValue` unsmoothed, while easing every change in the ring would lag a value driven each frame.
- **A hold keeps its time under reduced motion.** Reason: the hold's wait is the confirmation.
- **The submit button is an attached form.** `SubmitButton.State` (Idle/Busy/Succeeded/Failed) on any `Button`, set by a view model or by `SubmitButton.Run(button, work)`, with the spinner and mark in a layer appended to the button's visual and logical children and arranged at its bounds. Reason: a view model binds the state, a handler awaits `Run`, and the button keeps its own template and content, so no content needs a part. Rejected: a templated control or a builder chain.
- **The outcome is drawn in, not morphed.** A check or a cross is drawn with `Recipes.DrawIn` after the spinner fades. Reason: the spinner's arc stops at whatever angle the wait left it, and a cross is two strokes. Rejected: a `MorphTo` of the spinner's arc.
- **The expand reverts when it lands.** Collapsing and expanding animate `Width`, `CornerRadius` and, with a success or failure brush, `Background`, and the expand's animations are reverted on landing. Reason: the button ends on its own local or styled values.
- **The expand does not overshoot.** It lands on `Springs.Gentle`. Reason: `Width` is a layout property, so an overshooting spring re-centers the content each frame through layout rounding and the label visibly wobbles by a pixel as the spring settles. Rejected: `Springs.Bouncy`, and keeping the bounce with layout rounding turned off during the expand.
- **Run reports success unless the work failed.** The outcome is success unless the task faulted, was canceled or returned false, and it is shown for `Tokens.Medium + Tokens.Long`. Reason: not recorded.
- **The ring executes `Command` after raising `Confirmed`.** Same order as `Button`, which raises `Click` before its command. Reason: a handler that animates the confirm runs before the view model reacts to it. Rejected: a command that replaces the event.
- **A command that cannot execute disables the ring.** While `Command.CanExecute(CommandParameter)` is false, the ring's `IsEffectivelyEnabled` is false, so it takes no presses, keys or focus; it re-queries on `CanExecuteChanged`, subscribed only while attached. Reason: Avalonia's `Button` behaves this way, and a hold that fills to a confirmation that does nothing reads as broken. Rejected: holding normally and skipping the command at 1.
- **`HoldStarted` and `HoldCanceled` are plain CLR events, like `Confirmed`.** Reason: all three describe one control's hold and are handled on that control; a routed event would bubble to ancestors that have no reason to handle it. Rejected: routed events, and an `IsHolding` property (a "was pressed since it opened" check is a latch over an event, not a state).
- **`HoldCanceled` fires only for a hold that ends short.** It is raised as the rewind starts, before `Value` moves back. Reason: an early release is the moment to react, and a release after `Confirmed` is not a cancel. Rejected: a `HoldEnded` with a confirmed flag, which every handler would have to filter.
- **The latest `Run` owns the button.** Each run takes a token, and a run whose token is stale writes no `State`; it still awaits its work, holds for its outcome and returns its own result. Reason: the work has already started when `Run` is called, so refusing the second run would lose its outcome, and with no guard the first run's late `Idle` collapses the second's spinner. Rejected: throwing `InvalidOperationException` (the caller's task is already running); queueing the second run (the button would show a stale outcome first).
- **The outcome hold runs on engine time.** `Run` waits with `Presto.Delay`, while `work` keeps whatever clock the caller gave it. Reason: the hold is a beat between the mark drawing in and the expand, so it scales with the animations around it under the engine's speed or a DevTools slow-motion. Rejected: `Task.Delay`, which would leave the hold at full length while the animations slow.

## Non-goals

- An indeterminate ring (use `Recipes.Spinner` on a shape), a start offset other than the top, or counter-clockwise drawing.
- Easing `Value` inside the ring (see Decisions: the ring draws its value as it is).
- A templated submit control or a morph from the spinner arc into the mark (see Decisions).

## Open items

- Rebuild and reshape on `Segments`/`Fill`/`Track`/`Gap`/`Thickness` changes (006), the `Segments` validation throw (007), zero measure (008), layer re-hosting (019), idle while not collapsed (027) and state moves without re-collapse (028) have no tests.
