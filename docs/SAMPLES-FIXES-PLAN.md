# Monaco.Presto — Sample regressions and fixes plan

Four problems found on a Desktop pass after the library-findings work ([LIBRARY-FINDINGS-PLAN.md](LIBRARY-FINDINGS-PLAN.md),
commit `9bafac0`, plus the uncommitted sample clean-up that followed it). `83b942c` is the last commit before any of it
and is the baseline for "was this already broken?": check it out into a git worktree under the scratchpad
(`git worktree add <scratch>/baseline 83b942c`) rather than switching this branch. Remove the worktree when done.

Every fix starts with a headless test in `tests/PrestoSample.Tests` (or the bridge test project for a bridge fix) that
**fails on the current code** before the fix. A test that passes before the fix does not reproduce the bug; rework it.
The Rider XAML previewer may lock `samples/PrestoSample.Desktop/bin` and the test `bin` folders. Do not kill it. Build
and test with `--artifacts-path <scratchpad>/artifacts`, and build the Desktop head into the scratchpad for any visual check.

## 1. Board: a dropped card jumps back to its pre-drag slot, then slides in

**Symptom (Desktop).** The live drag is fine. When the card settles on release, it is drawn for a moment at the
position it was lifted from, then slides from there into its new slot.

**Cause.** At `9bafac0`, row 12 changed `LayoutTransition.Measure` to read the layout slot and ignore the render
transform. `BoardPage.Land` re-arms the card with `AnimateLayout` one line before the reparent, meaning to start the
move "where the card is drawn". The measured `first` is now the slot it was lifted out of, because the drag offset is a
render transform. After the reparent and `drag.Reset()`, the FLIP inverts from that old slot.

**Why the test missed it.** `BoardPageTests.ADroppedCardIsDrawnWhereItWasLetGoUntilItSettles` holds the pointer still
for 8 frames. The release spring settles and the card lands while the button is still down, so frames are sampled only
after the drop has finished. Rework it so the release happens while the card is still moving and the settle happens
after `MouseUp`, as it does by hand. Sample the drawn box every frame from `MouseUp` until 30 frames after the reparent.
Assert that no frame is drawn near the pre-drag slot and that the path runs from the release point to the new slot.
It must fail on the current code.

**Fix (preferred).**
- `LayoutTransition.MeasureDrawn`, the `first` of `AnimateSharedElement`, should include the source's render-transform
  translation, since "where the source is drawn" is what a shared element means.
- `BoardPage.Land` then arms with `PrestoAvalonia.AnimateSharedElement(card, card, ReflowDuration, Eases.OutQuint, <the
  mode AnimateLayout defaults to>, once: false)` in place of `Arm(card)`. The card keeps a live transition for later reflows.
- Add a bridge test for the `MeasureDrawn` change.
- Check that the other `AnimateSharedElement` callers (the Layout reference page) still behave.
- If this route is unworkable, pick the smallest bridge change that lets a transition be armed from the drawn box, and
  say why in the report.

Keep the grab-time revert only if it is still needed after the fix. Update its comment and SAMPLES-PLAN §8 rows 12 and
13 to match what is true afterwards.

## 2. Title sequence: "PRESTO" arrives jumpy

**Symptom (Desktop).** The per-character arrival of the wordmark looks uneven or jumpy. It may predate the recent work.

**Work.**
1. Run the baseline worktree's Desktop sample and the current one, and compare the title build (DevTools screenshots
   across the `title` label, or a headless frame-by-frame capture of each char's drawn Y and opacity).
2. If it is a regression, find the change that caused it and fix that. Leads, in order:
   - `TransportBar` now drives the scrubber from the timer's own `OnUpdate`. Check that a programmatic scrubber update
     does not feed back into `Seek` (the `ValueChanged` path) on every frame.
   - The `Loop` / `IterationProgress` handling in the bar.
   - The row 25 `ClipToBounds` override in `TextSplitter`, which could trigger a re-layout or re-split.
   - The row 24 `Opacity` clamp and the row 27 typed binding.
3. If it predates the recent work, look for the cause in the page itself (stagger, ease, per-char durations, a re-split
   rebuilding the timeline mid-play). Fix it if the fix is clear; otherwise report what was measured and leave the page alone.

Add a regression test for whatever cause is found. For example, the scrubber never calls `Seek` while the timer is
driving it, or char Y is monotonic through the arrival.

## 3. Loaders: add the 2 s UI-thread freeze

The Compositor reference, the Layout reference and the Particle field have a button that freezes the UI thread for 2 s
(`Dispatcher.UIThread.Post(() => Thread.Sleep(2000), DispatcherPriority.Background)`). Add the same to `LoadersPage`,
using the same button text and placement as the Particle field. Disable it on Browser, as the other pages do if they do.

A freeze only means something if something survives it. Add a "render thread" toggle to the rail. When it is on, rebuild
through `AnimateOnCompositor` every loader whose properties are all composition tracks (X, Y, Scale, ScaleX, ScaleY,
Rotate, Opacity). When it is off, the loaders stay as they are now. The `*Code` strings must keep matching the calls that
run: either show the compositor variant's call when the toggle is on, or keep the displayed code for the UI-thread variant
and say so in the caption. Loaders with no composition counterpart (`Data` morph, `Draw`, colours) stay on the UI thread.
The caption should say which loaders keep moving through a freeze. Add a smoke test that toggles the mode back and forth
and detaches cleanly.

## 4. Reference pages: an animated button snaps home for a frame on press and release

**Symptom (Desktop).** On the Controls, Callbacks, Keyframes, Value types, Animation basics and Playback settings
reference pages, clicking the animated "Click me" button (or its equivalent) while it is mid-animation draws it at its
un-animated position for about one frame on pointer press, and again on release.

**Likely cause (verify).** Fluent's `Button` `:pressed` style sets `RenderTransform` (a small scale), possibly with a
`TransformOperationsTransition`. `RenderTransformComposer` captures the visual's existing transform once, at construction.
It then holds its `TransformGroup` at `BindingPriority.Animation`. A style or transition changing `RenderTransform` while
the composer owns it either displaces the group for a frame or leaves the captured `_existing` stale.

**Work.**
1. Check against the baseline worktree whether this is a regression. The row 27 typed binding and the row 12 `Animates`
   change are the suspects.
2. Reproduce headlessly in the bridge tests: a `Button` animating `X`, pressed and released mid-animation with real
   pointer input, with the drawn position checked every frame. The test must fail before the fix.
3. Fix it in the bridge, not per page. The composer should keep drawing the animated shorthands through a style-driven
   `RenderTransform` change on press and release. It should compose with the new style value (the pressed scale) rather
   than drop it, and releasing the last shorthand should uncover whatever the style value is at that moment.
4. Step through the six reference pages listed above.

## Verification

- `dotnet test` for `tests/Monaco.Presto.Tests`, `tests/Monaco.Presto.AvaloniaBridge.Tests` and `tests/PrestoSample.Tests`
  with `--artifacts-path`, all green.
- Desktop visual pass (Desktop head built into the scratchpad): drop a card on the Board with a gentle drag and with a
  flick; watch the Title sequence build; freeze the Loaders page with the render-thread toggle on and off; click the
  animated button mid-flight on each of the six reference pages.
- SAMPLES-PLAN §8: update rows 12, 13 and 27 for anything that changed, and add a row for any new library gap found.
