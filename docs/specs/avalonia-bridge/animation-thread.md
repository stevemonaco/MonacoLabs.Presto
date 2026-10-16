---
id: BRIDGE-THREAD
title: Animation thread and compositor animations
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto
  - MonacoLabs.Presto.Compositor
sources:
  - MonacoLabs.Presto.Avalonia/AnimationThread.cs
  - MonacoLabs.Presto.Avalonia/ThreadedAnimation.cs
  - MonacoLabs.Presto.Avalonia/PrestoAvalonia.cs
  - MonacoLabs.Presto.Avalonia/AnimateExtensions.cs
  - MonacoLabs.Presto.Avalonia/Compositor/CompositorAnimation.cs
  - MonacoLabs.Presto.Avalonia/Compositor/CompositorAnimationParams.cs
  - MonacoLabs.Presto.Avalonia/Compositor/CompositorCompiler.cs
  - MonacoLabs.Presto.Avalonia/Compositor/CompiledTrack.cs
  - MonacoLabs.Presto.Avalonia/Compositor/CompositorProperty.cs
  - MonacoLabs.Presto.Avalonia/Compositor/CompositorRegistry.cs
  - MonacoLabs.Presto.Avalonia/Compositor/CompositorScratch.cs
  - MonacoLabs.Presto.Avalonia/Compositor/CompositorTracks.cs
  - MonacoLabs.Presto.Avalonia/Compositor/ICompositorTrackOwner.cs
types:
  - AnimationThread
  - ThreadedAnimation
  - CompositorAnimation
  - CompositorAnimationParams
tests:
  - AnimationThreadTests
  - CompositorAnimationTests
  - CompositorCompileTests
  - CompositorPlaybackTests
  - CompositorRegistryTests
  - AnimationBuilderBridgeTests
  - DiscretePropertyTests
  - CompositorPageTests
upstream:
  - src/waapi/waapi.js
  - src/waapi/composition.js
  - tests/suites/waapi.test.js
depends:
  - BRIDGE-INIT
  - BRIDGE-TARGETS
  - BRIDGE-TRANSFORMS
  - BRIDGE-HELPERS
  - BRIDGE-LAYOUT
  - CORE-ANIMATION
  - CORE-TIMER
  - CORE-ENGINE
  - CORE-SCOPE
  - CORE-BUILDER
  - CORE-REDUCED
  - CORE-SPRING
---

# Animation thread and compositor animations

## Purpose

Runs an animation's transform and opacity on Avalonia's render thread, so it keeps moving while the UI thread is blocked: the counterpart of upstream's `waapi.animate`, compiled into composition `KeyFrameAnimation`s on each control's own composition visual. A paused Presto `Animation` over a scratch copy of the control's transform (the mirror) is both the sampler and the playback model. `AnimationThread` lets a caller pick the thread per call, with a silent UI-thread fallback. Layout transitions (BRIDGE-LAYOUT) share the per-property registry. Also specifies `PrestoAvalonia.AnimateOnCompositor`, `PrestoAvalonia.CanAnimateOnCompositor`, the `PrestoAvalonia.Animate` and `AnimateExtensions.Animate` overloads that take an `AnimationThread`, and `AnimateExtensions.AnimateOnCompositor`.

## Requirements

### Choosing the thread

- **BRIDGE-THREAD-001** — When `thread` is `UIThread`, `Animate(targets, …, thread)` shall run the animation on the engine with `Presto.Animate`, even when every property has a composition counterpart, and return a handle whose `UIThread` is set and `OnCompositor` is false.
  - Tests: `AnimationThreadTests.TheHandleReportsTheThreadTheModePicked`
- **BRIDGE-THREAD-002** — When `thread` is `Compositor`, `Animate` shall run the animation as `AnimateOnCompositor` does and throw the same `NotSupportedException`, with the same message, for a property or setting the compositor lacks.
  - Tests: `AnimationThreadTests.ForcingTheCompositorThrowsWhatAnimateOnCompositorThrows`, `AnimationThreadTests.TheHandleReportsTheThreadTheModePicked`
- **BRIDGE-THREAD-003** — When `thread` is `PreferCompositor`, `Animate` shall run on the render thread when `CanAnimateOnCompositor` holds and every target is a `Visual`, and all targets on the engine otherwise, without throwing for an unsupported property, setting or target.
  - Tests: `AnimationThreadTests.TheHandleReportsTheThreadTheModePicked`, `CompositorPageTests.EachThreadModeLaneReportsTheThreadItGot`, `AnimationThreadTests.PreferringTheCompositorForAVisualAndANonVisualRunsBothOnTheUIThread`
- **BRIDGE-THREAD-004** — When `PreferCompositor` falls back, the animation shall end in the same state as `Animate` without a thread argument.
  - Tests: `AnimationThreadTests.AFallbackEndsWhereAnimateEnds`
- **BRIDGE-THREAD-058** — When a target is not a `Visual`, `AnimateOnCompositor` and `Animate` with `Compositor` shall throw `NotSupportedException` with the message `<type name> is not a Visual. The compositor animates Visuals only.` before compiling anything.
  - Tests: `AnimationThreadTests.ForcingTheCompositorForANonVisualThrowsNamingIt`
- **BRIDGE-THREAD-005** — `CanAnimateOnCompositor` shall resolve the params against `Engine.Current` (so a reduced form is what is checked) and, when a setting is unsupported, return false with the first of `Keyframes`, `FrameRate`, `PlaybackEase`, `Composition`, `Modifier`, `Id`, a non-default `Priority`, `OnBeforeUpdate`, `OnUpdate`, `OnLoop`, `OnPause`, `OnRender`, `WhenDetached` as the reason, or `A setting` for any other setting BRIDGE-THREAD-012 does not carry.
  - Tests: `AnimationThreadTests.CanAnimateOnCompositorNamesWhatDoesNotFit`, `AnimationThreadTests.EverySettingTheCompositorDoesNotCarryIsRejected`
- **BRIDGE-THREAD-006** — When every setting is supported, `CanAnimateOnCompositor` shall return false with the first property key that has no composition counterpart as the reason, or true with a null reason.
  - Tests: `AnimationThreadTests.CanAnimateOnCompositorNamesWhatDoesNotFit`
- **BRIDGE-THREAD-007** — The builder overloads shall run a `ToParams()` snapshot of the builder, and the lambda overloads a builder built by the lambda, exactly as the equivalent params do.
  - Tests: `AnimationBuilderBridgeTests.ABuilderRunsOnTheCompositorAsTheEquivalentParamsDo`, `AnimationBuilderBridgeTests.ALambdaRunsOnTheCompositorAsItsBuilderDoes`
- **BRIDGE-THREAD-008** — `ThreadedAnimation.Play`, `Pause`, `Resume`, `Cancel` and `Revert` shall delegate to the live animation and return the handle.
  - Tests: untested
- **BRIDGE-THREAD-009** — `ThreadedAnimation.Then()` and `await` shall complete with the handle itself once the live animation completes.
  - Tests: `AnimationThreadTests.ACompositorHandleResolvesWithItsAnimation`, `AnimationThreadTests.AFallbackEndsWhereAnimateEnds`

### What the compositor can carry

- **BRIDGE-THREAD-010** — The compositor animation shall map the keys given to the `CompositorAnimationParams` `PropertyKey` indexer as follows: `"X"` and `"Y"` to the composition `Translation`, `"Scale"`, `"ScaleX"` and `"ScaleY"` to `Scale` with the uniform scale multiplying each axis scale, `"Rotate"` to `RotationAngle` in radians, and `"Opacity"` or `Visual.OpacityProperty` to `Opacity`.
  - Tests: `CompositorCompileTests.XAndYCompileIntoOneSampledTranslationTrack`, `CompositorCompileTests.TheUniformScaleMultipliesTheAxisScales`, `CompositorCompileTests.RotateCompilesToRadians`, `CompositorAnimationTests.TranslationMovesThePixels`
- **BRIDGE-THREAD-011** — When a property has no composition counterpart, the compositor animation shall throw `NotSupportedException` with the message `'<name>' has no composition property. The compositor animates X, Y, Rotate, Scale, ScaleX, ScaleY and Opacity only.`
  - Tests: `CompositorCompileTests.APropertyWithNoCompositionCounterpartThrows`, `DiscretePropertyTests.ABoolHasNoCompositorCounterpart`, `AnimationThreadTests.ForcingTheCompositorThrowsWhatAnimateOnCompositorThrows`
- **BRIDGE-THREAD-012** — When `AnimationParams` or a builder run on the compositor set any setting other than the properties, `Duration`, `Delay`, `Ease`, `Loop`, `LoopDelay`, `Alternate`, `Reversed`, `Autoplay`, `PlaybackRate`, `OnBegin`, `OnComplete` and `WhenReduced` (resolved first), the call shall throw `NotSupportedException` with the message `<reason> has no counterpart on the compositor.`, the reason being the one BRIDGE-THREAD-005 gives.
  - Tests: `AnimationBuilderBridgeTests.KeyframesAreNotSupportedOnTheCompositor`, `AnimationBuilderBridgeTests.OnUpdateIsNotSupportedOnTheCompositor`, `AnimationThreadTests.ForcingTheCompositorThrowsWhatAnimateOnCompositorThrows`, `AnimationThreadTests.EverySettingTheCompositorDoesNotCarryIsRejected`
- **BRIDGE-THREAD-013** — A rotation given with an angle unit shall compile to the same angle the UI thread would render.
  - Tests: `CompositorCompileTests.AnAngleUnitConvertsTheSameWayItDoesOnTheUIThread`
- **BRIDGE-THREAD-014** — Function-based values and staggers, in the top-level settings and in property values, shall be resolved per target on the UI thread when the animation is created.
  - Tests: `CompositorCompileTests.AStaggeredDelayResolvesPerTarget`, `CompositorCompileTests.PerTargetValuesCompileEachTargetOnItsOwn`
- **BRIDGE-THREAD-015** — Each target shall start from its control's current transform shorthands and opacity, or, for a property another compositor animation drives, from where that animation's mirror stands.
  - Tests: untested

### Compiling

- **BRIDGE-THREAD-016** — When a property's track is one tween spanning the whole iteration with the default modifier and no text modifier (for opacity, also an ease that stays within 0..1), the compositor animation shall compile it to two key frames eased exactly by the tween's ease.
  - Tests: `CompositorCompileTests.ASinglePlainTweenCompilesToTwoExactKeyFrames`
- **BRIDGE-THREAD-017** — Any other track shall be sampled from the mirror at 100 linear segments per iteration.
  - Tests: `CompositorCompileTests.XAndYCompileIntoOneSampledTranslationTrack`, `CompositorCompileTests.AnOvershootingEaseCompilesOpacityInsideZeroToOne`
- **BRIDGE-THREAD-018** (inherited) — Sampled opacity values shall be clamped to 0..1.
  - Tests: `CompositorCompileTests.AnOvershootingEaseCompilesOpacityInsideZeroToOne`
- **BRIDGE-THREAD-019** — When the ease is a spring, the compiled duration shall be the spring's settling duration and the render thread shall evaluate only its pure solver, so sampling never completes the spring.
  - Tests: `CompositorCompileTests.ASpringEaseCompilesWithoutTheSamplingCompletingIt`, `CompositorCompileTests.AnInertiaCompilesToTheCurveTheUIThreadPlays`
- **BRIDGE-THREAD-020** — `Loop` shall map to an iteration count of loop + 1, or to a forever iteration for an infinite loop.
  - Tests: `CompositorCompileTests.LoopMapsToTheIterationCount`
- **BRIDGE-THREAD-021** — `Alternate` and `Reversed` shall map to the `Normal` and `Alternate` playback directions, a reversed run compiling its key frames mirrored in time, so a reversed animation ends on its start values.
  - Tests: `CompositorCompileTests.AReversedRunCompilesMirroredKeyFramesPlayedForward`
- **BRIDGE-THREAD-022** — When a looping animation has a `LoopDelay`, each cycle shall hold the iteration's end value for the loop delay; an alternating finite loop shall have no trailing hold (BRIDGE-THREAD-023), while a non-alternating one keeps it, holding the end value the mirror ends on.
  - Tests: `CompositorCompileTests.ALoopDelayHoldsTheEndValueForTheRestOfTheCycle`
- **BRIDGE-THREAD-023** — When a finite alternating animation has a `LoopDelay`, the compositor animation shall compile one sampled track over the mirror's whole duration, played once in the `Normal` direction and ending on the mirror's end values; a `Forever` one shall compile one forward-and-back cycle with both holds, repeated forever.
  - Tests: `CompositorCompileTests.AFiniteAlternatingLoopDelayUnrollsIntoOneTrackEndingWithTheMirror`, `CompositorCompileTests.AnAlternatingLoopDelayForeverPlaysForwardAndBackCycles`
- **BRIDGE-THREAD-059** — A finite alternating compositor animation's tracks shall end when its mirror ends, on the mirror's end values.
  - Tests: `CompositorCompileTests.AFiniteAlternatingLoopDelayUnrollsIntoOneTrackEndingWithTheMirror`
- **BRIDGE-THREAD-024** — `Delay` shall become the track delay, with the from value drawn from creation on, before the delay elapses.
  - Tests: `CompositorCompileTests.DelayBecomesTheTrackDelayAndLeavesTheFromValueOnKeyFrameZero`
- **BRIDGE-THREAD-025** — When nothing but the top-level delay varies per target, targets with the same starting transform and opacity shall share one set of sampled key frames, each with its own delay and mirror; a differently seeded target or per-target values compile on their own.
  - Tests: `CompositorCompileTests.UniformTargetsSeededAlikeShareTheirSampledTracks`, `CompositorCompileTests.ATargetSeededDifferentlyGetsItsOwnTracks`, `CompositorCompileTests.ASharedTrackLeavesItsMirrorWhereSamplingWould`, `CompositorAnimationTests.UniformStaggeredTargetsEachLandOnTheirEndValue`
- **BRIDGE-THREAD-026** (inherited) — Track durations and delays shall be wall-clock times, scaled by the mirror's speed and the engine's speed, and a cycle shorter than 1 ms shall play over 1 ms.
  - Tests: untested

### Running on a control

- **BRIDGE-THREAD-027** — When a `Scale` or `Rotate` track runs, the composition `CenterPoint` shall follow the control's `RenderTransformOrigin`, relative origins tracking the control's size, and move at the next frame when the origin changes.
  - Tests: `CompositorAnimationTests.RotationTurnsAboutTheCenterOfTheResizedControl`, `CompositorAnimationTests.AScalePivotsOnTheRenderTransformOrigin`, `CompositorAnimationTests.ChangingTheOriginMidAnimationMovesThePivotAtTheNextFrame`
- **BRIDGE-THREAD-028** — While an `Opacity` track runs, the compositor animation shall hold `Visual.Opacity` at `Animation` priority, so a re-sync of the control (an `InvalidateVisual`) does not stop the fade.
  - Tests: `CompositorAnimationTests.InvalidateVisualDoesNotStopACompositorFade`
- **BRIDGE-THREAD-029** — When a target's mirror completes, its end opacity, clamped to 0..1, shall be pushed through that hold so the control reports what is drawn.
  - Tests: `CompositorAnimationTests.EveryCompletionHandsTheDrawnOpacityBackToTheControl`
- **BRIDGE-THREAD-030** — When a target has no composition visual yet (outside the visual tree), its tracks and mirror shall start when it attaches.
  - Tests: untested
- **BRIDGE-THREAD-031** — When `Autoplay` resolves to false, nothing shall start until `Play` or `Resume`.
  - Tests: `CompositorPlaybackTests.SeekingBeforeTheFirstResumeCarriesOnFromThere`

### Playback control

- **BRIDGE-THREAD-032** — `Pause` shall stop every track and write the mirror's exact current values, and `Resume` shall carry on from there in the current direction.
  - Tests: `CompositorPlaybackTests.PauseFreezesThePixelsAndResumeCarriesOnFromThere`
- **BRIDGE-THREAD-033** — `Seek` shall move the mirror and recompile from its position: a finite animation into the single forward cycle it has left, written directly while paused.
  - Tests: `CompositorPlaybackTests.SeekingToHalfLandsOnTheHalfwayValue`
- **BRIDGE-THREAD-034** — A `Seek` before the first start shall be kept in the mirror, and the first start shall compile from that position rather than from zero.
  - Tests: `CompositorPlaybackTests.SeekingBeforeTheFirstResumeCarriesOnFromThere`
- **BRIDGE-THREAD-035** — When an infinite loop is recompiled, the track shall be one full period starting at the current phase, so the cycle rotates rather than restarts.
  - Tests: `CompositorPlaybackTests.SeekingAnInfiniteLoopRotatesThePhaseInsteadOfRestarting`
- **BRIDGE-THREAD-036** — `Alternate` shall flip the mirror's direction and recompile; `Reverse` shall play backwards and `Play` forwards, resuming in both cases.
  - Tests: `CompositorPlaybackTests.ReverseMidFlightWalksThePixelsBackTowardsTheStart`, `CompositorPlaybackTests.AStaggerReversedMidFlightCompletesWhenTheLastVisualIsHome`
- **BRIDGE-THREAD-037** — Setting `Speed` shall change the mirror's speed and recompile from the current position.
  - Tests: `CompositorPlaybackTests.DoublingTheSpeedCoversTwiceTheDistance`
- **BRIDGE-THREAD-038** — `Restart` shall seek to 0 with callbacks muted and resume; `Complete` shall seek every target to its end.
  - Tests: `CompositorPlaybackTests.RestartAndCompleteLandOnTheStartAndEndValues`
- **BRIDGE-THREAD-039** (inherited) — `CurrentTime`, `Progress`, `Began` and `Speed` shall read the mirror of the target whose delay plus duration is longest.
  - Tests: untested

### Completion and ending

- **BRIDGE-THREAD-040** — `Completed` shall be true once every target's mirror has completed in the current direction.
  - Tests: `CompositorPlaybackTests.AStaggerReversedMidFlightCompletesWhenTheLastVisualIsHome`
- **BRIDGE-THREAD-041** (inherited) — When the last target completes, `OnComplete` shall fire once with that target's mirror, and only the longest target's mirror shall fire `OnBegin`.
  - Tests: untested
- **BRIDGE-THREAD-042** — `Then` shall resolve, after invoking its callback, when the animation completes; on an animation already completed it shall invoke the callback and return a completed task at once.
  - Tests: `CompositorAnimationTests.ThenResolvesWhenTheMirrorCompletes`
- **BRIDGE-THREAD-043** — `Cancel` shall detach every track, leaving the pixels where they stand, release the opacity hold, so `Visual.Opacity` reads the control's own value and a later local write is drawn, and leave `Then` unresolved.
  - Tests: `CompositorAnimationTests.CancelFreezesThePixelsWhereTheyAre`, `CompositorAnimationTests.CancelHandsVisualOpacityBackToTheControl`
- **BRIDGE-THREAD-044** — `Revert` shall cancel, write identity transforms and the control's own opacity to the composition visual, reset the `CenterPoint`, release the opacity hold and leave every mirror at 0.
  - Tests: `CompositorAnimationTests.RevertPutsTheControlBackAsItWas`, `CompositorAnimationTests.RevertLeavesEveryMirrorAtZeroRatherThanBeforeItsDelay`
- **BRIDGE-THREAD-045** — The compositor animation shall register with `Engine.Current.CurrentScope` when there is one, so reverting the scope reverts it.
  - Tests: `CompositorPageTests.ThePageRunsAndRevertsEverythingOnDetach`
- **BRIDGE-THREAD-057** — When a compositor animation with `Reversed` completes, the visual shall rest on the animation's start values until it is reverted.
  - Tests: `CompositorPlaybackTests.AReversedRunRestsOnItsStartValuesUntilReverted`, `PageTransitionTests.GoingBackWithTheForwardPairNeverBlinksTheIncomingPage`

### Ownership of composition properties

- **BRIDGE-THREAD-046** — When a compositor animation or layout transition starts on a composition property of a visual another compositor animation drives, the earlier one shall release that property, and an animation left with no running track shall cancel with `Then` unresolved.
  - Tests: `CompositorRegistryTests.ASecondAnimationOnOnePropertyCancelsTheFirst`, `CompositorRegistryTests.ALayoutTransitionTakesTheTranslationFromACompositorAnimation`
- **BRIDGE-THREAD-047** — Compositor animations on different composition properties of one control shall run side by side.
  - Tests: `CompositorRegistryTests.AnimationsOnDifferentPropertiesOfOneControlCoexist`
- **BRIDGE-THREAD-048** — `Presto.Remove(control)` shall cancel every compositor animation on the control, and `Presto.Remove(control, property)` only the track the property feeds.
  - Tests: `CompositorRegistryTests.RemoveCancelsACompositorAnimation`, `CompositorRegistryTests.RemovingOnePropertyLeavesTheOtherRunning`
- **BRIDGE-THREAD-049** — In a Debug build, starting a transform track on a visual whose transform shorthands are animating, or a shorthand on a visual a compositor transform track drives, shall log one Avalonia `Animations` warning.
  - Tests: `CompositorRegistryTests.MixingAShorthandAndACompositorAnimationWarnsOnceEitherWayRound`

### Reduced motion

- **BRIDGE-THREAD-050** — While `PrestoDefaults.ReduceAll` is set, a compositor animation without a reduced alternative shall land on its end values at once.
  - Tests: `CompositorAnimationTests.ReduceAllLandsACompositorAnimationOnItsEndValuesAtOnce`
- **BRIDGE-THREAD-051** — While `ReducedMotion` is set, params or a builder with `WhenReduced` shall run their reduced form on the compositor, which `ReduceAll` leaves alone.
  - Tests: `CompositorAnimationTests.ReduceAllLeavesAReducedAlternativeOnTheCompositorAlone`, `CompositorAnimationTests.DictionaryParamsOnTheCompositorPlayTheirReducedForm`

### Defaults

- **BRIDGE-THREAD-052** — When `Duration`, `Delay` or `Ease` is not set on `CompositorAnimationParams`, the compositor animation shall use `Engine.Current.Defaults`' value.
  - Tests: untested
- **BRIDGE-THREAD-053** — When `Autoplay` is not set on `CompositorAnimationParams`, the compositor animation shall start according to `Engine.Current.Defaults.Autoplay`.
  - Tests: untested
- **BRIDGE-THREAD-054** — When `PlaybackRate` is not set, `Speed` shall start at the engine's default playback rate (1).
  - Tests: untested
- **BRIDGE-THREAD-055** (inherited) — When the animation has no target, `Speed` shall read 1, `CurrentTime` 0 and `Began` false.
  - Tests: untested
- **BRIDGE-THREAD-056** — When `muteCallbacks` is not passed, `Seek` shall fire callbacks.
  - Tests: untested

## Invariants

- At most one owner (compositor animation or layout transition) drives each composition property of a visual.
- Exactly one of `ThreadedAnimation.UIThread` and `ThreadedAnimation.Compositor` is non-null.
- The mirror and the render thread are at most one frame apart after any control call; the next call samples the mirror again.
- A finished track stays on the composition visual holding its last key frame until the animation is canceled, reverted or replaced.

## Edge cases

- Zero targets: `Completed` is true at once, so `Then` resolves immediately.
- A paused or not yet started animation ignores `Pause`; `Resume` on one that never started starts it.
- A shorthand and a compositor transform on one control multiply on the server (render transform, then composition transform): the compositor track also seeds from the shorthand's value, so the two offsets add.
- `Seek` mutes nothing unless asked; `Restart` always mutes.

## Threading and lifetime

- Every entry point and control call is UI-thread only. The mirror ticks on the engine current at creation; its callbacks and `Then` continuations run there and lag the render thread while the UI thread is blocked, catching up afterwards.
- The mirror targets a scratch object, so `PrestoAvalonia.WhenDetached` never pauses or reverts a compositor animation.
- Survival of a blocked UI thread holds only where the render loop has its own thread; Browser and headless render on the UI thread (see "Browser's blocked-UI-thread limitation is documented, not worked around"). Freeze-proofing is checked by hand on the sample's Compositor page.
- The Avalonia semantics this rests on (client setter equality guard, a set removing the server animation) are pinned by `InvalidateVisualDoesNotStopACompositorFade`, which must be re-run when `AvaloniaVersion` moves.

## Reduced motion

- `AnimationParams`/builder paths resolve `WhenReduced` before compiling (BRIDGE-THREAD-051); `CompositorAnimationParams` has no `WhenReduced`, so the direct params path only gets `ReduceAll` (BRIDGE-THREAD-050).

## Allocation

- Creating and every control call (pause, seek, speed…) allocate: they sample the mirror and build composition animations. Between calls the render thread runs Avalonia's key frame animations and the UI thread ticks only the mirror, on the core's allocation-free tick.

## Provenance

- Not CSS: tracks are composition `KeyFrameAnimation`s, and only `X`, `Y`, `Rotate`, `Scale*` and `Opacity` are carried, because only those have composition counterparts; anything else throws instead of falling back, since a silent fallback would defeat the point of the API. Skew, colors and other properties are UI-thread only.
- Playback control is emulated by recompiling from the mirror, because a composition `KeyFrameAnimation` has no pause, seek, rate or readback; upstream sets `playbackRate` and `currentTime` on live WAAPI animations.
- No `composition` or `persist` parameters: composition is always replace within the compositor registry, and finished tracks always persist.
- No `commitStyles`, `forEach`, `currentTime` setter or exposed `animations`; `Seek` replaces the setter.
- `OnComplete` receives the last-finishing target's mirror `Timer`, not the animation; `Then`'s callback receives the animation.
- Exact single-segment tracks use `PrestoEasing` instead of a sampled `linear()`; everything else samples 100 points per iteration, the count upstream's `easingToLinear` uses.
- `AnimationThread`, `ThreadedAnimation` and `CanAnimateOnCompositor` have no upstream counterpart: a browser picks the thread for WAAPI itself, while here the caller chooses (see "The thread is an argument on bridge entry points").

## Decisions

- **Compositor animations are a reduced, separate type.** Like upstream's `waapi.animate`, they have their own registry and carry only `X`, `Y`, `Rotate`, `Scale*` and `Opacity`, and they compile by sampling a paused mirror Presto `Animation` on a scratch object (100 samples per iteration), a single two-value tween compiling exactly through `PrestoEasing`; `RotationAngle` is radians on the wire and degrees at the API. Reason: the mirror's `Seek` is Presto's own timing math, so keyframes, eases, modifiers, springs and staggers come for free and the render thread plays what the UI thread would; 100 samples are enough linear segments that a sampled ease reads as its curve; background color could only be a child visual drawn above the control's content, so it stays UI-thread work. Rejected: keeping every animation on the UI thread (the earlier position, replaced once a busy indicator had to survive a blocked UI thread); writing composition properties directly while keeping Presto's clock, because per-frame sync overwrites them on a managed visual.
- **Unsupported properties throw rather than fall back.** A property or setting with no composition counterpart throws `NotSupportedException` from `AnimateOnCompositor`, including a builder setting it cannot honor and a bridge-only one such as `WhenDetached`. Which settings pass is a whitelist of what `CompositorAnimationParams` carries: everything else must be at its default, checked by record equality, so a setting added later, or one on a subclass of `AnimationParams`, is rejected (as `A setting`) until it is carried. Reason: a silent UI-thread fallback, or a silently dropped setting, would defeat the point of the API, and a fixed list of unsupported settings had already let `WhenDetached` through. Rejected: silent fallback and silent dropping; fallback is opt-in through `PreferCompositor` instead.
- **Only pure eases cross to the render thread.** A spring compiles through its pure solver, never `Spring.Ease`. Reason: the render thread calls the ease per frame, and `Spring.Ease` mutates its completion state as it is sampled.
- **Delay holds the from value.** `Delay` maps to the track delay with the initial value set before it. Reason: a UI-thread animation renders its `from` value at creation, so the compositor form looks the same through the delay.
- **Reversal is compiled into the key frames.** A reversed run compiles its key frames mirrored in time, eases inverted, and plays them `Normal` (`Alternate` when it alternates), its delay holding the forward end it starts on. Reason: Avalonia's key frame instance returns the last frame once a run ends, in any direction, and the first during a delay, so `PlaybackDirection.Reverse` ended on the forward end values. Rejected: writing the end value from the mirror on completion, which lands up to a frame late and would still flash.
- **The pivot follows `RenderTransformOrigin`.** The `CenterPoint` is an expression built from the origin at compile time: a relative origin scales with the control's size, an absolute one is constant, and an origin change restarts it. Reason: a fixed center (the earlier `Size * 0.5`) made a bottom-origin ball squash about its middle while UI-thread shorthands honor the origin; an expression keeps a relative origin correct across resizes, and keeping the absolute origin an expression lets start, restart and stop share one `CenterPoint` animation.
- **Opacity is held and playback is emulated by recompiling.** `Visual.Opacity` is held by an `Animation`-priority binding for the life of a compositor opacity track; `Pause`, `Resume`, `Seek`, `Reverse` and speed stop and recompile from the mirror's position (rotating the cycle for infinite loops), and the mirror supplies time, progress, callbacks and `Then`, up to one frame apart from the render thread. `Cancel` releases the hold, as `Revert` and a hand-over do, so the control's own opacity applies again: the frozen pixels stay until it differs from the last value the hold pushed, and a later local write is drawn. Reason: the server animation dies the moment `Visual.Opacity` changes, and `KeyFrameAnimation` has no pause, seek, rate, completion event or readback; a hold kept after `Cancel` had no owner left to release it, so `Visual.Opacity` reported a stale value and masked local writes. Rejected: measuring and compensating the skew through `CompositionBatch.Processed`, not worth it until the skew is visible.
- **One owner per composition property.** Compositor animations register per (visual, composition property) with replace semantics, as `waapi/composition.js` does, `Presto.Remove(control)` reaches them, and layout transitions share the registry. Mixing a UI-thread transform shorthand and a compositor animation on one control is unsupported and only gets a Debug warning. Reason: the two compose multiplicatively on the server, and additive composition and per-frame callbacks do not exist there.
- **The thread is an argument on bridge entry points.** `AnimationThread { UIThread, Compositor, PreferCompositor }` is taken by `PrestoAvalonia.Animate` and `control.Animate`, which return a `ThreadedAnimation`; `PreferCompositor` falls back, all targets together, when `CanAnimateOnCompositor` fails or a target is not a `Visual`, and `Compositor` throws as `AnimateOnCompositor` does. XAML entry points stay on the UI thread, and `PrestoPageTransition.Thread` defaults to `PreferCompositor`. Reason: the core stays Avalonia-free, `Presto.Animate` could not honor a setting it cannot act on, and one handle holds one animation, so mixed targets cannot split across threads. `CanAnimateOnCompositor` stays target-agnostic, since it answers for the params alone. Rejected: a setting on the params or the builder.
- **The render thread is named after the compositor.** The API says `Compositor` (`AnimateOnCompositor`, `CompositorAnimation`, `AnimationThread.Compositor`, `Thread="PreferCompositor"` in XAML) and `ThreadedAnimation.OnCompositor` reports the thread obtained. Reason: consistency with the existing compositor types, so that "render thread" in prose only ever means where frames are drawn; "Prefer" signals that fallback is expected, not an error.
- **Browser's blocked-UI-thread limitation is documented, not worked around.** Browser runs compositor animations but its render loop is on the UI thread, so they stop with it; nothing is built for that. The Avalonia internals the compositor path rests on (client setter equality guard, a set removing the running server animation, `Offset + Translation` composition) are pinned by `CompositorAnimationTests.InvalidateVisualDoesNotStopACompositorFade`, which is re-run on every `AvaloniaVersion` bump. Reason: single-threaded WebAssembly has no separate render thread, and the internals are not public contract.
- **Finite expanded compositor loops are unrolled into one sampled track.** A finite alternating loop with a `LoopDelay` samples the mirror over its whole duration (loop delays and reversal included) into one track played once; `Forever` keeps the forward-and-back cycle. Reason: `KeyFrameAnimation` has no loop delay and cannot play half a cycle, so ⌈n/2⌉ cycles played an extra iteration on odd counts and an invisible trailing hold on even ones, while one track over the mirror's exact duration ends where the mirror ends. Rejected: falling back to the UI thread (loses render-thread smoothness for a common case), and stopping at the mirror's end (lands mid-cycle and flashes, as "Reversal is compiled into the key frames" found). Cost: samples are capped at 1000, so a large count loses per-iteration easing precision. A non-alternating finite loop keeps its cycles and their trailing hold, since every cycle ends on the end value the mirror ends on, so the hold is invisible and its exact two-key-frame compile is kept.
- **Unset settings follow the engine defaults.** Without `Duration`, `Delay`, `Ease`, `Autoplay` or `PlaybackRate`, a compositor animation takes them from `Engine.Current.Defaults`, and `Seek` without `muteCallbacks` fires callbacks (BRIDGE-THREAD-052 to 054, 056). Reason: the same params then time and start alike on either thread, so `PreferCompositor`'s fallback changes only where the animation runs, and `Seek` behaves as `Timer.Seek` does (CORE-TIMER).
- **Render-thread work is a separate API named "Compositor", and a scene owns its engine.** See BRIDGE-SCENES.

## Non-goals

- Timelines on the compositor; per-frame callbacks; additive composition; reading live server values back (`Get` returns the UI-side value).
- A thread mode on XAML entry points (`Animate.OnAttached`, `Presence`, recipes): they stay on the UI thread (see "The thread is an argument on bridge entry points").
- Thousands of targets: each target gets its own mirror and composition animations.

## Open items

- Untested: `ThreadedAnimation` delegation (008), seeding from another animation's mirror (015), wall-clock scaling and the 1 ms floor (026), start on attach (030), the control mirror's timing reads (039), `OnBegin`/`OnComplete` firing rules (041).
