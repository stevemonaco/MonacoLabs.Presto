# Open-item defects: core

## Why

The specs' Open items sections list defects found when the specs were extracted: code that disagrees with its docs, silent misbehavior, missing validation and stale spec text. Fixing them comes before covering the untested requirements, so the new tests don't pin wrong behavior. This proposal covers the core package; `open-item-defects-bridge.md` covers the Avalonia bridge and DevTools.

## What

Each defect below is fixed in code, or recorded as a decision when the behavior is kept. Each fix updates its spec in the same task: the requirement, its `Tests:` line, a Decisions entry when a choice was made, and the Open items line deleted. Untested-only open items stay; they belong to the later test-coverage batches.

What does not change: the timing model outside the two timer/timeline items named here, and the public API apart from the new exceptions listed.

## Decisions

- **`Complete()` does not fire `OnPause`.** A completion is not a pause, and a handler that resumes or saves state on pause would misfire. Rejected: upstream's order (OnComplete, then OnPause).
- **`Timeline.Remove` keeps `Duration`.** Kept from upstream and pinned by the ported `TimelineCallbacksTests.RemoveNestedAnimations`. Reason: changing it rewrites a ported timing test, which belongs in a timing change of its own. Rejected: shrinking `Duration` too.
- **`ReduceAll` collapses timeline positions.** A fully reduced timeline lands every child's end state at once, as `ReduceAll` promises (CORE-REDUCED). Rejected: keeping positions, which still takes the full length.
- **Spring settings stay in step.** Setting `Mass`, `Stiffness`, `Damping` or `Velocity` re-derives `Bounce` and `Duration`; setting `Bounce` or `Duration` re-derives `Stiffness` and `Damping` for the current `Mass`, leaving `Mass` and `Velocity` as set; a spring built from physical arguments reports the `Bounce` and `Duration` they imply. Rejected: documenting upstream's one-way derivation.
- **Timeline defaults: explicit ones are snapshotted, engine ones are read live.** A timeline given `Defaults` copies them at construction; one without reads the engine's defaults as each child is added. Reason: explicit defaults are a value passed in, and engine defaults are global settings every animation reads when it is built. Record only; no code change.
- **Percentage keyframes reject per-frame settings.** A percentage frame that sets `Duration`, `Delay`, `Modifier` or `Composition` throws `ArgumentException` when the keyframes are built. Reason: the percentage fixes the frame's time, and dropping the other settings silently hid them. Rejected: applying `Modifier`/`Composition` per frame (no tween-level support).
- **`TimeUnit` is set on the main engine only.** Setting it on any other engine throws `InvalidOperationException`. Reason: it changes process-wide state but rescaled only that engine.
- **The ease parser cache is bounded.** Past 256 entries it is cleared before inserting. Reason: callers building strings from varying numbers grew it without limit; clear-on-full keeps the lookup lock-free-simple and allocation-light. Rejected: an LRU (more state for a cache of cheap parses).
- **A stagger's start offset is computed per use.** Reusing one stagger as a position on a second timeline, or after the first grows, uses that timeline's offset. Reason: the cache made reuse depend on first use.

## Spec changes

- **CORE-TIMER:** `Complete()` fires `OnComplete` and not `OnPause`; add the decision; delete the open item.
- **CORE-TIMELINE:** record the `Remove` decision; the zero-duration shift (017) reads the resolved duration, including defaults and function values; positions under `ReduceAll` collapse; delete the three open items.
- **CORE-REDUCED:** `ReduceAll` collapses timeline positions.
- **CORE-SCOPE:** add the timeline-defaults decision; delete the open item.
- **CORE-ANIMATION:** `GetCurrent` on an `Overlap` number tween returns the rendered value; update the member doc.
- **CORE-VALUES:** `Refresh` reconciles a re-decomposed end exactly as the initial build does (codec `Reconcile`, kind homogenizing, padding, unit conversion); the implicit `PrestoColor` conversion keeps channels exact; record that `TweenValue` has no `object` conversion (`TweenValue.Of` covers it).
- **CORE-TARGETS:** registering a codec invalidates cached accessors so it applies to later resolutions; add a decision.
- **CORE-PRESTO:** `Set` raises the timer-created hook; `Get` canonicalizes the property name.
- **CORE-ENGINE / CORE-THREADING:** setting `TimeUnit` on a non-main engine throws; `Precision`'s setter asserts thread affinity in Debug like the other engine-wide setters.
- **CORE-MOTIONVALUE:** `Set` asserts thread affinity in Debug.
- **CORE-INERTIA:** the 60000 decay cap scales with the time unit; `interval` must be positive, `Projectile.Friction` in (0, 1) and `Min <= Max` (`ArgumentOutOfRangeException`); a `ModifyTarget` result within `RestDelta` of the start still lands on the modified target.
- **CORE-SPRING:** the keep-in-step decision; requirements for each setter's derivation.
- **CORE-ANIMATABLE:** the callbacks animation lasts as long as the longest property animation it accompanies, not the 1000 ms engine default; a channel `Set` clears a saved fling; `Fling` on a color or complex property throws `NotSupportedException`.
- **CORE-KEYFRAMES:** the percentage-frame rejection.
- **CORE-EASEPARSER:** the bounded cache.
- **CORE-STAGGER:** the start offset is computed per use.
- **CORE-DIAGNOSTICS:** an animatable's property animations are tagged `"Animatable"`, like its callbacks animation.
- **CORE-COMPOSITION:** delete the integrity-check open item (the decision already follows the code).
- **CORE-PARTICLES:** correct the seeded-randomness decision text to the process-wide counter.

## Tasks

1. **Timer and timeline.** `Complete()` without `OnPause`; the `Remove` decision; the zero-duration shift on the resolved duration; `ReduceAll` collapsing positions; the timeline-defaults decision. Tests for each behavior change; the ported timer/timeline suites stay green. Read `tools/anime/src/timer/timer.js` and `src/timeline/timeline.js` first.
2. **Animation and values.** `GetCurrent` on `Overlap`; `Refresh` reconciliation; exact `PrestoColor` conversion. Tests: an `Overlap` tween's `GetCurrent` mid-flight; a refreshed function end with a different unit and shape; a `Mix` result passed implicitly keeps fractional channels.
3. **Targets and the facade.** Codec registration invalidates cached accessors; `Presto.Set` raises the hook; `Get` canonicalizes. Tests for each.
4. **Engine and threading.** `TimeUnit` on a non-main engine throws; `Precision` and `MotionValue.Set` thread assertions. Tests for the throw (the Debug assertions follow the existing pattern).
5. **Inertia.** Scaled cap, argument validation, `ModifyTarget` within `RestDelta`. Tests for each.
6. **Spring.** Keep-in-step derivation in both directions, and physical-argument springs reporting their implied `Bounce`/`Duration`. Tests round-trip both forms. Read `tools/anime/src/easings/spring` first.
7. **Animatable.** Callbacks animation length, channel `Set` clearing a saved fling, `Fling` rejecting colors. Tests for each.
8. **Keyframes, ease parser, stagger, diagnostics.** Percentage-frame rejection, bounded cache, per-use stagger offset, `"Animatable"` tags. Tests for each.
9. **Spec housekeeping.** CORE-COMPOSITION and CORE-PARTICLES text; in `docs/BACKLOG.md`, rewrite each core per-spec line under "Per-spec open items" to the spec's current untested count and remaining open items (several are stale: colors, callbacks, ease-parser).

## Open questions

None.
