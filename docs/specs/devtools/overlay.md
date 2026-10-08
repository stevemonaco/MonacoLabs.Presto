---
id: DEVTOOLS-OVERLAY
title: Presto DevTools overlay
package: Monaco.Presto.AvaloniaBridge.DevTools
namespaces:
  - Monaco.Presto.AvaloniaBridge.DevTools
sources:
  - Monaco.Presto.AvaloniaBridge.DevTools/PrestoDevTools.cs
  - Monaco.Presto.AvaloniaBridge.DevTools/DevToolsOverlay.cs
  - Monaco.Presto.AvaloniaBridge.DevTools/TransportBar.cs
  - Monaco.Presto.AvaloniaBridge.DevTools/TickableList.cs
  - Monaco.Presto.AvaloniaBridge.DevTools/TickableRow.cs
  - Monaco.Presto.AvaloniaBridge.DevTools/TickableState.cs
  - Monaco.Presto.AvaloniaBridge.DevTools/RootInspector.cs
  - Monaco.Presto.AvaloniaBridge.DevTools/ScrubTrack.cs
  - Monaco.Presto.AvaloniaBridge.DevTools/TweenTable.cs
  - Monaco.Presto.AvaloniaBridge.DevTools/EaseThumbnail.cs
  - Monaco.Presto.AvaloniaBridge.DevTools/Sparkline.cs
  - Monaco.Presto.AvaloniaBridge.DevTools/SpringsPanel.cs
  - Monaco.Presto.AvaloniaBridge.DevTools/TargetHighlighter.cs
  - Monaco.Presto.AvaloniaBridge.DevTools/PickMode.cs
types:
  - PrestoDevTools
tests:
  - DevToolsShellTests
  - DevToolsListTests
  - DevToolsInspectorTests
  - DevToolsHighlightTests
  - DevToolsPageTests
upstream: []
depends:
  - CORE-DIAGNOSTICS
  - CORE-ENGINE
  - CORE-TIMER
  - CORE-TIMELINE
  - CORE-EASES
  - BRIDGE-HELPERS
---

# Presto DevTools overlay

## Purpose

An in-app inspector for what `Presto.Engine` runs, in its own package so a release app does not carry it. It docks a panel into a window's overlay layer with a transport bar (freeze, step, time dilation, frame stats), a tree of live and recently ended roots, an inspector with scrubbing and playback controls for the selected root, a tween table, target outlines and pick mode, and a springs panel. It reads everything through the core's diagnostics registry and introspection surface (CORE-DIAGNOSTICS) and the engine's time base (CORE-ENGINE).

## Requirements

### Hosting

- **DEVTOOLS-OVERLAY-001** — `Open` shall add one overlay to the top level's `OverlayLayer`, and do nothing when the overlay is already open there or the top level has no overlay layer.
  - Tests: `DevToolsShellTests.ToggleAddsAndRemovesTheOverlayAndDiagnostics`
- **DEVTOOLS-OVERLAY-002** — When the first overlay opens, `Presto.Engine.Diagnostics.Enabled` shall be set to true.
  - Tests: `DevToolsShellTests.ToggleAddsAndRemovesTheOverlayAndDiagnostics`
- **DEVTOOLS-OVERLAY-003** — When the last open overlay closes, the overlay shall disable the engine's diagnostics, thaw the engine and set `TimeDilation` back to 1; while another top level still has one open, closing shall leave them as they are.
  - Tests: `DevToolsShellTests.ToggleAddsAndRemovesTheOverlayAndDiagnostics`, `DevToolsShellTests.ClosingWhileFrozenAndDilatedRestoresTime`
- **DEVTOOLS-OVERLAY-004** — `Toggle` shall close the top level's overlay when it is open and open it otherwise; `Close` on a top level without one shall do nothing.
  - Tests: `DevToolsShellTests.ToggleAddsAndRemovesTheOverlayAndDiagnostics`
- **DEVTOOLS-OVERLAY-005** — `Attach` shall toggle the overlay whenever the gesture is pressed in the top level, handling the key event in the tunnel phase; the default gesture is Ctrl+Shift+F12, because F12 belongs to Avalonia DevTools.
  - Tests: `DevToolsShellTests.TheAttachedGestureTogglesTheOverlay`
- **DEVTOOLS-OVERLAY-006** (inherited) — When `Attach` is called again for a top level already attached, it shall do nothing, keeping the first gesture.
  - Tests: untested
- **DEVTOOLS-OVERLAY-007** (inherited) — `Detach` shall close the top level's overlay and stop listening for its gesture.
  - Tests: `DevToolsShellTests.TheAttachedGestureTogglesTheOverlay`
- **DEVTOOLS-OVERLAY-008** — `UsePrestoDevTools` shall, after setup, call `Attach` with the given gesture on every `TopLevel` as it loads.
  - Tests: untested
- **DEVTOOLS-OVERLAY-009** — The gesture shall work when pressed by hand on the Desktop head.
  - Tests: manual — press Ctrl+Shift+F12 in `samples/PrestoSample.Desktop`; DevTools input cannot send modifier chords (Phase 48 open check).

### Panel

- **DEVTOOLS-OVERLAY-010** — The overlay shall fill its layer, dock its panel to the bottom at 260 px tall, resizable by dragging its top edge between 80 px and the layer's height, and follow the layer's size.
  - Tests: untested
- **DEVTOOLS-OVERLAY-011** — When Collapse is checked, the overlay shall hide everything below the transport bar and the resize grip, size the panel to its content, remove target outlines, and stop refreshing the hidden parts; unchecking shall restore the last height.
  - Tests: untested
- **DEVTOOLS-OVERLAY-012** (inherited) — The Close button shall close the overlay as `Close` does.
  - Tests: untested
- **DEVTOOLS-OVERLAY-013** — The overlay shall refresh once per frame from `TopLevel.RequestAnimationFrame` and run no Presto animations, so it stays responsive while the engine is frozen and never lists itself.
  - Tests: `DevToolsShellTests.TheOverlayRespondsToInputWhileFrozen`
- **DEVTOOLS-OVERLAY-014** (inherited) — The overlay shall use a light and a dark palette for its background and border, following the theme variant.
  - Tests: untested

### Transport bar

- **DEVTOOLS-OVERLAY-015** — The Freeze toggle shall freeze the engine when it is thawed and thaw it when it is frozen, and show the engine's `Frozen` state.
  - Tests: `DevToolsShellTests.FreezeHoldsTimeAndStepMovesARunningTimerByOneFrame`, `DevToolsShellTests.TheOverlayRespondsToInputWhileFrozen`
- **DEVTOOLS-OVERLAY-016** — The Step button shall freeze the engine and advance it by one 1000/60 ms frame per click, repeating every 1000/60 ms after being held for 400 ms.
  - Tests: `DevToolsShellTests.FreezeHoldsTimeAndStepMovesARunningTimerByOneFrame`
- **DEVTOOLS-OVERLAY-017** — The dilation presets 0.1×, 0.25×, 0.5×, 1× and 2× shall set `Engine.TimeDilation`, and the preset equal to the current dilation shall show checked.
  - Tests: `DevToolsShellTests.ADilationPresetSetsTimeDilation`
- **DEVTOOLS-OVERLAY-018** — The bar shall show `Engine.Speed` read-only, so an app-set rate is not mistaken for the inspector's dilation.
  - Tests: untested
- **DEVTOOLS-OVERLAY-019** — When the engine has run a frame since the last refresh, the bar shall push its update time to a frame-time sparkline and show the frame's roots ticked, tweens rendered, springs stepped and update milliseconds.
  - Tests: untested
- **DEVTOOLS-OVERLAY-020** (inherited) — While the engine is hidden and `PauseOnHidden` is on, the bar shall show "hidden · paused".
  - Tests: untested

### Tickable list

- **DEVTOOLS-OVERLAY-021** — The list shall show every registered root that has not completed or been canceled, running, paused and unplayed alike, followed by the recently ended roots newest first, dimmed.
  - Tests: `DevToolsListTests.ListsRunningPausedAndUnplayedAnimations`, `DevToolsListTests.ACompletedRootMovesToTheDimmedRecentlyEndedSection`
- **DEVTOOLS-OVERLAY-022** — A root row shall show a kind glyph, the timer's id, its origin and scope chips when present, a target summary, the distinct animated property names, its state, a progress bar and `iteration/count` (∞ for an infinite loop).
  - Tests: `DevToolsListTests.ListsRunningPausedAndUnplayedAnimations`
- **DEVTOOLS-OVERLAY-023** — A target shall be summarized by its `Name`, else as `Type[index]` within its parent panel, else by its type name; a timer with more than one target shall show "N targets".
  - Tests: `DevToolsListTests.ListsRunningPausedAndUnplayedAnimations`
- **DEVTOOLS-OVERLAY-024** — A root's state shall be completed, canceled, detached (paused because its control left the tree, see BRIDGE-HELPERS), paused, running once it has begun, delay while its current time is negative or it has an unstarted delay, and pending otherwise.
  - Tests: `DevToolsListTests.ListsRunningPausedAndUnplayedAnimations`, `DevToolsListTests.ALoopOnARemovedControlShowsDetachedUntilItIsReattached`, `DevToolsListTests.AUserPausedRowStaysPausedWhenItsControlIsRemoved`
- **DEVTOOLS-OVERLAY-025** (inherited) — A timeline child row shall show its parent's state whenever the parent is not running.
  - Tests: untested
- **DEVTOOLS-OVERLAY-026** — When a row that has children or tweens is expanded, the list shall show its child timers, each followed by its own expanded content, and then its tweens, indented one level deeper.
  - Tests: `DevToolsListTests.ATimelineExpandsToItsChildrenAndTheirTweens`
- **DEVTOOLS-OVERLAY-027** — A tween row shall show the property, its target, from → current → to values and its progress through its own span.
  - Tests: `DevToolsListTests.ATimelineExpandsToItsChildrenAndTheirTweens`
- **DEVTOOLS-OVERLAY-028** — Displayed values shall round any number with four or more decimals to at most three.
  - Tests: `DevToolsInspectorTests.DisplayedValuesKeepAtMostThreeDecimals`
- **DEVTOOLS-OVERLAY-029** — The list shall keep one row object per timer or tween across refreshes, updated in place, and keep the selection while its row is still listed.
  - Tests: `DevToolsListTests.RefreshReusesRowObjects`
- **DEVTOOLS-OVERLAY-030** — While "Hide bridge-internal" is checked (the default), roots with an `Origin` shall be hidden.
  - Tests: `DevToolsListTests.BridgeOriginsAreHiddenUntilTheFilterIsOff`
- **DEVTOOLS-OVERLAY-031** — The state filter shall show only roots in the chosen state; "All states" shows every state.
  - Tests: untested
- **DEVTOOLS-OVERLAY-032** — The search box shall show only roots whose id, target summary, scope or property names contain the text, ignoring case.
  - Tests: `DevToolsListTests.TheSearchFiltersByPropertyName`
- **DEVTOOLS-OVERLAY-033** — "Capture traces" shall set `PrestoDiagnostics.CaptureCreationTrace`, so roots created from then on record their creation stack.
  - Tests: untested
- **DEVTOOLS-OVERLAY-057** — A root shall show detached only while its pause came from its control leaving the tree, returning to running on reattach; a root the user paused before its control was removed shall stay paused.
  - Tests: `DevToolsListTests.ALoopOnARemovedControlShowsDetachedUntilItIsReattached`, `DevToolsListTests.AUserPausedRowStaysPausedWhenItsControlIsRemoved`

### Inspector

- **DEVTOOLS-OVERLAY-034** — The inspector shall act on the root of the selection: selecting a timeline child or a tween selects its root for the controls and highlights the child's or tween's span on the root's track; the inspector is hidden with no selection.
  - Tests: `DevToolsInspectorTests.SelectingATimelineChildSeeksOnlyTheRoot`
- **DEVTOOLS-OVERLAY-035** (inherited) — The inspector shall show `#id current / duration ms` for the root, with ∞ for an infinitely looping root.
  - Tests: untested
- **DEVTOOLS-OVERLAY-036** — Play, Pause, Reverse and Restart shall call the root's method of that name.
  - Tests: untested
- **DEVTOOLS-OVERLAY-037** — Enter in the seek box shall seek the root to the invariant-culture number typed; Enter in the speed box shall set the root's `Speed` when the number is positive, and the box shows the root's speed when it is selected.
  - Tests: untested
- **DEVTOOLS-OVERLAY-038** — Revert… shall open a flyout whose confirm button reverts the root, so reverting is never one click from scrubbing.
  - Tests: untested
- **DEVTOOLS-OVERLAY-039** — When the root has a creation trace, the inspector shall show the first stack frame outside the `Monaco.Presto` namespaces as `created at Type.Method (file:line)`, with the whole trace as its tooltip.
  - Tests: untested

### Scrubbing

- **DEVTOOLS-OVERLAY-040** — Pressing the track shall pause the root and seek it to the time under the pointer, dragging shall keep seeking, and releasing shall resume it only if it was playing when the scrub began.
  - Tests: `DevToolsInspectorTests.ScrubbingAPlayingRootSeeksItAndResumesOnRelease`, `DevToolsInspectorTests.ScrubbingAPausedRootLeavesItPaused`
- **DEVTOOLS-OVERLAY-041** — Scrubbing shall seek only the root, never a timeline child on its own.
  - Tests: `DevToolsInspectorTests.SelectingATimelineChildSeeksOnlyTheRoot`
- **DEVTOOLS-OVERLAY-042** — The track shall span the root's delay through its duration, or only the current iteration for an infinitely looping root, and draw the delay, each iteration and loop delay, a timeline's child spans as a Gantt and its labels, the selection's span and the playhead.
  - Tests: untested
- **DEVTOOLS-OVERLAY-043** — Drag-scrubbing the track and hover outlines shall work by hand on the Desktop head.
  - Tests: manual — drag the scrub track and hover list rows in `samples/PrestoSample.Desktop`; DevTools input cannot send drags or hover (Phase 48 open check).

### Reviving ended roots

- **DEVTOOLS-OVERLAY-044** — While the selected root has completed or been canceled and has not been revived, the inspector shall disable its controls, refuse to scrub and show Revive.
  - Tests: `DevToolsInspectorTests.ScrubbingACompletedRootIsBlockedUntilReviveIsConfirmed`
- **DEVTOOLS-OVERLAY-045** — When Revive is clicked and a live root animates the same property of the same target with a newer tween, the inspector shall first warn, naming each such property and root id, and relabel the button "Revive anyway"; the next click shall revive.
  - Tests: `DevToolsInspectorTests.ScrubbingACompletedRootIsBlockedUntilReviveIsConfirmed`
- **DEVTOOLS-OVERLAY-046** — When Revive is clicked with no such conflict, the root shall be revived at once.
  - Tests: untested

### Tween table

- **DEVTOOLS-OVERLAY-047** — The tween table shall list one row per tween of the selected timer and its descendants, each with property, from → current → to, ease name (`custom` when unnamed), an ease curve thumbnail with the current progress, span in root-iteration ms and composition, and highlight the selected tween.
  - Tests: `DevToolsInspectorTests.TheTweenTableShowsLiveValuesAndTheEaseName`
- **DEVTOOLS-OVERLAY-048** — While a tween is overridden, its row shall show "overridden by #id" naming the newer live root, or `#?` when none is found.
  - Tests: `DevToolsInspectorTests.TheOverriddenMarkerNamesTheNewerRoot`

### Target highlighting and pick

- **DEVTOOLS-OVERLAY-049** — The overlay shall outline, in top-level coordinates, the transformed bounds of each visual the hovered row and the selected row animate (a timer's: those of all its tweens), each visual once, the hovered ones first.
  - Tests: `DevToolsHighlightTests.TheOutlineMatchesTheTargetsTransformedBoundsInTopLevelCoordinates`
- **DEVTOOLS-OVERLAY-050** — The highlighter shall sit in the top level's `AdornerLayer` only while there is an outline, and leave it when the pointer leaves the rows, the panel collapses or the overlay closes.
  - Tests: `DevToolsHighlightTests.HoveringARowAddsTheAdornerAndLeavingRemovesIt`
- **DEVTOOLS-OVERLAY-051** — Targets that do not resolve to a visual shall get no outline.
  - Tests: untested
- **DEVTOOLS-OVERLAY-052** — While Pick is on, the next press on an app control outside the overlay shall be consumed, pick that control, and turn Pick off; presses inside the overlay are not consumed.
  - Tests: `DevToolsHighlightTests.PickModeFiltersTheListToTheClickedControlsAnimationsIncludingPausedOnes`
- **DEVTOOLS-OVERLAY-053** — While a control is picked, the list shall show only roots with a tween whose target resolves to that control or a descendant, paused and unplayed roots included, and a clear button naming the pick shall drop it.
  - Tests: `DevToolsHighlightTests.PickModeFiltersTheListToTheClickedControlsAnimationsIncludingPausedOnes`

### Springs panel

- **DEVTOOLS-OVERLAY-054** — The springs panel shall list each moving motion value, labeled "spring" when it follows a spring and "velocity" otherwise, with its value and spring target, and each registered animatable property as `target.property` with its current and target values, each with a speed sparkline.
  - Tests: `DevToolsHighlightTests.TheSpringsPanelListsAnActiveSpring`
- **DEVTOOLS-OVERLAY-055** (inherited) — The springs panel shall drop rows that are no longer active and hide while it has none.
  - Tests: untested
- **DEVTOOLS-OVERLAY-056** — Freeze and dilation shall hold during a Draggable or Transformable drag by hand.
  - Tests: manual — freeze and dilate during a drag in `samples/PrestoSample.Desktop`; headless only so far (Phase 48 open check).

## Invariants

- The overlay only reads `Presto.Engine` and calls public timer and engine methods; it never touches another engine.
- After the last overlay closes, the engine is thawed, undilated and has diagnostics disabled, whatever the overlay did while open.
- Every refresh rebuilds the visible rows from the registry and drops rows not seen in it.

## Edge cases

- Diagnostics discover only what is in the engine list when enabled: a paused or unplayed root, or an `Animatable`, created before the overlay opened never appears.
- Compositor-played transitions appear as rows that go from pending to canceled and ignore freeze and dilation; render-thread Lottie playback and compositor scenes are not listed (see Non-Presto motion is out of scope).
- A root with an `Origin` that the app also wants to inspect needs "Hide bridge-internal" unchecked.
- Reviving lasts for the root last revived: selecting another root and back keeps it revived, but resets the conflict warning.

## Threading and lifetime

- UI thread only, on the overlay's own animation frames. Open overlays and attached gestures are kept per `TopLevel` in weak tables; one static count across top levels decides when diagnostics turn off.
- Closing does not restore a root `Speed` set from the inspector, a root it paused, seeked or reverted, or `CaptureCreationTrace`.

## Reduced motion

- Not affected: the overlay runs no animations, and it shows the engine's roots as they are, reduced or not.

## Allocation

- Not on the Presto tick path: the overlay polls after the engine's frame. Rows are reused, but each refresh formats strings for visible rows and takes snapshots for every tween in the tween table and every expanded tween row; target summaries and property lists are recomputed only when a timer's shape changes.

## Deviations from anime.js

- No upstream counterpart: anime.js 4.5.0 has no inspector.

## Decisions

- **The inspector is purely additive.** Everything DevTools needs is an addition modeled on GSAP's GSDevTools and the Chrome Animations panel; no upstream semantics change and no parity test moves. Reason: anime.js 4.5.0 has no equivalent.
- **DevTools is a separate package.** The overlay is `Monaco.Presto.AvaloniaBridge.DevTools`, its headless tests live in the bridge test project, and the diagnostics it reads stay in the core. Reason: a release app does not carry the overlay.
- **The overlay never observes or freezes itself.** It lives in the window's `OverlayLayer`, with highlights in the `AdornerLayer`, and runs no Presto animations; opening it enables diagnostics, and closing it disables them and thaws and undilates the engine. Reason: freezing the main engine must leave the overlay responsive, and closing must leave nothing slowed.
- **The default toggle is Ctrl+Shift+F12.** It is configurable through `PrestoDevTools.Attach`. Reason: F12 belongs to Avalonia DevTools.
- **Slow motion is dilation; `Speed` is read-only; controls act on roots.** The transport's presets set time dilation and show engine `Speed` read-only; the inspector's controls always act on the root, so a timeline child is selectable and highlighted on its parent's track but never seeked on its own; scrubbing pauses and seeks the root and restores the previous play state on release. Reason: an app-set rate must not be mistaken for the inspector's, and upstream semantics do not cleanly support seeking a timeline child on its own.
- **An ended root needs an explicit revive; revert is not one click away.** A recently ended root can be scrubbed only after Revive, which first warns when it would override a live tween; revert sits behind a confirmation. Reason: `Seek` on an ended timer revives it, re-composing its tweens and possibly overriding a newer animation, and reverting is destructive to app state.
- **Targets map back to visuals through `TargetOwner`.** Bridge-made brushes and composer-owned transforms map to their visual, split-text pieces resolve to themselves, and anything else (plain objects, view models) gets no outline, only a type name; pick mode searches the registry. Reason: highlighting and pick mode need a visual, and the registry includes paused and pending animations, unlike `Presto.GetAnimations`.
- **Non-Presto motion is out of scope.** Avalonia `Transitions` and `Animation`s are not made to obey freeze or dilation; `FramePlayer` and `LottieView.Play`/`PlaySegment` tween on the main engine and obey both, while `LottieView.PlayOnRenderThread` does not. Reason: not recorded.
- **Each engine owns its time base, and dilation is separate from `Speed`.** See CORE-ENGINE.
- **Animations pause when their controls leave the tree.** See BRIDGE-HELPERS.

## Non-goals

- Inspecting or controlling render-thread engines and compositor scenes.
- Avalonia `Transitions`/`Animation`s, which do not obey freeze or dilation (see Non-Presto motion is out of scope).
- Plugging into the `avdt` tool.
- Break on event, loop a range, copy as code and frame history, left for a later version.

## Open items

- Manual: the Ctrl+Shift+F12 gesture, hover outlines, drag-scrubbing the track, and freeze/dilation during a Draggable or Transformable drag have not been driven by hand on Desktop (Phase 48).
- The design asked for a list that refreshes "without allocating", with snapshots only for expanded rows; the tween table snapshots every tween of the selection each frame.
- Closing the overlay leaves `PrestoDiagnostics.CaptureCreationTrace` on if it was checked, and keeps root `Speed` changes made from the inspector.
- No tests for `UsePrestoDevTools`, a repeat `Attach`, collapse and resize, the state filter, the inspector buttons and seek/speed boxes, frame stats, or the springs panel dropping rows.
