---
id: BRIDGE-FOLLOW
title: Value-driven animation and motion value sources
package: Monaco.Presto.AvaloniaBridge
namespaces:
  - Monaco.Presto.AvaloniaBridge
sources:
  - Monaco.Presto.AvaloniaBridge/Follow.cs
  - Monaco.Presto.AvaloniaBridge/FollowExtension.cs
  - Monaco.Presto.AvaloniaBridge/FollowParams.cs
  - Monaco.Presto.AvaloniaBridge/Bindings
  - Monaco.Presto.AvaloniaBridge/MotionValueSource.cs
  - Monaco.Presto.AvaloniaBridge/PrestoAvalonia.cs
  - Monaco.Presto.AvaloniaBridge/Events/ScrollContainer.cs
types:
  - Follow
  - FollowExtension
  - FollowParams
tests:
  - FollowerTests
  - FollowBindingTests
  - FollowBindingPlumbingTests
  - FollowMarkupTests
  - FollowTests
  - MotionValueSinkTests
  - MotionValueSourceTests
  - MotionValueCodecTests
  - AllocationTests
upstream: []
depends:
  - CORE-MOTIONVALUE
  - CORE-ANIMATABLE
  - CORE-SPRING
  - CORE-SCOPE
  - BRIDGE-TRANSFORMS
  - BRIDGE-EFFECTS
  - BRIDGE-SHAPES
  - BRIDGE-CODECS
  - BRIDGE-TOKENS
  - BRIDGE-HELPERS
  - BRIDGE-SCROLLOBSERVER
  - BRIDGE-DRAGGABLE
---

# Value-driven animation and motion value sources

## Purpose

Makes a bound property ease to each new value instead of jumping to it (≈ Compose `animate*AsState`, Flutter `AnimatedContainer`): `{presto:Follow {Binding …}}` in XAML, `Follow.Bind` in code, and `Follow.*` attached shorthands for the render transform, blur and a shape's drawn stroke. It also carries the core `MotionValue` graph (CORE-MOTIONVALUE) to and from controls: bridge sources (scroll, pointer, property, draggable, observer) feed values in, and the `Follow` sinks write them out. Also specifies `PrestoAvalonia.ScrollValue`, `PointerValue` and `ValueOf`, `ScrollObserver.ProgressValue` and `Draggable.XValue`/`YValue`.

## Requirements

### Timing resolution

- **BRIDGE-FOLLOW-001** — The follower shall take its timing from exactly one source, in this precedence: `Builder` (its duration and ease only), then `Spring`, then `Bounce` (with `Duration` as perceived duration, default 500 ms), then `Duration`/`Ease`, then `Token` (`Medium` duration and `Ease`), and otherwise `Springs.Gentle`.
  - Tests: `FollowerTests.EachTimingSourceResolves`, `FollowMarkupTests.TheTimingAttributesAreHonored`
- **BRIDGE-FOLLOW-002** (inherited) — When `Builder` is combined with `Duration`, `Ease`, `Spring` or `Bounce`, when `Spring` is combined with `Bounce`, `Duration` or `Ease`, when `Bounce` is combined with `Ease`, or when a spring `Ease` is combined with `Duration`, the follower shall throw `ArgumentException` at construction.
  - Tests: `FollowerTests.ConflictingTimingSourcesThrow`
- **BRIDGE-FOLLOW-003** (inherited) — When `Spring` names no preset and is not `"bounce,duration"` (including a single number, more than two parts, or a preset followed by a duration), the follower shall throw `FormatException` at construction naming the presets and the `"bounce,duration"` form.
  - Tests: `FollowerTests.ConflictingTimingSourcesThrow`, `FollowerTests.SpringPresetsParseByNameOrBounceAndDuration`
- **BRIDGE-FOLLOW-004** (inherited) — When a `Builder` or `WhenReduced` builder has a non-numeric `Duration`, the follower shall throw `NotSupportedException`.
  - Tests: untested
- **BRIDGE-FOLLOW-059** — When `FollowParams.Spring`, trimmed, names a preset ignoring case (`gentle`, `snappy`, `bouncy`, `stiff`), the follower shall use a new spring of that `Springs` preset (CORE-SPRING).
  - Tests: `FollowerTests.SpringPresetsParseByNameOrBounceAndDuration`
- **BRIDGE-FOLLOW-060** — When `FollowParams.Spring` is two comma-separated numbers in the invariant culture, surrounding spaces allowed, the follower shall use `new Spring(bounce: first, duration: second)`.
  - Tests: `FollowerTests.SpringPresetsParseByNameOrBounceAndDuration`, `FollowerTests.EachTimingSourceResolves`
- **BRIDGE-FOLLOW-005** — While the resolved ease is a `Spring`, a retarget shall start from the displayed value and keep its current velocity, per channel for multi-channel types.
  - Tests: `FollowerTests.ASpringRetargetKeepsTheVelocityOfADouble`, `FollowerTests.ASpringRetargetKeepsTheVelocityOfEachThicknessChannel`, `FollowBindingTests.AMidFlightRetargetKeepsTheVelocity`, `FollowTests.AMidFlightRetargetKeepsTheVelocity`
- **BRIDGE-FOLLOW-006** — While the resolved ease is not a spring, a retarget shall restart from the displayed value with the full duration.
  - Tests: `FollowerTests.ADurationEaseRestartsFromTheDisplayedValueWithItsFullDuration`

### Follower: easing and snapping

- **BRIDGE-FOLLOW-007** — The follower shall convert each incoming value to the property type as a binding would (`TypeUtilities.TryConvert`, invariant culture), and a value that does not convert shall become `UnsetValue`.
  - Tests: `FollowerTests.AValueIsConvertedToThePropertyType`, `MotionValueSinkTests.BindIsAtLocalValueOverAStyleAndConvertsToThePropertyType`
- **BRIDGE-FOLLOW-008** — The first value shall snap, unless `AnimateInitial` is set, in which case it shall ease from the property's value before the binding.
  - Tests: `FollowerTests.TheFirstValueSnaps`, `FollowerTests.AnimateInitialEasesTheFirstValueFromThePreBindingValue`, `FollowBindingTests.TheFirstValueSnapsAtLocalValue`, `FollowBindingTests.AnimateInitialEasesFromThePreBindingValue`
- **BRIDGE-FOLLOW-009** (inherited) — When `AnimateInitial` is set but the pre-binding value is not finite (an unset `Width` reads NaN), the first value shall snap.
  - Tests: `FollowBindingTests.AnimateInitialSnapsWhenThePreBindingValueIsUnset`
- **BRIDGE-FOLLOW-010** — The follower shall ease `double`, `float`, `int` (shown as whole numbers), `Color`, `IBrush`, `Thickness`, `CornerRadius`, `Point`, `Size`, `Rect`, `Vector`, `RelativePoint` and `BoxShadows`, raising one change per frame and landing exactly on the target.
  - Tests: `FollowerTests.AChangeEasesOneEventPerFrameAndLandsExactly`, `FollowerTests.AColorAndABrushEaseAndLandExactly`, `FollowerTests.EachChannelTypeEasesAndLandsExactly`, `FollowerTests.AnIntEasesThroughWholeNumbers`, `FollowBindingTests.ColorBrushThicknessAndCornerRadiusAnimate`
- **BRIDGE-FOLLOW-011** — When the old and new values have different channel types, either is of a non-easable type (string, enum, `bool`, `null`, `UnsetValue`, `BindingNotification`), or their codec decompositions differ in kind, channel count or string parts (a solid brush to a gradient), the follower shall show the new value at once.
  - Tests: `FollowerTests.ValuesThatCannotBeEasedPassThrough`, `FollowBindingTests.ValuesThatCannotBeInterpolatedPassThrough`
- **BRIDGE-FOLLOW-012** — While `PrestoDefaults.ReducedMotion` is set, each value shall snap, unless `WhenReduced` is given, in which case it shall ease with that builder's duration and ease.
  - Tests: `FollowerTests.ReducedMotionSnapsUnlessWhenReducedGivesATiming`, `FollowBindingTests.ReducedMotionSnaps`
- **BRIDGE-FOLLOW-013** (inherited) — When `ReducedMotion` changes between two values, the follower shall rebuild its animation with the timing for the new state from the displayed value.
  - Tests: untested
- **BRIDGE-FOLLOW-014** — The follower shall drive its own core `Animatable` over an internal holder and never register the control's property with Presto, so an animation of the same property at `Animation` priority layers over the followed value and reverting it shows the latest followed value.
  - Tests: `FollowBindingTests.AnAnimationOfTheSamePropertyLayersOverTheFollowAndRevertsToIt`, `FollowBindingPlumbingTests.AnAnimationLayersOverTheReturnedBindingAndRevertsToItsLatestValue`
- **BRIDGE-FOLLOW-058** — When `null` arrives for a value-typed property (a recycled row's value on a `double`), the follower shall show `UnsetValue` at once, so the property falls back to its lower-priority value.
  - Tests: `FollowerTests.ValuesThatCannotBeEasedPassThrough`

### `{presto:Follow}` and `Follow.Bind` over a binding

- **BRIDGE-FOLLOW-015** — The wrapped binding shall be bound to a hidden `object`-typed companion attached property (one per animated property) on the target, so `DataContext`, `ElementName`, `RelativeSource`, `TemplatedParent`, `TemplateBinding`, converters and `FallbackValue` resolve as on the property itself.
  - Tests: `FollowBindingPlumbingTests.TheWrappedBindingResolvesOnTheCompanionAsItWouldOnTheProperty`, `FollowBindingTests.ARelativeSourceAncestorResolvesOnTheTarget`, `FollowMarkupTests.ElementNameAndAConverterResolveInTheWrappedBinding`, `FollowMarkupTests.InsideAControlTemplateItFollowsTheTemplatedParent`
- **BRIDGE-FOLLOW-016** — The output shall be a one-way binding at `LocalValue` priority, overriding lower-priority style values, inside templates as on attributes.
  - Tests: `FollowBindingTests.AStyleValueAtALowerPriorityIsOverridden`, `FollowBindingPlumbingTests.AReturnedObservableBindingLandsAtLocalValueOverAStyleAndInsideATemplateLikeABareBinding`
- **BRIDGE-FOLLOW-017** — When the wrapped binding's explicit `Mode` (read from `CompiledBinding`, `ReflectionBinding`, `MultiBinding` or `TemplateBinding`) is `TwoWay` or `OneWayToSource`, `Follow.Bind` and `ProvideValue` shall throw `ArgumentException`; `Default` shall be accepted.
  - Tests: `FollowBindingTests.AWriteBackModeThrows`
- **BRIDGE-FOLLOW-018** — When `ProvideValue` runs in a style `Setter`, it shall throw `InvalidOperationException` whose message points at `ControlTemplate`/`DataTemplate`.
  - Tests: `FollowMarkupTests.AStyleSetterThrowsAndPointsAtTemplates`, `FollowBindingPlumbingTests.InAStyleSetterTheTargetIsTheSharedSetterAndItsValueClrProperty`
- **BRIDGE-FOLLOW-019** — When `ProvideValue` has no `AvaloniaObject` target with an `AvaloniaProperty`, it shall throw `InvalidOperationException` whose message points at `Follow.Bind`.
  - Tests: `FollowMarkupTests.AStyleSetterThrowsAndPointsAtTemplates`
- **BRIDGE-FOLLOW-020** — When the target's `DataContext` changes (`{presto:Follow}` and `Follow.Bind` over a `BindingBase`), the next value shall snap; a `Background` dispatcher post shall clear the flag if the new item left the value unchanged, so a later change eases.
  - Tests: `FollowBindingTests.ADataContextSwapSnapsAndAnUnchangedValueStillLetsTheNextChangeAnimate`, `FollowBindingTests.ARecycledContainerInAVirtualizedListSnapsToItsNewItem`, `FollowMarkupTests.InAnItemTemplateEachRowSnapsToItsItemAnimatesItsChangesAndSnapsToANewItem`
- **BRIDGE-FOLLOW-021** (inherited) — `Follow.Bind<T>` over an `IObservable<T>` shall not watch `DataContext`, so it never snaps for a new item.
  - Tests: untested
- **BRIDGE-FOLLOW-022** — When the target detaches from the visual tree, the follower shall land on the latest target and stop ticking; a value arriving while detached shall snap.
  - Tests: `FollowBindingTests.DetachLandsOnTheLatestTargetAndReattachSnaps`, `FollowerTests.StopLandsOnTheLatestTargetAndStopsTicking`
- **BRIDGE-FOLLOW-023** — When the returned `IDisposable` (or the binding expression) is disposed, the follow shall stop ticking, remove the companion binding and stop following the source, so the property falls back to its lower-priority value; a `ClearValue` or local write shall not end it.
  - Tests: `FollowBindingTests.DisposingRestoresThePropertyAndStopsTicking`, `FollowBindingPlumbingTests.OnlyRemovingTheBindingExpressionUnsubscribesTheFollower`

### Motion value sinks

- **BRIDGE-FOLLOW-024** — When `Follow.Bind` gets a `MotionValue`, no options and a `StyledProperty<double>`, it shall write each flushed value unboxed at `LocalValue`, at most once per frame however many upstream sources changed.
  - Tests: `MotionValueSinkTests.BindWritesOncePerFrameHoweverManySourcesChanged`, `AllocationTests.AMotionValueBoundWidthTicksWithinBudget`
- **BRIDGE-FOLLOW-025** — When `Follow.Bind` gets a `MotionValue` for another property type, or a `MotionObjectValue`, without options, it shall write each flushed value converted to the property type (ints rounded) with no easing.
  - Tests: `MotionValueSinkTests.BindIsAtLocalValueOverAStyleAndConvertsToThePropertyType`, `MotionValueSinkTests.AnObjectValuedCellBindsTheSameWay`
- **BRIDGE-FOLLOW-026** — When `Follow.Bind` gets a motion value with options, it shall ease to each flushed value through the follower with that timing, snapping to the first.
  - Tests: `MotionValueSinkTests.BindStopsListeningWhileDetachedAndSnapsOnReattach`
- **BRIDGE-FOLLOW-027** — When a `MotionValue` or `MotionObjectValue` arrives through `{presto:Follow}`'s wrapped binding, it shall listen to that value: snapping to its current value, then passing flushed values through with `Duration=0` and easing with any other timing.
  - Tests: `MotionValueSinkTests.TheXamlExtensionPassesAViewModelValueThroughAtZeroDurationAndSmoothsItWithASpring`
- **BRIDGE-FOLLOW-028** — When a new motion value arrives on the companion (a `DataContext` swap), the sink shall stop listening to the old one and snap to the new one.
  - Tests: `MotionValueSinkTests.ADataContextSwapRebindsToTheNewItemsValueAndSnaps`
- **BRIDGE-FOLLOW-029** — While a zero-duration motion value sink runs under `ReducedMotion` with a `WhenReduced` timing, it shall ease with that timing rather than pass values through.
  - Tests: untested
- **BRIDGE-FOLLOW-030** — While its target is detached, every motion value sink shall stop listening; on reattach it shall resubscribe and show the latest value at once.
  - Tests: `MotionValueSinkTests.BindStopsListeningWhileDetachedAndSnapsOnReattach`
- **BRIDGE-FOLLOW-031** — A motion value sink shall layer under an animation of the same property and show the latest value when that animation reverts.
  - Tests: `MotionValueSinkTests.AnAnimationOfThePropertyLayersOverTheBindingAndRevertsToTheLatestValue`
- **BRIDGE-FOLLOW-032** — When a motion value sink's `IDisposable` is disposed, it shall stop writing.
  - Tests: `MotionValueSinkTests.DisposingStopsTheWrites`

### `Follow.*` shorthands

- **BRIDGE-FOLLOW-033** — `Follow.X/Y/Scale/ScaleX/ScaleY/Rotate/Blur` shall ease the visual's render-transform shorthand or blur radius to each numeric value with the visual's `Follow.Timing`, landing exactly.
  - Tests: `FollowTests.XRotateScaleAndBlurAnimateToTheirBoundValuesAndLandExactly`
- **BRIDGE-FOLLOW-034** — A shorthand's first value shall snap; a shorthand has no `AnimateInitial`.
  - Tests: `FollowTests.TheFirstValueSnapsAndAUnitStringConverts`
- **BRIDGE-FOLLOW-035** (inherited) — When a shorthand gets a string, it shall parse it with `ValueParser`: `Rotate` converts any angle unit (`deg`, `rad`, `turn`…) to degrees, other shorthands accept a bare number or `px`.
  - Tests: `FollowTests.TheFirstValueSnapsAndAUnitStringConverts`
- **BRIDGE-FOLLOW-036** (inherited) — When a shorthand's value cannot be converted to a number (a non-numeric string, a unit other than `px` outside `Rotate`), the shorthand shall release its followed layer while the attached property keeps the value.
  - Tests: untested
- **BRIDGE-FOLLOW-037** (inherited) — When `Follow.Timing` changes, the new timing shall apply to every shorthand of the visual from its next value on, not to a motion in flight.
  - Tests: `FollowTests.TimingIsHonoredAndAChangeTakesEffectFromTheNextValue`
- **BRIDGE-FOLLOW-038** — The followed value of a transform or blur shorthand shall be a base layer: an animation of the same shorthand starts from it, overrides it while it holds it, and reverting the animation shows the latest followed value.
  - Tests: `FollowTests.AnAnimationOfTheSameShorthandLayersOverTheFollowAndRevertsToIt`, `FollowTests.AnAnimatedBlurLayersOverTheFollowedBlurAndRevertsToIt`, `MotionValueSinkTests.AnXAnimationOverridesTheFollowedValueAndRevertsToTheLatestOne`
- **BRIDGE-FOLLOW-039** — When a shorthand is set to `null` or cleared, it shall stop following and the shorthand shall return to identity, restoring the visual's own `RenderTransform` and `Effect` once nothing else holds them.
  - Tests: `FollowTests.ClearingRestoresTheIdentityAndTheVisualsOwnRenderTransform`
- **BRIDGE-FOLLOW-040** — While `ReducedMotion` is set, a shorthand's numeric values shall snap.
  - Tests: `FollowTests.ReducedMotionSnaps`
- **BRIDGE-FOLLOW-041** — When a shorthand's value is caused by a `DataContext` change, it shall snap.
  - Tests: `FollowTests.ADataContextSwapSnaps`
- **BRIDGE-FOLLOW-042** — When a shorthand gets a `MotionValue`, it shall write the current value at once and then each flushed value, ignoring `Follow.Timing` and reduced motion.
  - Tests: `MotionValueSinkTests.EachShorthandWritesEachFlushedValueIgnoringTheTiming`, `AllocationTests.AMotionValueFollowedXTicksWithinBudget`
- **BRIDGE-FOLLOW-043** — When a shorthand holding a `MotionValue` is set to a number, another value or `null`, it shall unsubscribe from the old value; a number then snaps as a first value.
  - Tests: `MotionValueSinkTests.AnotherValueOrNullReleasesTheSubscription`
- **BRIDGE-FOLLOW-044** — When the visual detaches, every shorthand follower shall land on its latest value and motion value subscriptions shall stop; on reattach each motion value shall resubscribe and write its latest value.
  - Tests: untested
- **BRIDGE-FOLLOW-045** — `Follow.Draw` on a `Shape` shall follow the share of the stroke drawn from its start as the `"Draw"` value `"0 p"`, clamped to 0–1, eased with `Follow.Timing` or written from a `MotionValue`.
  - Tests: `FollowTests.DrawEasesToANumber`, `FollowTests.DrawTracksAMotionValueAndReleasingRestoresTheDashes`
- **BRIDGE-FOLLOW-046** — `Follow.Draw` shall have no base layer: it writes the dashes as a `"Draw"` tween does, so the later write wins, and releasing it shall restore the dashes the shape had.
  - Tests: `FollowTests.DrawTracksAMotionValueAndReleasingRestoresTheDashes`

### Motion value sources

- **BRIDGE-FOLLOW-047** — `PrestoAvalonia.ScrollValue` shall hold the scroller's offset on the axis, rounded to whole pixels, or with `progress`, offset ÷ (extent − viewport) clamped to 0–1 (0 when nothing scrolls).
  - Tests: `MotionValueSourceTests.ScrollValuesFollowTheOffsetOnBothAxes`
- **BRIDGE-FOLLOW-048** — A `ScrollValue` shall refresh when the scroller's extent or viewport changes, and each call shall make its own value fed from the scroller's shared `ScrollContainer`, which stays alive while any value uses it.
  - Tests: `MotionValueSourceTests.ScrollProgressIsRefreshedWhenTheExtentChanges`
- **BRIDGE-FOLLOW-049** — Several scroll changes within one frame shall reach a `Changed` handler on a derived scroll value once, with the last value.
  - Tests: `MotionValueSourceTests.ASinkSeesOneChangePerFrameWhileScrollingSeveralNotches`
- **BRIDGE-FOLLOW-050** — `PrestoAvalonia.PointerValue` shall start at 0 and hold the pointer position over the control on the axis, in DIPs from its top left, or with `relativeToBounds` as a fraction of its size (0 when the size is 0), updated on `PointerEntered` and `PointerMoved`.
  - Tests: `MotionValueSourceTests.PointerValueFollowsPointerMoved`
- **BRIDGE-FOLLOW-051** — `PrestoAvalonia.ValueOf` shall hold the property's value converted with `Convert.ToDouble`, updated on every change of that property.
  - Tests: `MotionValueSourceTests.ValueOfFollowsSetValue`
- **BRIDGE-FOLLOW-052** (inherited) — When a `ValueOf` property value does not convert to `double`, `ValueOf` shall throw the `Convert.ToDouble` exception (at creation, or from the change that carries it).
  - Tests: untested
- **BRIDGE-FOLLOW-053** — When its control leaves the visual tree, or the scope that was current at creation reverts, a `ScrollValue`, `PointerValue` or `ValueOf` shall stop at its last value and not revive on reattach; a non-`Visual` `ValueOf` target stops only with its scope.
  - Tests: `MotionValueSourceTests.ScrollAndPropertyValuesStopWhenTheirControlDetaches`, `MotionValueSourceTests.PointerValueStopsWhenItsScopeReverts`
- **BRIDGE-FOLLOW-054** — `ScrollObserver.ProgressValue` shall equal the observer's `Progress` after every scroll and freeze when the observer reverts.
  - Tests: `MotionValueSourceTests.ProgressValueMatchesTheObserverAcrossItsThresholds`, `MotionValueSourceTests.ObserverAndDraggableValuesFreezeOnRevert`
- **BRIDGE-FOLLOW-055** — `Draggable.XValue`/`YValue` shall mirror `X`/`Y` rounded to the engine's precision through drag and release, and freeze when the draggable reverts.
  - Tests: `MotionValueSourceTests.DraggableValuesFollowADragAndTheReleaseSpring`, `MotionValueSourceTests.ObserverAndDraggableValuesFreezeOnRevert`

### Defaults

- **BRIDGE-FOLLOW-056** — When `Follow.Timing` is not set, the `Follow.*` shorthands shall move with `Springs.Gentle`, the follower default (Timing options default to `Springs.Gentle`).
  - Tests: untested
- **BRIDGE-FOLLOW-057** — When `ScrollValue`'s axis is not given, the value shall follow the vertical offset.
  - Tests: `MotionValueSourceTests.ScrollValuesFollowTheOffsetOnBothAxes`

## Invariants

- A follower never registers the target property with Presto composition; its only engine presence is its own `Animatable`, which is reverted whenever it lands or snaps.
- At most one write per sink per frame from a motion value (the core flush rule, CORE-MOTIONVALUE).
- Each `Follow` shorthand is one `double`, eased by one follower or written from one `MotionValue`, never both.
- Once every shorthand of a visual is released, its per-visual state and event handlers are removed.

## Edge cases

- `Follow.Bind` over a `BindingBase` validates mode and timing synchronously, so a bad call throws before anything binds.
- A recycled virtualized row gets a fresh template root (a first value); a row scrolled away is handed `null` as its `DataContext` clears, which passes through.
- The detached flag is set only by a detach, so values that arrive before the target was ever attached are not snapped by it (after the first value they ease).
- A `Follow.Timing` change rebuilds the animation on the next value from the displayed value, so velocity across a retime is not kept.
- A `Follow.Timing` builder with a non-numeric duration throws from the next shorthand value or from the `Timing` change itself.
- A `Spring` bounce outside -1..1 or duration outside the spring's range is clamped by `Spring` itself (CORE-SPRING), not rejected.
- A gradient-to-solid or stop-count-changing brush change passes through; same-shape gradients ease per stop (MotionObjectValue maps: `MotionValueCodecTests`).

## Threading and lifetime

- UI thread only. Followers use `Engine.Current` at construction; the `DataContext` flag is cleared by a `Dispatcher.UIThread` post at `Background`.
- `{presto:Follow}` lives as long as its binding expression; `Follow.Bind` until its handle is disposed or the property is rebound.
- Bridge sources register with `Engine.Current.CurrentScope` at creation and unhook on that scope's revert or on their `Visual`'s detach, whichever comes first.

## Reduced motion

- Followers snap every value unless `WhenReduced` gives a timing (`Follow.*` shorthands have no `WhenReduced`, so they always snap).
- Motion value writes are not affected: a graph states its own smoothing; a zero-duration `Follow.Bind`/`{presto:Follow}` over a motion value eases only when `WhenReduced` is given.

## Allocation

- A followed `double` follower allocates only its box per frame (≤ 24 B, `FollowerTests.AFollowedDoubleAllocatesOnlyItsBoxPerFrame`).
- `Follow.Bind` over a binding ≤ 128 B per tick (measured 104), `{presto:Follow}` ≤ 192 B (measured 160): `AllocationTests.AFollowBoundWidthTicksWithinBudget`, `AllocationTests.AnAnimatedMarkupWidthTicksWithinBudget`.
- An unboxed `MotionValue` → `double` sink and a `MotionValue`-driven `Follow.X` ≤ 96 B per tick (measured 80, Avalonia's change args).
- A `MotionObjectValue` boxes one struct per changed frame unless its codec reuses the instance.

## Deviations from anime.js

- No upstream counterpart (Compose / Flutter / Motion-inspired).

## Decisions

- **Follow is a binding wrapper written as a markup extension.** `{presto:Follow {Binding X}, …}` wraps an ordinary binding whose value is the target while the property shows the animated value. Reason: it sits where the binding already is, so there is no second attached property to keep in step, and changing which binding is animated is a one-attribute change. Rejected: Avalonia `Transitions` (`TransitionBase.Apply` is internal and the only hook lets Avalonia own duration and progress, ruling out springs, velocity across interruptions and reduced motion); a custom `BindingBase` subclass (`CreateInstance` is internal).
- **The wrapped binding resolves on a hidden companion property of the target.** One lazily registered `object`-typed attached property per target property; its change is the "value changed" event, and each value is converted to the property type, a non-convertible one becoming `UnsetValue`. Reason: `DataContext`, `ElementName`, `RelativeSource`, `TemplatedParent`, converters and `FallbackValue` resolve exactly as on the real property; conversion is needed because the `object` companion converts nothing (a XAML `FallbackValue` arrives as a string, a recycled row's as `null`).
- **The output is the follower as a binding at `LocalValue`.** The follower starts at `UnsetValue`. Reason: lower-priority styles are overridden as usual, DevTools shows a binding expression, animations at `Animation` priority layer over it and revert to it, its subscription gives the lifetime signal, and a value-typed store throws on `null`. Rejected: an INPC follower plus `Binding` (192 B per push against 160, and its conversion hid the untyped-companion problem); binding directly from `ProvideValue` (compiled XAML casts a non-binding result inside a template and throws).
- **The follower runs its own `Animatable`, outside composition.** It never registers the control's property with Presto. Reason: a `control.Animate` of the same property then neither cancels the follow nor is canceled by it, and reverting it uncovers the latest followed value rather than the identity.
- **Retargets keep velocity, per channel.** A spring retarget starts from the displayed value and keeps each channel's velocity; a duration-based ease restarts from the displayed value with its full duration. Reason: colors, thicknesses and points keep momentum too; the full-duration restart matches Compose.
- **The follower snaps when the value is not the same item changing.** The first value (unless `AnimateInitial`), a value caused by a `DataContext` change, a value it cannot ease, and every value under reduced motion without `WhenReduced` snap. Reason: a `DataContext` change is a recycled container or swapped item, so sweeping would be wrong; `DataContextChanged` always fires synchronously before the companion change it causes.
- **Follower lifetime matches a `LocalValue` binding.** Detaching lands on the latest target, reattaching snaps, and only disposing the expression or rebinding the property tears it down. Reason: in Avalonia 12 a `LocalValue` binding survives `ClearValue` and local writes, and the next push restores it.
- **Follow eases any codec type, with no per-follow color space.** Ints round; colors blend as any color tween does. Reason: there is no per-tween color space.
- **Follow is one-way only.** An explicit `TwoWay` or `OneWayToSource` throws; `Default` resolves against the one-way companion and is accepted. Reason: writing animated intermediate values back to a view model would be wrong.
- **Follow is not usable in style setters.** Reason: one setter is shared by every control it matches and, with `BindingBase` sealed off, there is no per-instance hook; templates build their content per instance.
- **Timing options default to `Springs.Gentle`.** `Duration`/`Ease`, `Spring`, `Bounce`, a `Builder` or a `Token`; with none, the critically damped `Springs.Gentle`. Reason: a spring rather than a `MotionTokens` entry, so retargets keep their velocity.
- **The spring text grammar belongs to Follow.** A preset name or `"bounce,duration"` is parsed privately by `FollowTiming` for `FollowParams.Spring` (and `{presto:Follow Spring=…}`). Reason: it exists for XAML, and the core keeps no spring string syntax (CORE-SPRING). Rejected: a public `Springs.Parse`, or extending `EaseParser` (no use outside Follow).
- **One name, `Follow`, and `Follow.Bind` is a static method.** Reason: one name for the whole feature, and XAML resolves `{presto:Follow}` to `FollowExtension` before the `Follow` class; an extension method named `Bind` would lose to `AvaloniaObject.Bind`. Rejected: the earlier `Animated` naming.
- **Shorthands are followed through attached properties, as a base layer.** `Follow.X`…`Follow.Blur` plus `Follow.Timing`; an animation of the same shorthand overrides the followed value while it holds it, then the latest followed value shows again. Reason: the shorthands are not Avalonia properties, so an extension cannot sit on them; the layering mirrors `Animation` priority over a `LocalValue` binding, and without it reverting an animation reset the shorthand to identity.
- **A motion value fed to a shorthand ignores `Follow.Timing`.** `Follow.Bind` is the one sink that takes options, and `{presto:Follow}` smooths unless `Duration=0`. Reason: a graph already states its smoothing; easing on top would lag a scroll- or drag-locked value and double-smooth a sprung one, while `Follow.Bind` is called with a value explicitly and can say so.
- **`Follow.Draw` follows one number.** The drawn share from the start, written as a `"Draw"` tween writes it. Reason: every `Follow` shorthand is one double, and every value-bound draw in the sample starts at 0; `RotateX`/`RotateY` stay out because they need `Perspective` beside them. Rejected: a start/end pair.
- **The core owns the cell, the bridge owns the sources.** See CORE-MOTIONVALUE.

## Non-goals

- Two-way follow: animated intermediate values are never written back to a view model.
- Use in a style `Setter` (one setter is shared by every control it matches).
- A `{presto:Map}` XAML extension: derived values are built in code and reach XAML through `{presto:Follow}`.
- `Follow.RotateX`/`RotateY` or other effect shorthands beyond `Blur`, and a start/end `Draw` pair.
- Per-follow color space choice: colors blend as any color tween does.

## Open items

- The planned API lists `FollowExtension.Ease` as `EaseValue?`; the code takes a `string?` parsed with `EaseParser`.
- The `Follow` XML remarks say a value that "arrives while detached" snaps, but a visual or target that has never been attached is not treated as detached, so a second value before the first attach eases.
- `Follow.*` shorthands cannot opt into `AnimateInitial` or `WhenReduced`; `Follow.Timing` is their only option.
- A non-numeric shorthand string (`"50%"`) silently releases the followed layer while the attached property keeps the string; untested.
- Untested: `NotSupportedException` for a non-numeric builder duration (BRIDGE-FOLLOW-004), reduced-motion toggles between values (013), `Follow.Bind<T>` ignoring `DataContext` (021), zero-duration motion sinks under `WhenReduced` (029), shorthand detach/reattach (044), `ValueOf` conversion failure (052).
- The derived-values plan lists a "timer" bridge source; it is the core `Timer.ProgressValue` (CORE-MOTIONVALUE), not a bridge type.
