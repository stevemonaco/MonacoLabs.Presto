---
id: CORE-ANIMATION
title: Animation and tweens
package: MonacoLabs.Presto
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto/Animation.cs
  - MonacoLabs.Presto/AnimationParams.cs
  - MonacoLabs.Presto/Tween.cs
  - MonacoLabs.Presto/TweenParams.cs
  - MonacoLabs.Presto/TweenSnapshot.cs
  - MonacoLabs.Presto/TimerTweens.cs
  - MonacoLabs.Presto/Core/Renderer.cs
  - MonacoLabs.Presto/Presto.cs
types:
  - Animation
  - AnimationParams
  - Tween
  - TweenParams
  - TweenSnapshot
  - TimerTweens
tests:
  - AnimationsTests
  - TweensTests
  - ParametersTests
  - FunctionBasedValuesTests
  - ValuesTests
  - KeyframesTests
  - ControlsTests
  - IntrospectionTests
  - CallbacksTests
  - TargetsTests
  - SpringTests
  - LeaksTests
  - DiscreteValuesTests
  - ReducedMotionTests
  - PropertyNamingTests
upstream:
  - src/animation/animation.js
  - tests/suites/animations.test.js
  - tests/suites/tweens.test.js
  - tests/suites/parameters.test.js
  - tests/suites/function-based-values.test.js
  - tests/suites/leaks.test.js
depends:
  - CORE-TIMER
  - CORE-VALUES
  - CORE-TARGETS
  - CORE-EASES
  - CORE-KEYFRAMES
  - CORE-COMPOSITION
  - CORE-STAGGER
  - CORE-REDUCED
---

# Animation and tweens

## Purpose

An `Animation` is a timer (CORE-TIMER) that resolves its parameters into one `Tween` per target, property and keyframe, then derives its own delay and duration from those tweens. This spec covers how the parameters become tweens, how a tween renders, and the animation-level controls `Refresh`, `Revert` and `Stretch`. Keyframes are CORE-KEYFRAMES, composition between tweens is CORE-COMPOSITION, value parsing is CORE-VALUES. The `AnimationParams` indexer takes a `PropertyKey` (CORE-TARGETS). Also specifies `Timer.Tweens`, `Presto.Animate` and `Presto.Set`.

## Requirements

### Construction

- **CORE-ANIMATION-001** — When `Presto.Animate` is called, the animation shall build one tween per parsed target, per property and per keyframe, then initialize itself and raise the owner engine's `TimerCreated`.
  - Tests: `TargetsTests.AnimatingASingleObjectBuildsOneTween`, `TargetsTests.AnimatingAnArrayBuildsOneTweenPerTarget`, `TargetsTests.AnimatingNestedArraysFlattensThem`, `HostHookTests.TimerCreatedFiresAfterInit`
- **CORE-ANIMATION-002** — When the targets parse to nothing, the animation shall build no tweens.
  - Tests: `TargetsTests.AnimatingNullHasNoTargets`
- **CORE-ANIMATION-003** — The animation shall order its tweens by target, then by property in declaration order, then by keyframe, and `Tweens` shall enumerate them in that order without allocating.
  - Tests: `IntrospectionTests.ChildrenAndTweensEnumerateInOrderWithoutAllocating`, `KeyframesTests.GeneralKeyframesInheritTheInstanceParameters`
- **CORE-ANIMATION-004** — When the same property is assigned twice through the indexer, the parameters shall keep its first position with the last value.
  - Tests: `AnimationBuilderTests.DuplicatePropertyKeepsItsFirstPositionWithTheLastValue`
- **CORE-ANIMATION-005** (inherited) — When the indexer getter reads a property that was never assigned, it shall throw `KeyNotFoundException`.
  - Tests: untested
- **CORE-ANIMATION-006** — The animation shall key each tween by the canonical property name the first claiming adapter returns, so every spelling of one property composes against the others.
  - Tests: `PropertyNamingTests.ATypedPropertyOverridesATweenDeclaredByName`, `PropertyNamingTests.ANamedPropertyOverridesATweenDeclaredByHandle`
- **CORE-ANIMATION-007** — The animation's `Id` shall be the `Id` parameter, or else a process-wide sequence number as a string.
  - Tests: untested
- **CORE-ANIMATION-008** — When no tween is built, the animation shall have a `Constants.MinValue` duration and zero iterations, and complete on its first frame.
  - Tests: `AnimationsTests.CompletesInstantlyIfNoAnimatablePropsProvided`

### Settings resolution

- **CORE-ANIMATION-009** — When a setting (`Duration`, `Delay`, `Ease`, `PlaybackEase`, `Modifier`, `Composition`, `OnRender`, timer settings) is null, the animation shall take it from its parent timeline's defaults, or else from its owner engine's `Defaults`.
  - Tests: `TimelinesTests.DefaultParametersAreInheritedByChildren`
- **CORE-ANIMATION-010** — When a property's value is a `TweenParams`, its non-null settings shall override the animation's for that property.
  - Tests: `ParametersTests.SpecificPropertyParameters`, `TweensTests.ProperlyAssignSpecificTweenProperties`
- **CORE-ANIMATION-011** — When `Composition` is unset and the animation has 1000 targets or more, its tweens shall use `Composition.Overlap`.
  - Tests: untested
- **CORE-ANIMATION-012** — When a value, duration, delay or ease is a `FunctionValue`, the animation shall evaluate it per target with the target, its index and the target count, reading numeric strings as numbers.
  - Tests: `FunctionBasedValuesTests.BasicFunctionBasedValues`, `FunctionBasedValuesTests.FunctionBasedStringValuesAreConvertedToNumbers`
- **CORE-ANIMATION-013** — When a function-based value returns a from/to pair, the tween shall use it as its from and to values.
  - Tests: `FunctionBasedValuesTests.FunctionBasedValuesReturningFromToArrayValues`, `FunctionBasedValuesTests.FunctionBasedValuesInsideFromToArrayValues`
- **CORE-ANIMATION-014** — When a function-based value returns a `TweenParams` with a `To`, the tween shall take its `To` and its settings, `Modifier` and `TextModifier` included, as if the property had been declared in object form, so each target can have its own.
  - Tests: `FunctionBasedValuesTests.AFunctionReturningTweenParamsGivesEachTargetItsOwnModifiers`
- **CORE-ANIMATION-015** — When a value is a `ChainedFunctionValue`, the animation shall pass it the previous keyframe's tween, or in a timeline the latest sibling tween of the same timeline on that property.
  - Tests: `FunctionBasedValuesTests.ChainedFunctionValuesInKeyframesReadThePreviousKeyframeEndValue`, `FunctionBasedValuesTests.ChainedFunctionValuesOnTimelineChildrenReadThePreviousChildEndValue`
- **CORE-ANIMATION-016** — When a duration or delay resolves to something that is not a number, the animation shall read it as 0.
  - Tests: untested
- **CORE-ANIMATION-017** — When the animation's `Ease` is a `Spring`, every tween's duration shall be the spring's settling duration whatever `Duration` says, and the spring shall report its completion to this animation.
  - Tests: `SpringTests.SpringEaseOverridesTheAnimationDurationParameter`, `SpringTests.SpringEaseParametersAffectTheAnimationDuration`
- **CORE-ANIMATION-018** — When a tween's own ease is a `Spring`, that tween's duration shall be the spring's settling duration.
  - Tests: `SpringTests.SpringEaseOverridesTheTweenDurationParameter`
- **CORE-ANIMATION-019** — When `PlaybackEase` is set and `Ease` is not, the tweens shall default to linear, while a tween that declares its own ease keeps it.
  - Tests: `KeyframesTests.ArrayKeyframesWithPlaybackEase`
- **CORE-ANIMATION-020** — While `PlaybackEase` is set, the animation shall ease its whole playhead, delays included, through it.
  - Tests: `ParametersTests.PlaybackEaseOnAnimation`

### Values

- **CORE-ANIMATION-021** — When a property is given a bare value, its tween shall run from the target's current value to that value.
  - Tests: `ValuesTests.TweenEndValueTypes`, `TweensTests.SingleTweenTimings`
- **CORE-ANIMATION-022** — When a `TweenParams` has a `From` and no `To`, its tween shall run from `From` to the target's current value.
  - Tests: `ValuesTests.FromAndFromToValues`
- **CORE-ANIMATION-023** — When a property's value is an array of plain values, one value shall be a to value, two a from/to pair, and more than two a from/to first keyframe followed by one keyframe per remaining value.
  - Tests: `KeyframesTests.AnArrayOfOneRawValueIsASimpleValue`, `KeyframesTests.AnArrayOfTwoRawValuesBecomesFromToValues`, `KeyframesTests.TheFirstOfMoreThanTwoRawValuesIsUsedAsAFromValue`, `ValuesTests.FromToValuesShorthand`
- **CORE-ANIMATION-024** — When a to value carries a relative operator (`+=`, `-=`, `*=`), it shall apply to the tween's from value; a relative from value shall apply to the previous sibling's to value, or else to the target's original value.
  - Tests: `ValuesTests.RelativeValuesWithOperators`, `ValuesTests.RelativeFromValues`, `ValuesTests.RelativeFromToValues`, `ValuesTests.RelativeValuesInsideKeyframes`
- **CORE-ANIMATION-025** — When a from value has no unit, it shall take the unit of the previous sibling, or else of the target's original value; when the two ends' units still differ, the from value shall be converted to the to value's unit (CORE-VALUES).
  - Tests: `UnitsTests.ShouldMatchAnyUnitsFromOriginalValues`, `UnitsTests.ConvertsAngleUnitsAgainstTheOriginalValue`, `KeyframesTests.ArrayKeyframesInheritUnitsFromTheKeyframeBeforeThem`
- **CORE-ANIMATION-026** — When the two ends are of different kinds, the animation shall promote the simpler end: a number to every channel of a complex value, a number to a unit value, and a non-color to a color with zeroed channels.
  - Tests: untested
- **CORE-ANIMATION-027** — When two complex ends have different numbers of channels, the shorter one shall be padded with zeros and take the longer one's strings.
  - Tests: untested
- **CORE-ANIMATION-028** — While the animation is a timeline child, a tween without a from value whose previous sibling belongs to the same timeline shall start from that sibling's end value.
  - Tests: `TimelinesTests.TimelineValues`, `TimelineCallbacksTests.TimelineMixSetAndAdd`
- **CORE-ANIMATION-029** — When the property is discrete, its tween shall run one channel from 0 to 1 and write the from or to object whole, switching by the `TweenParams.Discrete` rule or else the property's own rule.
  - Tests: `DiscreteValuesTests.ABoolSwitchesHalfwayByDefault`, `DiscreteValuesTests.EachRuleSwitchesAtItsOwnPoint`
- **CORE-ANIMATION-030** — When a discrete property is given `Composition.Additive`, the constructor shall throw `ArgumentException`.
  - Tests: `DiscreteValuesTests.AdditiveThrows`
- **CORE-ANIMATION-031** — When an `Inertia` or `Projectile` is a keyframe or part of a from/to pair, or targets a color or complex property, the constructor shall throw `ArgumentException`.
  - Tests: `InertiaTests.AnInertiaInAKeyframeArrayThrows`, `InertiaTests.AColorPropertyThrows`
- **CORE-ANIMATION-032** — When a `TweenParams` has a `TextModifier`, the tween shall write the string it returns for the current number instead of the composed value.
  - Tests: `FunctionBasedValuesTests.TextModifiersWriteStringsAndSettleOnTheValueAtOne`

### Timing

- **CORE-ANIMATION-033** — The animation shall start each tween at its delay after the previous keyframe's end, apply the animation delay to the first keyframe only, and move the earliest first-tween start out of the tweens into its own `Delay`, so `Duration` excludes it.
  - Tests: `TweensTests.SingleTweenTimings`, `TweensTests.KeyframesTweenTimings`, `AnimationsTests.TweensTimingInheritance`, `KeyframesTests.TheFirstKeyframeDelayIsTransferredToTheAnimationDelay`
- **CORE-ANIMATION-034** — The animation's `IterationDuration` shall be the latest tween end, never below `Constants.MinValue` once the delay is moved out, and its `Duration` the iterations with their loop delays, or `Constants.MinValue` when that is 0 or NaN.
  - Tests: `AnimationsTests.TweensTimingInheritance`, `ParametersTests.ZeroDurationAnimationWithInfiniteLoop`, `ParametersTests.ZeroDurationAnimationWaitsOutItsDelay`
- **CORE-ANIMATION-035** — When a tween's duration is 0 or NaN, the animation shall give it `Constants.MinValue`, so it writes its end value on its first render.
  - Tests: `ParametersTests.ZeroDurationAnimation`, `AnimationsTests.AnimateTheProgressOfAnAnimationWithZeroDurationTweens`
- **CORE-ANIMATION-066** — When an animation with a `Constants.MinValue` duration has a `Delay`, it shall hold its start values through the delay and land, firing `OnComplete` once, when the delay ends; without a delay it lands synchronously (CORE-TIMER-038).
  - Tests: `ParametersTests.ZeroDurationAnimationWaitsOutItsDelay`, `ParametersTests.ZeroDurationAnimation`
- **CORE-ANIMATION-036** — The animation shall round tween start, end and duration times to 12 decimals.
  - Tests: `KeyframesTests.ArrayBasedKeyframesWithFloatingPointDurations`, `ValuesTests.RendersNextKeyframeAtExactBoundaryWhenOffsetHasFloatRoundingDrift`
- **CORE-ANIMATION-037** — The animation shall place each tween's absolute start and end at its own offset plus its parent's offset plus the tween's local times.
  - Tests: `IntrospectionTests.TweenSpanGettersMatchTheTimelinePosition`

### Rendering

- **CORE-ANIMATION-038** — When the animation renders, each live tween shall write `Modifier(lerp(from, to, Ease(t)))`, `t` being its clamped time since its start over its duration.
  - Tests: `AnimationsTests.ValuesEndAtTheirCorrectEndPositionWhenSeeked`, `AnimationsTests.ValuesEndAtTheirCorrectStartPositionWhenSeekedInReverse`, `TweensTests.SimpleTweenEase`
- **CORE-ANIMATION-039** — The modifier shall apply to every interpolated number before it is written: plain numbers, unit values, each channel of a complex value and each channel of a color.
  - Tests: `ParametersTests.Modifier`
- **CORE-ANIMATION-040** — The animation shall write a number value unrounded, and round unit and complex values to `Globals.Precision` (4) decimals on frames strictly between progress 0 and 1.
  - Tests: untested
- **CORE-ANIMATION-041** — When a frame wrote at least one tween at a current time above 0, the animation shall invoke `OnRender` once after the tweens, unless callbacks are muted.
  - Tests: `CallbacksTests.OnRenderOnAnimation`
- **CORE-ANIMATION-042** — When `Autoplay` is false and a tween has an explicit from value, construction shall write the from values and invoke `OnRender` once.
  - Tests: `CallbacksTests.OnRenderWithAutoplayFalse`
- **CORE-ANIMATION-043** — The animation shall frame-throttle rendering by its `FrameRate` and scale it by its `PlaybackRate` as any timer does.
  - Tests: `ParametersTests.FrameRateThrottlesAnimationRendering`, `ParametersTests.PlaybackRateOnAnimation`, `AnimationsTests.AdvancesByOneFrameImmediatelyAfterBeingPlayed`

### Set

- **CORE-ANIMATION-044** — When `Presto.Set` is called, the animation shall build every tween as a from/to pair of the given value with a `Constants.MinValue` duration and `Composition.Overlap` unless set, and start at once so the values are written before it returns. A function value is resolved once per target, and one that returns an array (a from/to pair or keyframes) sets its last item.
  - Tests: `UtilsTargetsTests.SetObjectProperties`, `UtilsTargetsTests.SetWritesTheToValueOfAFunctionReturningAFromToPair`, `UtilsTargetsTests.SetObjectPropertiesToSpecificUnit`, `DiscreteValuesTests.SetWritesABoolAndAnEnum`
- **CORE-ANIMATION-045** (inherited) — When `Presto.Set` is called, it shall not raise the engine's `TimerCreated`.
  - Tests: untested

### Refresh

- **CORE-ANIMATION-046** — When `Refresh` is called, every tween with a function-based from or to value shall re-evaluate it against the target's current state.
  - Tests: `ControlsTests.RefreshAnAnimation`, `ControlsTests.RefreshAnAnimationFromValue`, `ControlsTests.RefreshAnAnimationFromValueInObjectForm`, `ControlsTests.RefreshAnAnimationWithFromToValues`, `DiscreteValuesTests.RefreshReReadsAFunctionValue`
- **CORE-ANIMATION-047** — When a refreshed tween has a function-based to value and no function-based from value, its from value shall be re-read from the target, and a relative to value re-applied against it.
  - Tests: `ControlsTests.RefreshAnAnimationWithRelativeValues`
- **CORE-ANIMATION-048** — When a refreshed from value's unit differs from the tween's, it shall be converted to the tween's unit.
  - Tests: `ControlsTests.RefreshAnAnimationWithFromToUnitConversionAndRelativeOperator`
- **CORE-ANIMATION-049** — `Refresh` shall leave tweens without function-based values, all timings and the playhead untouched.
  - Tests: `ControlsTests.RefreshAnAnimationWithRelativeValues`, `CallbacksTests.RefreshInsideAnOnLoopOnAnimation`
- **CORE-ANIMATION-050** — When `Refresh` is called on an animation whose duration is `Constants.MinValue`, it shall restart the animation so the refreshed values are written once.
  - Tests: untested

### Revert

- **CORE-ANIMATION-051** — The animation shall capture each property's original value from the target when it first reads it, 0, `""` and `null` included (CORE-TARGETS "Revert restores falsy originals").
  - Tests: `ControlsTests.RevertAnAnimation`, `ObjectTargetAdapterTests.RevertRemovesADictionaryKeyTheAnimationAdded`, `ObjectTargetAdapterTests.RevertRestoresANullMember`
- **CORE-ANIMATION-052** — When `Revert` is called, the animation shall seek to 0 without callbacks, cancel, and write every captured original value back through its accessor's `Revert`.
  - Tests: `ControlsTests.RevertAnAnimation`, `DiscreteValuesTests.RevertRestoresTheOriginalValue`, `InertiaTests.RevertRestoresTheStart`
- **CORE-ANIMATION-053** — When an animation that replaced earlier ones is reverted, the property shall return to the value captured before the first of them.
  - Tests: `ScopeTests.ReplacedAnimationsDoNotAccumulateAndRevertStillRestoresTheOriginal`

### Stretch and awaiting

- **CORE-ANIMATION-054** — When `Stretch` is called, the animation shall scale every tween's durations, delay, start and current time by the new over the old duration.
  - Tests: `ControlsTests.StretchAnAnimation`, `ControlsTests.StretchScalesKeyframeDelaysProportionally`, `ControlsTests.StretchScalesGlobalDelayAndKeyframeDelaysProportionally`
- **CORE-ANIMATION-055** — When a top-level animation is stretched, it shall keep its offset and rebuild its tweens' absolute times from their scaled local times; a timeline child shall scale them.
  - Tests: `AnimatablesTests.RetargetingAZeroDurationPropertyLongAfterTheEngineStartedStillEases`
- **CORE-ANIMATION-056** — When stretched to 0, every tween shall collapse to `Constants.MinValue`, and a later stretch shall give every tween the same duration.
  - Tests: `ControlsTests.StretchAnAnimation`
- **CORE-ANIMATION-057** — `Then` and `await` shall complete with the animation itself when it completes (CORE-CALLBACKS).
  - Tests: `PromisesTests.ThenOnAnimation`, `PromisesTests.AwaitAnAnimation`

### Tween introspection

- **CORE-ANIMATION-058** — `Tween.Delay`, `StartTime` and `Duration` shall report the tween's local timing, and `AbsoluteStart`/`AbsoluteEnd` its engine-time span.
  - Tests: `IntrospectionTests.TweenSpanGettersMatchTheTimelinePosition`
- **CORE-ANIMATION-059** — `Tween.IsOverridden` shall turn true once a newer tween on the same property takes it over.
  - Tests: `IntrospectionTests.IsOverriddenFlipsWhenANewerAnimationTakesTheProperty`
- **CORE-ANIMATION-060** — `GetFrom`, `GetTo` and `GetCurrent` shall return copies: the number, the channels of a color or complex value, and the value composed as the string a target would receive.
  - Tests: `IntrospectionTests.GetCurrentMatchesTheTarget`
- **CORE-ANIMATION-061** (inherited) — When a number or unit tween has `Composition.Overlap`, `GetCurrent` shall read as its from value, since such a tween does not track its number.
  - Tests: untested

### Reduced motion

- **CORE-ANIMATION-062** — While the owner engine's `ReducedMotion` is set and `WhenReduced` is given, the animation shall play the parameters with `WhenReduced`'s properties and keyframes, each setting and callback it sets overriding these, and its own `WhenReduced` ignored.
  - Tests: `ReducedMotionTests.WhenReducedReplacesThePropertiesAndKeepsTheCallbacks`, `ReducedMotionTests.WhenReducedKeepsTheStaggerAndTheSettingsItDoesNotSet`, `ReducedMotionTests.WhenReducedOverridesTheEaseItSets`
- **CORE-ANIMATION-063** — While `ReducedMotion` is clear, `WhenReduced` shall be ignored.
  - Tests: `ReducedMotionTests.WhenReducedIsIgnoredWhileTheFlagIsClear`
- **CORE-ANIMATION-064** — While the owner engine's `ReduceAll` is set, the animation shall give every tween a `Constants.MinValue` duration and no delay and play one iteration, except for a reduced form and for `Presto.Set`.
  - Tests: `ReducedMotionTests.ReduceAllWritesTheEndValueOnTheFirstFrameAndCompletes`, `ReducedMotionTests.ReduceAllEndsAKeyframedPropertyOnItsLastKeyframe`, `ReducedMotionTests.ReduceAllLeavesADictionaryReducedFormAlone`
- **CORE-ANIMATION-065** — While `ReduceAll` is set, an `Inertia` or `Projectile` tween shall resolve its end before collapsing, so the property lands on the physical end.
  - Tests: `InertiaTests.ReduceAllLandsOnTheSnappedClampedTargetInOneFrame`, `ProjectileTests.ReduceAllLandsOnTheEndAtOnce`

## Invariants

- Every tween belongs to exactly one animation's tween list; its `Parent` never changes.
- A tween's `Duration` is never 0: the smallest is `Constants.MinValue` (1e-11).
- An animation's `Duration` is `Constants.MinValue` or positive; it is never 0 or NaN.
- After construction, the first-starting tween of the animation has a local `StartTime` of 0.

## Edge cases

- A target count above 1000 silently switches the default composition to `Overlap`, so a large group never overrides an earlier animation of the same properties.
- A keyframe array of length 1 is one plain keyframe; a `TweenParams` array of any length is keyframes even at length 2.
- `Refresh` does not re-run function-based durations, delays or eases.
- Animating a `Timer` or `Animation` as a target (its `CurrentTime` or `Progress`) works through the plain object adapter (`AnimationsTests.AnimateTheProgressOfAnAnimationWithZeroDurationTweens`).
- The indexer's dictionary is shared by a `with` copy; nothing mutates it after construction.

## Threading and lifetime

- Constructed and driven on the owner engine's thread: `Engine.Current` at construction, or the parent timeline's engine. Construction uses thread-static scratch values.
- Once completed, the engine detaches the animation's tweens from composition (CORE-COMPOSITION); `Restart`, `Reset` or `Seek` re-attach them. An idle animation holds its targets strongly until it is released.
- After everything it ran completes, the engine holds no tickable (`LeaksTests.EngineShouldNotContainAnyActiveTickable`).

## Reduced motion

- `WhenReduced` replaces the form while `ReducedMotion` is set; `ReduceAll` collapses every tween to its end value in one frame, keyframes and springs included. See CORE-REDUCED.

## Allocation

- Construction allocates the tweens, their channel arrays and closures for function-based values; it reuses thread-static `DecomposedValue` scratch.
- Rendering a number or unit tween allocates nothing beyond what the accessor's write costs; complex and color strings are composed only when a target needs a string.
- `Tweens` enumerates without allocating. `GetFrom`, `GetTo` and `GetCurrent` allocate and are meant for tools.

## Provenance

- Original values are captured even when 0, empty or null, so `Revert` restores them; upstream skips falsy originals.
- `Stretch` of a top-level animation keeps its offset and rebuilds absolute times: a top-level offset is the engine time the timer was created at, and multiplying it by the factor of a stretch from a `Constants.MinValue` duration lost the precision a frame needs, so a release tween started late or never.
- Color channels interpolate with premultiplied alpha, so a fade from a transparent color does not take on its RGB mid-way (CORE-COLORS).
- Discrete properties tween instead of throwing, so `bool` and enum properties can be animated (CORE-TARGETS).
- A zero-duration animation with a delay waits it out, where upstream `resume()` renders it once inside the delay (see "A zero-duration animation honors its delay").
- `TextModifier`, `Inertia`/`Projectile` values, `WhenReduced` and `ReduceAll` have no upstream counterpart.
- No DOM-specific tween types, `persist` or WAAPI conversion: there is no DOM, and upstream's `persist` exists only on its WAAPI path.

## Decisions

- **Modifiers take a number; per-target modifiers come from a function value.** `Modifier` and `TextModifier` are `Func<double, …>`, as upstream's `modifier` is; a target that needs its own (counters with different precision in one call) gets it from a function-based value returning a `TweenParams` (CORE-ANIMATION-014). Reason: that form already carries every tween setting per target, so a second per-target spelling for the modifiers would duplicate it at the cost of a union on four parameter types. Rejected: a `(target, index, total)` form of `Modifier`/`TextModifier`.
- **Upstream's linked lists are kept, and tweens hang off the timer.** Engine children, timeline children and tweens use upstream's intrusive linked lists, and the tween list lives on `Timer`. Reason: an intrusive list links and unlinks in O(1) and ticks without allocating, and keeping upstream's structure lets its regression suites port one-to-one; on `Timer`, the renderer and the additive animation work on any timer.
- **Params are init-only records with a key indexer.** `AnimationParams` mixes property tweens (through its `PropertyKey` indexer, CORE-TARGETS) and settings in one initializer, and a derived params type can add a typed indexer; `TweenParams` is a sealed class. Reason: records let `Timeline.Set` override with `with`, and indexer initializers are the only C# syntax that mixes typed members and arbitrary keys in one initializer, mirroring the JS object literal. Rejected: a collection-initializer `Add` design, which cannot mix with settings.
- **`Presto.Set` sets the last value of a function returning an array.** Upstream `utils.set` wraps a function value as a from/to pair of itself, so a function that returns a from/to pair or keyframes nests an array in each end and fails to decompose; `Set` resolves the function once and sets the array's last item, the value the animation would end on (CORE-ANIMATION-044). Reason: a motion-state motion (BRIDGE-MOTION) that morphs from the current value applies through `Set` when its element attaches already in the state.
- **Function values receive the group's target list.** Reason: it lets `Stagger` resolve auto-grids from the targets.
- **Dictionary params take `WhenReduced`; timers and timelines do not.** `AnimationParams.WhenReduced` has the builder's semantics: under the flag its properties replace these and the settings it sets override them, and a nested `WhenReduced` is ignored. Reason: a timer animates nothing, and each timeline child picks its own form.
- **A zero-duration animation honors its delay.** `Resume` renders a zero-duration timer at once only when it has no delay; otherwise it waits the delay out and lands (CORE-ANIMATION-066, CORE-TIMER-038). Reason: a delay is part of the schedule (stagger, orchestration). Upstream's render lands inside the delay and never links the timer into the engine, so whenever float noise left the duration at or below `Constants.MinValue` (a 50 ms delay, not a 100 ms one) the animation held its start for good, which broke zero-duration motion-state returns under `StaggerChildren`/`DelayChildren`. Rejected: keeping upstream and documenting a 1 ms duration as the workaround.
- **`persist` is not ported.** Reason: in anime.js 4.5.0 it exists only on the WAAPI path.
- **Inertia is a to-value, shaped like Spring is an ease.** See CORE-INERTIA.
- **The builder is the documented, typed spelling; the indexer stays beside it.** See CORE-BUILDER.
- **Introspection is read-only and allocation-free.** See CORE-TIMER.

## Non-goals

- No per-tween interpolation space; color paths go through keyframes (CORE-KEYFRAMES).
- No string-keyed type safety: property names are checked at run time by the adapters.

## Open items

- `Id` defaults, `Presto.Set` not raising `TimerCreated`, kind homogenization, complex padding, the 1000-target composition switch, string rounding precision, a function value returning `TweenParams`, and the setter restart in `Refresh` have no test.
- `GetCurrent` on a `Composition.Overlap` number tween returns the from value rather than the rendered one (documented on the member); tools reading it get a stale value.
