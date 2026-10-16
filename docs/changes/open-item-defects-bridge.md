# Open-item defects: bridge and DevTools

## Why

The bridge and DevTools counterpart of `open-item-defects-core.md`: the defects, silent failures, late validation and stale text listed in the specs' Open items, fixed before the untested requirements are covered.

## What

Each defect below is fixed in code, or recorded as a decision when the behavior is kept. Each fix updates its spec in the same task: the requirement, its `Tests:` line (headless tests in `tests/MonacoLabs.Presto.Avalonia.Tests`), a Decisions entry when a choice was made, the XML docs it touches, and the Open items line deleted. Untested-only open items stay for the later test-coverage batches.

Out of scope, left in Open items or the backlog: feature requests (`MorphText` shard count, an all-at-once `WriteIn`, re-seekable `WriteIn`/`Shatter`, shared warp fields and per-letter warps, multi-color formations, XAML settings for `Lift`'s distance and `Glow`'s color, a touch `DragThreshold`), visual-quality items that need eyes on Desktop (`Turbulence` fraying, `MorphGlyphs` specks at body size), the `FilteredImage` resize-timing question, and every manual or platform check.

## Decisions

- **Validate where a setting is set, not where it is first used.** `Connected.Motion`, `Selection.Motion` and `FlipCard.Motion` validate when assigned and throw there; a `Motion` state with a rest-less property throws when the state is set; `OnScroll` throws for a missing target or `ScrollViewer` when it resolves, not from a tick. Reason: an exception from an input handler or an engine tick points nowhere near the mistake. This matches `Animate.Layout`.
- **Settings without a meaning are rejected, not dropped.** `RunOnCompositor` throws `NotSupportedException` for any setting with no compositor meaning (such as `WhenDetached`), not just the listed ones; `Selection.Motion` rejects `X`/`Y`/`Scale`; `FlipCard.Motion` accepts timing and ease only, like `Selection.Motion`; a shorthand string that is not a number throws instead of releasing the followed layer; `ImageTransition.Parse` rejects numeric and undefined kinds; an extension with no exit form set on `Presence.Exit` throws; `OnTapped` on a visual that is not an `InputElement` throws.
- **One owner at a time.** A `CompositorScene` already attached to another canvas throws on a second attach, and a detached scene's `Now`, `Size` and `Invalidate` throw `InvalidOperationException`; `Animate.Layout` and `ItemLayout` on one container throws on the second arm.
- **A replaced or reopened surface follows its latest input.** `Modal.Open` while open takes the new origin for its close; a `Backdrop` replaced while open is unblurred; changing one lifecycle builder while others stay set restarts the arrival with the new set; `OnTapped`/`OnChanged` pick up a swapped builder at once, as `OnAttached` does.
- **Kept and recorded:** `Draggable.Reset` writes 0 unclamped (reset means the origin, as upstream); a `Transformable` capture loss runs a full release (as a `Draggable`'s does, BRIDGE-DRAGGABLE edge cases); a `Source` change mid-transition retargets the running transition (smooth for rapid changes); a layout transition with an off-screen source degrades to a one-shot `AnimateLayout` (it still animates; documented on the member); a local `RenderTransform` set while the group is held is not picked up (documented); a `Distort`ed child is hit-tested over the control's bounds; the scroll-observer snap waits on stillness without a velocity check; the selection indicator's clip check is the far corner with 0.5 DIP tolerance.
- **A faulted `Refresh` is logged, not rethrown.** `PullToRefresh` catches a faulted or canceled `Refresh` task, logs it through Avalonia's `Logger` and settles. Reason: an `async void` rethrow crashes the app from the synchronization context with no caller to catch it.
- **The toast host owns its items.** Changing `Items` directly or setting `ItemsSource` throws `InvalidOperationException`; a re-prepared container detaches its handlers before attaching again.

## Spec changes

- **BRIDGE-THREAD:** the rejection rule; `Cancel` releases the `Visual.Opacity` hold.
- **BRIDGE-SCENES:** the double-attach throw and detached-call throws.
- **BRIDGE-CONNECTED:** validation on set; the registry holds the last flight's control weakly.
- **BRIDGE-LAYOUT:** `Then()` before the first move waits for the flight; the off-screen decision.
- **BRIDGE-DECLLAYOUT:** two arms on one container throw.
- **BRIDGE-FLIP:** `Motion` validation; an `Orientation` change while flipped resets the old axis to 0° and turns the new one to 180°.
- **BRIDGE-SELECTION:** validation on set, `X`/`Y`/`Scale` rejected, the `Motion` doc reading `Recipes.Tokens`, the clip-check decision.
- **BRIDGE-MOTION:** `Leave` accepts a per-property `Delay` (the tracker already reads it; validation and the doc agree); a return to a lower looping or keyframed state resumes its loop or keyframes; a function-valued `Delay` survives orchestration's added delay; the rest-less property error at set time; `Motion.Add` does nothing in design mode; record the synthesized hover source.
- **BRIDGE-PRESENCE / BRIDGE-PRESENCE-EXIT:** the partial-builder restart; `AnimateExit` does not ghost a control that is hidden or at opacity 0 (itself or an ancestor), as a departure does; fix the decision's test name (`UnderSyncTheEnterPlaysWhileTheGhostExits`).
- **BRIDGE-POPOVER:** builders on a plain `Popup`'s child start a run, as the presenter fallback documents.
- **BRIDGE-MARKUP:** the exit-form throw.
- **BRIDGE-MODAL:** new origin on re-open; backdrop unblur on replace.
- **BRIDGE-TOAST:** the ownership rule and re-prepare.
- **BRIDGE-PULL:** the faulted-refresh decision and XML docs.
- **BRIDGE-FOLLOW:** a never-attached target snaps its values like a detached one; the non-numeric shorthand throw; record that `FollowExtension.Ease` is a parsed `string`; delete the "timer source" item (it is `Timer.ProgressValue`).
- **BRIDGE-ATTACH:** builder swaps apply at once; `OnTapped` on a non-`InputElement` throws.
- **BRIDGE-TRANSFORMABLE:** a horizontal-only wheel is left unhandled under a zooming mode; the capture-loss decision.
- **BRIDGE-SCROLLOBSERVER:** the missing-target error at resolution; the snap decision.
- **BRIDGE-REORDER:** a refused or canceled step raises `Moving` once per slot crossing, not on every move or auto-scroll frame; the `_raised =container` spacing; the plan's test name; record the once-per-grab `Logger` warning.
- **BRIDGE-DISTORT:** a static `Distort` stops requesting frames once nothing changes; the hit-test decision.
- **BRIDGE-PARTICLES:** a formation over `MaxLive` thins its points evenly instead of dropping the last ones; record that `ParticleShape.Bitmap` takes a `Bitmap`.
- **BRIDGE-IMAGE:** `Parse` rejection; the retarget decision.
- **BRIDGE-RECIPES:** `PageSharedAxis` takes a duration like `PageFade`; `Develop` in the UI-thread-only list.
- **BRIDGE-RECIPES-MEDIA:** the reduced check runs before the window check; fix the text.
- **DEVTOOLS-OVERLAY:** closing the overlay restores `CaptureCreationTrace` and the root speeds it changed; the tween table snapshots only expanded rows.
- **Doc-only fixes:** BRIDGE-ACCORDION (BRIDGE-CODECS references this spec for `GridLength`), BRIDGE-CONTENTSIZE and BRIDGE-INIT (delete items whose decisions already follow the code), BRIDGE-SHEET (`Drawer` docs: any `Edge`), BRIDGE-FRAMES (in-place `Frames` mutation is not observed; documented), BRIDGE-SWIPE (a later `Command` widens the range; documented), BRIDGE-REDUCED (the follower writes the flag of the engine current at creation; documented in the spec and README), BRIDGE-TRANSFORMS (the held-group decision), BRIDGE-DRAGGABLE (the `Reset` decision).

## Tasks

1. **Render thread and scenes.** BRIDGE-THREAD and BRIDGE-SCENES. Tests for each throw and the opacity release.
2. **Layout family.** BRIDGE-CONNECTED, BRIDGE-LAYOUT, BRIDGE-DECLLAYOUT. Tests: invalid `Motion` throws on set, `Then()` awaits a fresh flight, two arms throw.
3. **Flip and selection.** BRIDGE-FLIP, BRIDGE-SELECTION. Tests for validation and the orientation change.
4. **Motion states.** BRIDGE-MOTION, all five fixes. Tests for each.
5. **Presence, exits, popovers, markup.** Tests for each fix.
6. **Modal, toast, pull, sheet.** Tests for re-open origin, backdrop, toast ownership and re-prepare, faulted refresh.
7. **Follow, attach, transformable, scroll observer, reorder.** Tests for each fix.
8. **Distort, particles, image, recipes.** Tests for the frame requests, even thinning, `Parse`, the `PageSharedAxis` duration. Visual: the Particle type page at default spacing shows the whole word.
9. **DevTools.** Restore on close; expanded-row snapshots. Tests for the restore.
10. **Doc-only fixes and backlog.** The doc-only list; the kept-and-recorded decisions; in `docs/BACKLOG.md`, rewrite each bridge and DevTools per-spec line under "Per-spec open items" to the spec's current untested count and remaining open items.

## Open questions

None.
