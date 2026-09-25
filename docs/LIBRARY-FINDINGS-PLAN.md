# Monaco.Presto — Library findings plan

Addresses the 30 gaps recorded in [SAMPLES-PLAN.md](SAMPLES-PLAN.md) §8. Each row gets one of three outcomes:

- **Fix** — change the library, with tests.
- **Document** — the behaviour is faithful to anime.js 4.5.0 (or deliberately kept); say so in the XML docs of the member involved.
- **Defer** — a real design change that needs its own decision; record it under "Open" in [PLAN.md](PLAN.md) §7 with the finding's evidence.

Parity rule from `CLAUDE.md` holds: where upstream behaves the same, the default is **Document**, not a semantic change.
Sample workarounds are removed only where the fix makes them dead code (rows 1, 2, 3, 25); other demo pages are left as they are.

## 1. Core — timer and timeline

| Row | Outcome | Work |
|---|---|---|
| 1 | Fix | Make the `Timer` callbacks (`OnBegin`, `OnUpdate`, `OnLoop`, `OnPause`, `OnComplete`, … every callback property it has) publicly settable after creation, as upstream allows `timer.onUpdate = …`. Add a public settable loop count on `Timer` (same meaning as `TimerParams.Loop`) that retimes `IterationCount` and the derived duration safely mid-playback. Tests for both. Then simplify `samples/PrestoSample/Showcase/TransportBar.axaml.cs`: observe the bound timer through its own `OnUpdate` (chaining any existing handler) instead of a shadow timer, and make Loop toggle the loop count. |
| 2 | Fix | `Clock` / `Engine.FrameRate`: `0` (and negative) means uncapped, not "never render". Document it on `FrameRate`. Test. Update `Showcase/FpsMeter.cs` if it works around this. |
| 5 | Fix | Expose the eased, direction-resolved playhead as a public read-only `IterationTime` on `Timer`. Add a remark on `Timeline.Labels` that labels are raw times and `Seek` is subject to `PlaybackEase` and `Reversed`. |
| 6 | Document | Remark on `Timer.Stretch`: progress is not preserved; `CurrentTime` stays where it was (upstream parity). |

## 2. Core — values, composition, targets, scope

| Row | Outcome | Work |
|---|---|---|
| 8 | Document | On `Composition.Blend`: while a blended tween is live on a property, that property belongs to the additive pass and a `Replace` tween on it is overwritten the same frame. |
| 10 | Fix | `TargetParser.Parse` de-duplication: use a reference-equality `HashSet` above a small threshold (e.g. 16 targets), keep the linear scan below it. Test that order and de-duplication are unchanged at both sizes. |
| 16 | Document | On `TweenParams.TextModifier`: the string written is read back as the next tween's from-value, so it must parse back to the number it was given (no thousands separators, units in a separate element). |
| 17 | Defer | Per-target `TextModifier`/`Modifier` signature. Upstream has the same shape; record as open in PLAN.md §7. |
| 18 | Fix | `Scope` must not grow without bound when used for repeated retargets: on `Register`, prune revertibles that have been **cancelled** (superseded by a replacing animation). Completed ones stay, so `Revert` still restores values. Test: 1 000 replacing retargets inside a scope leave a bounded list, and `Revert` still restores the original value. |
| 19 | Fix | Public query for live animations on a target beside `Presto.Remove`: e.g. `Presto.GetAnimations(object target)` returning the live (not completed/cancelled) animations whose tweens touch it, optionally filtered by property name. Allocation is fine (not a hot path). Tests. Tween counts stay internal. |
| 22 | Document + Defer | Remark on `Composition` that a replaced tween cannot reach a property the additive pass owns, and that a blended tween's `To` sets the group's base. Record "relative/delta `To` for blended tweens" as open in PLAN.md §7. |
| 30 | Document | On `DraggableAxisParams.Modifier`: must be idempotent (rounding, wrapping); it is re-applied to its own output from the release on (upstream parity). |

## 3. Core — shapes and text

| Row | Outcome | Work |
|---|---|---|
| 4 | Defer | Lazily resolved timeline-child targets. Record as open in PLAN.md §7; the `AddEffect` + `Utils.KeepTime` rebuild is the documented pattern — say so in the `TextSplitter`/`SplitText` effect docs. |
| 7 | Fix | Public `Shapes.MorphPointCount(from, to, precision)` (or the equivalent name matching the existing `MorphTo` surface) returning the number of points the morph samples, delegating to `GeometryCodec.PointCount`. Test. |
| 23 | Fix | Split pieces expose their line: a `LineIndex` on word and char pieces (or `Words` grouped per line — pick whichever fits the existing `SplitText` types), and the split's measured line height. Tests over a wrapped split. |

## 4. Bridge — property writes and compositor

| Row | Outcome | Work |
|---|---|---|
| 3 | Fix | Promote `samples/PrestoSample/Showcase/SceneText.cs` to the bridge beside `CompositorScene.Brush` (public, same caching behaviour: glyph runs built and cached so per-frame drawing allocates nothing). Move its tests from `tests/PrestoSample.Tests/SceneTextTests.cs` to the bridge test project. Delete the sample copy and point its users at the bridge type. |
| 9 | Fix | `CompositorScene.Brush`: bound the cache (clear or evict once it exceeds a fixed size, e.g. 1 024 entries) and document that it is intended for a small palette. Test that a sweep of distinct colours does not grow the cache past the bound. |
| 24 | Fix | Clamp `Opacity` to [0, 1] when written through the Avalonia property adapter and on the compositor opacity track. Tests for an overshooting ease on both paths. |
| 26 | Fix | `MorphTo` reads its start geometry through the target adapter — any `Geometry`-typed `Data` property (`Path`, `PathIcon`, …) — not `(target as Path)?.Data`. Test morphing a `PathIcon`. |
| 27 | Fix | Remove the bridge's own allocation on the write path: a typed (`double`) observable/binding for `Opacity` and other `double` properties so the number is not boxed per frame. Update the `AllocationTests` expectations and PLAN.md §7 decision 9 with the new figures; document what remains Avalonia's. |
| 28 | Fix + Defer | Fix: reverting a per-visual `CompositorAnimation` leaves its mirror at 0, not minus its delay. Document on `AnimateOnCompositor` that it is meant for a handful of visuals, not thousands. Defer the shared playback model to PLAN.md §7 open. |
| 29 | Fix | `CompositorAnimation`: push the opacity end value on every completion, in either direction (not a one-shot `Then` from the first start). `OnComplete`/`Then` report when the mirror that finishes **last in the current direction** finishes. Tests: reverse a finished fade → `Visual.Opacity` reads the drawn value; a staggered reverse from mid-flight completes after the last card. |

## 5. Bridge — layout, draggable, scroll, text

| Row | Outcome | Work |
|---|---|---|
| 11 | Fix | `LayoutTransition` measures against the nearest scrolling ancestor (position in the `ScrollViewer`'s content coordinates) rather than the window, so scrolling is not a layout move. Headless test: scroll a `ScrollViewer` holding an armed control — no transition starts. |
| 12 | Fix | `LayoutTransition` measures the layout slot (from `Bounds` up the tree), not the drawn box via `TranslatePoint`, so render-transform shorthands (a drag, an entry transform) are not inverted back. Also make `RenderTransformComposer` clear its "animating" mark when the shorthand tween ends, so a later FLIP move does not log the collision warning. Tests for both. |
| 13 | Fix | Give `AnimateSharedElement` the `mode` and `once` parameters `AnimateLayout` has, and when the control already holds a live transition, take `first` from where it is drawn instead of the slot, so re-arming mid-flight is continuous. Test the 600 ms interrupted-halfway case from the finding. |
| 14 | Fix | `Draggable.Refresh()` during a release in flight re-targets the release instead of replacing it with a zero-duration muted write, and a refresh that ends a release reports `OnSettle`. Test: resize the container mid-throw → the throw lands and settles. |
| 15 | Fix | `Draggable.Stop()`/`Reset()` retire only what the draggable owns (cancel its own release/overshoot/scroll animations), never pause or remove animations on other targets. Test: a `Reset()` while an unrelated animation runs on another control leaves it running to `OnComplete`. |
| 20 | Document | On `OnScroll`/`ScrollObserver`: pinning is the caller's overlay (no `position: sticky` in Avalonia); describe the sibling-overlay pattern. |
| 21 | Fix | `ScrollObserver.Refresh()` is a no-op until the observer has resolved, and expose a public refresh across all observers of a `ScrollViewer` (e.g. `PrestoAvalonia.RefreshScroll(ScrollViewer)` or a public container accessor — match existing bridge naming). Tests. |
| 25 | Fix | `TextSplitter` turns off the host's `ClipToBounds` while split and restores the original value on `Revert`. Test. Remove the now-redundant `ClipToBounds="False"` from `TypographyPage`. |

## 6. Docs

- [SAMPLES-PLAN.md](SAMPLES-PLAN.md) §8: add a final column (or a note per row) with the outcome: Fixed / Documented / Deferred to PLAN.md §7.
- [PLAN.md](PLAN.md) §7: add the deferred items (rows 4, 17, 22, 28) under "Open", and update decision 9 for row 27.

## Verification

Core and bridge: `dotnet test --solution Monaco.Presto.slnx` green. Sample changes (rows 1, 2, 3, 25) are small; they are checked by the `PrestoSample.Tests` smoke tests and a Desktop look at the Title sequence transport bar and the Typography wordmark.
