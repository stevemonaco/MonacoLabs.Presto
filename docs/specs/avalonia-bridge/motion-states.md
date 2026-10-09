---
id: BRIDGE-MOTION
title: Motion states and orchestration
package: Monaco.Presto.AvaloniaBridge
namespaces:
  - Monaco.Presto.AvaloniaBridge
sources:
  - Monaco.Presto.AvaloniaBridge/Motion.cs
  - Monaco.Presto.AvaloniaBridge/MotionCondition.cs
  - Monaco.Presto.AvaloniaBridge/MotionExtensions.cs
  - Monaco.Presto.AvaloniaBridge/MotionOrder.cs
  - Monaco.Presto.AvaloniaBridge/MotionState.cs
  - Monaco.Presto.AvaloniaBridge/MotionStates.cs
  - Monaco.Presto.AvaloniaBridge/MotionTracker.cs
types:
  - Motion
  - MotionState
  - MotionStates
  - MotionOrder
  - MotionExtensions
tests:
  - MotionStateTests
  - MotionConditionTests
  - MotionStateValidationTests
  - MotionPropagationTests
  - MotionOrchestrationTests
  - AllocationTests
  - WhenDetachedTests
  - EffectTests
  - RecipeTests
  - ReferencePagesTests
  - StorefrontPageTests
upstream: []
depends:
  - CORE-ANIMATION
  - CORE-BUILDER
  - CORE-COMPOSITION
  - CORE-KEYFRAMES
  - CORE-STAGGER
  - CORE-REDUCED
  - BRIDGE-TARGETS
  - BRIDGE-TRANSFORMS
  - BRIDGE-EFFECTS
  - BRIDGE-HELPERS
---

# Motion states and orchestration

## Purpose

State motion for any `Control`: while a condition holds on an element (hover, press, focus, a class, a pseudo-class or a named `Motion.State` token), the properties its `AnimationBuilder` names go to its values, and when it stops holding they return to the next winning state's value or to rest. States layer by declaration order. A `Motion.State` set on an element also drives the states of descendants that set none, timed by `StaggerChildren`, `DelayChildren` and `Order`. It builds on core animations with replace composition (CORE-ANIMATION, CORE-COMPOSITION) and the bridge's property resolution and shorthands (BRIDGE-TARGETS, BRIDGE-TRANSFORMS, BRIDGE-EFFECTS).

## Requirements

### Conditions

- **BRIDGE-MOTION-001** — A `When` shall be a run of ANDed tokens on the element itself: `:pseudo-class`, `.class`, and a bare name matched against `Motion.State` tokens.
  - Tests: `MotionConditionTests.AConditionSplitsIntoPseudoClassesClassesAndALeadingStateName`, `MotionConditionTests.EveryTokenMustHoldForAMatch`
- **BRIDGE-MOTION-002** — A bare name in a `When` shall match only `Motion.State` tokens, never a class of the same name.
  - Tests: `MotionConditionTests.ABareNameMatchesOnlyStateTokens`
- **BRIDGE-MOTION-003** — When a `When` is empty, contains whitespace, a combinator, a type name after the first token, a function such as `:nth-child(2)`, or a token with an empty or non-`[A-Za-z0-9_-]` name, parsing shall throw `ArgumentException` (parameter `when`) whose message points to `Motion.State` style setters.
  - Tests: `MotionConditionTests.SelectorPartsAreRejectedWithTheStateSetterEscapeHatch`
- **BRIDGE-MOTION-004** — The `:pointerover` and `:pressed` tokens shall match the tracker's synthesized hover and press, not the element's pseudo-classes of the same name.
  - Tests: `MotionConditionTests.PointeroverIsTheSynthesizedHoverRatherThanAClass`, `MotionStateTests.PressIsSynthesizedOnABorder`
- **BRIDGE-MOTION-005** — Every other `:pseudo-class` and `.class` token shall be read from the element's `Classes`, and any change to `Classes` shall re-evaluate the states.
  - Tests: `MotionStateTests.AClassAddedAndRemovedFromCodeEntersAndLeaves`, `MotionStateTests.CheckedOnAToggleButtonEnters`
- **BRIDGE-MOTION-006** — `Motion.State` tokens shall be the value's `ToString()` split on spaces, commas, tabs, CR and LF with empty parts dropped, so an enum matches by name and `"Error, Busy"` holds both tokens; a `null` value holds none.
  - Tests: `MotionStateTests.StateTokensMatchEnumNamesAndSeveralAtOnce`
- **BRIDGE-MOTION-007** — When a style setter on a descendant selector sets `Motion.State` on an element, its states shall enter while the selector matches and leave when the setter's value goes.
  - Tests: `MotionStateTests.ADescendantSelectorSettingStateDrivesTheChild`

### Sugar, ordering and layering

- **BRIDGE-MOTION-008** — `Motion.Hover`, `Motion.Focus` and `Motion.Press` shall be states with `When` `:pointerover`, `:focus-visible` and `:pressed`, ordered Hover, Focus, Press, then `Motion.States` entries in declaration order, then code-added states in the order added.
  - Tests: `MotionStateTests.PressLayersOverHoverOnASharedScale`, `MotionStateTests.ACodeStateWinsOverADeclaredStateForASharedProperty`
- **BRIDGE-MOTION-009** — For each property, the last active state in that order that names it shall be its winner; properties a state does not name are left to other states.
  - Tests: `MotionStateTests.PressLayersOverHoverOnASharedScale`, `MotionStateTests.ACodeStateWinsOverADeclaredStateForASharedProperty`
- **BRIDGE-MOTION-010** (inherited) — Property names shall be canonicalized per element before layering, so two spellings of one property share a winner.
  - Tests: untested
- **BRIDGE-MOTION-011** — When a property's winner changes to a state that has just become active, the tracker shall play that state's builder for the properties it takes, with its duration, delay, ease, per-property tween params, keyframes and loop.
  - Tests: `MotionStateTests.HoverEasesToTheTargetAndBackToRest`, `MotionStateTests.AKeyframedLoopingStateLoopsWhileActiveAndReturnsToRest`
- **BRIDGE-MOTION-012** — When a state stops holding, the properties it won shall return to the next winner's value, or to rest when no active state names them, timed by the leaving state's `Leave` if given, otherwise by its own `Motion` timing.
  - Tests: `MotionStateTests.TheReturnUsesLeaveTiming`, `MotionStateTests.PressLayersOverHoverOnASharedScale`
- **BRIDGE-MOTION-013** — When a property returns to a lower state that is still active, its target shall be that state's final value (the last keyframe holding a `To`), played on the leaving state's timing rather than the lower state's builder.
  - Tests: `MotionStateTests.PressLayersOverHoverOnASharedScale`
- **BRIDGE-MOTION-014** — Each transition shall play one animation per entering state and one per leaving state, covering only the properties whose winner changed.
  - Tests: `MotionStateTests.PressLayersOverHoverOnASharedScale`, `AllocationTests.AHoverAllocatesOnlyAtItsTransitions`
- **BRIDGE-MOTION-015** — State animations shall compose by `Composition.Replace`, so a property is taken over from where it stands with no jump.
  - Tests: `MotionStateTests.LeavingMidEnterReturnsFromTheCurrentValue`
- **BRIDGE-MOTION-016** — When a property is retargeted while the animation that last drove it has not yet begun (still in its delay), that animation shall be removed from the property rather than left to start later.
  - Tests: `MotionOrchestrationTests.ReturningMidStaggerNeverStartsTheWaitingChildrenAndReturnsTheOthersFromWhereTheyStand`

### Rest

- **BRIDGE-MOTION-017** — For an `AvaloniaProperty`, rest shall be its base value (below `Animation` priority), read as the return starts, or its styled default when nothing is set, so a local value, style or binding changed mid-state is honored.
  - Tests: `MotionStateTests.RestFollowsALocalValueChangedMidHover`
- **BRIDGE-MOTION-018** — For a transform, effect, clip, mask or warp shorthand, rest shall be the shorthand's identity.
  - Tests: `MotionStateTests.HoverEasesToTheTargetAndBackToRest`, `EffectTests.AHoverBlurReturnsToRestAndRemovesTheEffect`
- **BRIDGE-MOTION-019** — When a return to rest completes while it still drives a property, the tracker shall revert that property's animation-priority value, leaving no animation, no residual render transform and no residual effect.
  - Tests: `MotionStateTests.HoverEasesToTheTargetAndBackToRest`, `MotionStateTests.RestFollowsALocalValueChangedMidHover`, `EffectTests.AHoverBlurReturnsToRestAndRemovesTheEffect`
- **BRIDGE-MOTION-020** — When a property returning to rest has no resolvable `AvaloniaProperty` and is not a shorthand, the return shall throw `NotSupportedException`.
  - Tests: untested

### Validation

- **BRIDGE-MOTION-021** — When a state's `Motion` has a relative value (`+=`, `-=`, `*=`) in a property, tween `From`/`To` or keyframe, validation shall throw `NotSupportedException`.
  - Tests: `MotionStateValidationTests.ARelativeValueThrows`
- **BRIDGE-MOTION-022** — When a state's `Motion` has a tween with `From` but no `To`, validation shall throw `NotSupportedException`.
  - Tests: `MotionStateValidationTests.AFromWithoutAToThrows`
- **BRIDGE-MOTION-023** — Absolute values, from-to tweens, value arrays and keyframes shall be accepted in a state's `Motion`.
  - Tests: `MotionStateValidationTests.AbsoluteAndKeyframedValuesAreAccepted`
- **BRIDGE-MOTION-024** — When a `Leave` has keyframes, or gives a property anything but a per-property ease or duration (a value, `To`, `From`, `Delay`, `Composition`, `Modifier` or `TextModifier`), validation shall throw `NotSupportedException`; animation-level duration, delay and ease are accepted.
  - Tests: `MotionStateValidationTests.ALeaveThatGivesAPropertyAValueThrows`, `MotionStateValidationTests.ALeaveWithOnlyTimingIsAccepted`
- **BRIDGE-MOTION-025** (inherited) — Conditions shall be parsed and states validated when the attached property is set or the `MotionStates` list changes (or the code state is added), so an invalid state throws from the setter, attached or not.
  - Tests: `MotionStateTests.SettingTheAttachedPropertyValidatesTheState`

### Hover

- **BRIDGE-MOTION-026** (inherited) — The tracker shall listen for hover only while some state's `When` contains `:pointerover`, and shall start hovered when the pointer is already over the element.
  - Tests: untested
- **BRIDGE-MOTION-027** (inherited) — When the pointer exits an element while it is still inside the element's layout slot (its `Bounds` in the parent, ignoring render transforms), hover shall hold until the pointer leaves that slot or the top level.
  - Tests: `MotionStateTests.AHoverLiftingTheElementFromUnderThePointerHoldsUntilThePointerLeavesItsSlot`

### Press

- **BRIDGE-MOTION-028** — While some state's `When` contains `:pressed`, the tracker shall synthesize press for any element from `PointerPressed` with a left-button press kind (mouse, touch or pen), seen on tunnel and bubble with handled events included, without adding a `:pressed` class.
  - Tests: `MotionStateTests.PressIsSynthesizedOnABorder`, `MotionStateTests.AButtonsOwnHandledPressStillCounts`
- **BRIDGE-MOTION-029** — A right-button press shall not count as press.
  - Tests: `MotionStateTests.ARightButtonPressDoesNotCount`
- **BRIDGE-MOTION-030** — Press shall end on `PointerReleased` whose initial button was left, or on `PointerCaptureLost` (a touch that becomes a scroll).
  - Tests: `MotionStateTests.PressIsSynthesizedOnABorder`, `MotionStateTests.CaptureLossEndsThePress`

### Focus

- **BRIDGE-MOTION-031** — `Motion.Focus` and `WhileFocused` shall play on keyboard focus (`:focus-visible`) and not on a pointer click.
  - Tests: `MotionStateTests.FocusSugarPlaysOnKeyboardFocusButNotOnAClick`, `StorefrontPageTests.KeyboardFocusGlowsACard`
- **BRIDGE-MOTION-032** — A state with `When=":focus"` shall play on both keyboard focus and a click.
  - Tests: `MotionStateTests.AFocusConditionPlaysOnKeyboardFocusAndOnAClick`

### Attach, detach and lifetime

- **BRIDGE-MOTION-033** (inherited) — While the element is detached, condition changes shall play nothing.
  - Tests: untested
- **BRIDGE-MOTION-034** — When the element attaches, the states that hold shall be applied instantly with `Presto.Set` at their final values, with no animation left running.
  - Tests: `MotionStateTests.AReattachedElementSnapsToItsActiveStates`, `MotionPropagationTests.AChildAddedUnderAnOpenParentArrivesSnapped`
- **BRIDGE-MOTION-035** (inherited) — When a state that holds at attach loops, it shall also be played from the snapped value, so it keeps looping.
  - Tests: `MotionStateTests.ALoopingStateActiveAtAttachKeepsLooping`
- **BRIDGE-MOTION-036** — When the element detaches, every state animation shall be canceled and its properties reverted; the definitions stay.
  - Tests: `MotionStateTests.DetachRevertsEveryStateAnimation`, `MotionPropagationTests.DetachingTheParentRevertsTheChildren`
- **BRIDGE-MOTION-037** — State animations shall run with `HostLifetime = WhenDetached.Continue`, leaving detach handling to the tracker.
  - Tests: `WhenDetachedTests.AMotionStateAnimationIsLeftToMotion`
- **BRIDGE-MOTION-038** — When `Hover`, `Press`, `Focus` and `States` are all cleared and no code state remains, the tracker shall be released, canceling and reverting its animations at once.
  - Tests: untested
- **BRIDGE-MOTION-039** (inherited) — When one of those properties changes while others remain, or the `MotionStates` list raises a collection change, the tracker shall rebuild its state list and animate properties to their new winners or to rest.
  - Tests: untested
- **BRIDGE-MOTION-040** — In design mode, setting `Hover`, `Press`, `Focus` or `States` shall create no tracker.
  - Tests: untested
- **BRIDGE-MOTION-041** — One `MotionStates` list shared by a style across controls shall keep separate runtime state per control.
  - Tests: `MotionStateTests.AStyleSharedStatesListKeepsSeparateStatePerControl`

### Code API

- **BRIDGE-MOTION-042** — `WhileHover`, `WhilePressed` and `WhileFocused` shall add a state with `When` `:pointerover`, `:pressed` and `:focus-visible`; `While` adds one with the given `When`; the `Action<AnimationBuilder>` overloads build the builder with `Presto.Build` and behave as the builder overloads.
  - Tests: `MotionStateTests.WhileHoverPlaysOnHoverAndMatchesItsBuilderTwin`, `EffectTests.AHoverBlurReturnsToRestAndRemovesTheEffect`
- **BRIDGE-MOTION-043** — A code-added state shall sit after every declared state and win the properties it shares with them, and shall survive a rebuild of the declared states.
  - Tests: `MotionStateTests.ACodeStateWinsOverADeclaredStateForASharedProperty`
- **BRIDGE-MOTION-044** — Disposing the returned handle shall remove the state, returning its properties like any state that stops matching; a second dispose shall do nothing.
  - Tests: `MotionStateTests.DisposingMidStateReturnsToRest`

### Propagation

- **BRIDGE-MOTION-045** — An element with states that does not set `Motion.State` shall read its state tokens from the nearest visual ancestor that sets it, re-resolved on attach and whenever any element's `Motion.State` changes.
  - Tests: `MotionPropagationTests.AParentStatePlaysTheChildrensStatesAndClearingItReturnsThemToRest`, `MotionPropagationTests.AGrandchildFollowsThroughAnElementWithoutMotion`, `MotionPropagationTests.ItemsOfAnItemsControlFollowThroughTheirContainers`
- **BRIDGE-MOTION-046** — An element that sets its own `Motion.State` shall read only its own tokens, and its descendants shall follow it instead of the outer source.
  - Tests: `MotionPropagationTests.AChildsOwnStateWinsAndItsChildrenFollowIt`
- **BRIDGE-MOTION-047** — A follower's class and pseudo-class tokens shall come from the follower itself, never from its source.
  - Tests: `MotionPropagationTests.AChildsClassesAndPseudoClassesComeFromItself`
- **BRIDGE-MOTION-048** — When a source's `Motion.State` is cleared, its followers shall return to rest (or to their new source's states) on the source's orchestration timing.
  - Tests: `MotionPropagationTests.AParentStatePlaysTheChildrensStatesAndClearingItReturnsThemToRest`, `MotionOrchestrationTests.ClearingTheParentStateStaggersTheChildrenBackToRest`

### Orchestration

- **BRIDGE-MOTION-049** — On a source's state change, only followers whose winners change shall be timed; they shall be ordered by layout-slot origin under the source (top to bottom, then left to right, tree order breaking ties, render transforms ignored).
  - Tests: `MotionOrchestrationTests.TheStaggerFollowsWhereChildrenAreLaidOutRatherThanTreeOrder`, `MotionOrchestrationTests.AChildWithItsOwnStateIsLeftOutOfTheStagger`
- **BRIDGE-MOTION-050** — When `StaggerChildren` is non-zero, each follower's state animations shall be delayed by the core `Stagger(StaggerChildren, From = StaggerFrom, Total = followers)` value for its index.
  - Tests: `MotionOrchestrationTests.StaggerChildrenBeginsEachChildItsAmountAfterThePrevious`, `MotionOrchestrationTests.StaggerFromLastReversesTheOrder`
- **BRIDGE-MOTION-051** — `DelayChildren` shall be added to every follower's delay, on top of the state's own delay and the stagger offset.
  - Tests: `MotionOrchestrationTests.DelayChildrenShiftsEveryChild`
- **BRIDGE-MOTION-052** — With `Order = Together`, the source's own state animations shall start with no added delay, alongside its followers.
  - Tests: untested
- **BRIDGE-MOTION-053** — With `Order = BeforeChildren`, the end of the source's own state animations (delay plus duration) shall be added to every follower's delay.
  - Tests: `MotionOrchestrationTests.BeforeChildrenStartsTheChildrenWhenTheParentsOwnAnimationEnds`, `ReferencePagesTests.TheStatesMenuStaggersItsItemsInAfterThePanelAndClosesToRest`
- **BRIDGE-MOTION-054** — With `Order = AfterChildren`, the source's own state animations shall be delayed until the last follower's animation ends.
  - Tests: `MotionOrchestrationTests.AfterChildrenStartsTheParentWhenTheLastChildEnds`
- **BRIDGE-MOTION-055** — When the source's state changes again mid-stagger, followers still waiting shall never start their old animation, and the others shall return from where they stand.
  - Tests: `MotionOrchestrationTests.ReturningMidStaggerNeverStartsTheWaitingChildrenAndReturnsTheOthersFromWhereTheyStand`

### Reduced motion and diagnostics

- **BRIDGE-MOTION-056** — A state shall play its builder's `WhenReduced` form while `PrestoDefaults.ReducedMotion` is set, re-reading a state whose values were read under the other setting at its next transition.
  - Tests: `RecipeTests.TogglingReducedMotionTakesEffectAtTheNextTransition`, `RecipeTests.AnInteractionWorksAsAHoverStateAndReturnsToRest`, `StorefrontPageTests.WithReducedMotionOnAHoveredCardDoesNotMove`
- **BRIDGE-MOTION-057** — Under `ReduceAll`, every state animation, follower delays and staggers included, shall land at once.
  - Tests: `MotionOrchestrationTests.UnderReduceAllEveryChildLandsAtOnce`
- **BRIDGE-MOTION-058** — State animations shall be created under the diagnostics origin `"Motion"`.
  - Tests: untested

### Defaults

- **BRIDGE-MOTION-059** (inherited) — When a `MotionState`'s `When` is not set, it shall be empty, so validating the state throws `ArgumentException`.
  - Tests: `MotionConditionTests.SelectorPartsAreRejectedWithTheStateSetterEscapeHatch`
- **BRIDGE-MOTION-060** (inherited) — When `StaggerChildren` is not set, followers shall not be staggered.
  - Tests: untested
- **BRIDGE-MOTION-061** — When `StaggerFrom` is not set, the stagger shall run from the first follower, as anime.js's `stagger` does.
  - Tests: `MotionOrchestrationTests.StaggerChildrenBeginsEachChildItsAmountAfterThePrevious`
- **BRIDGE-MOTION-062** (inherited) — When `DelayChildren` is not set, followers shall get no added delay.
  - Tests: untested
- **BRIDGE-MOTION-063** (inherited) — When `Order` is not set, it shall be `Together`.
  - Tests: untested

## Invariants

- At most one tracker per element, held in a private attached property; all runtime state lives there, never in `MotionState` or `MotionStates`.
- The tracker acts only on condition changes (classes, synthesized hover/press, `Motion.State`, list changes, attach/detach), never per frame; an idle or settled hover allocates nothing per tick.
- After a return to rest settles, the element carries no Presto animation and no animation-priority value for the returned properties.
- Each property has at most one winner; a transition never plays an animation for a property whose winner did not change.

## Edge cases

- Rebuilding the state list recreates every entry, so properties of states still holding replay their enter animation from where they stand to the same target (no visible change).
- A property uncovered by a leaving state goes to the lower state's final value; a looping or keyframed lower state does not resume its loop (see Open items).
- A follower whose state does not change is not counted in the stagger, so stagger indices skip unaffected children.
- With no followers, `AfterChildren` plays the source at once; `BeforeChildren` with no own states adds nothing.
- A state delay given as a function value is read as 0 when an orchestration delay is added to it.
- Clearing a source's `Motion.State` releases followers through the same orchestration walk, so they return on its stagger.
- Press stays on while the pointer is dragged off the element, until release or capture loss.

## Threading and lifetime

- UI thread only, like every bridge type; animations run on the element's engine (`Presto.Engine` via `Presto.Animate`).
- The tracker subscribes to the element's `Classes`, attach/detach and, only while needed, pointer and top-level events; `Release` unsubscribes them all. The element's attached property keeps the tracker alive; the tracker holds the element strongly.
- A code-added handle keeps its state until disposed; it does not hold the tracker beyond the element.

## Reduced motion

- Per builder: `WhenReduced` forms are honored, read at each transition. Without one, `ReduceAll` collapses state animations, including orchestration delays, so they land at once (CORE-REDUCED).

## Allocation

- No per-frame work of its own. Transitions allocate their animations and small grouping dictionaries; the settled and idle states allocate nothing per tick (`AllocationTests.AHoverAllocatesOnlyAtItsTransitions`).

## Deviations from anime.js

- No upstream counterpart. The model follows Framer Motion's `whileHover`/`whileTap`/`whileFocus` and variants.

## Decisions

- **One static class, `Motion`, named for its meaning.** State motion is `Motion` (like `Presence` and `Connected`) with builder sugar `Hover`/`Press`/`Focus`, a `States` list of `MotionState { When, Motion, Leave }` and a `State` object whose tokens match named conditions; prebuilts stay on `Recipes`. Reason: named after the meaning, and `Motion` collides with nothing in Avalonia. Rejected: a Framer-style `While.Hover`; `Interact`.
- **Conditions are ANDed tokens, not a selector engine.** A `When` is `:pseudo-class`, `.class` and a leading bare `Motion.State` name on the element itself; descendant, child and template parts throw. Reason: Presto parses no selectors. Rejected: a selector engine.
- **Selector reach goes through style setters on `Motion.State`.** "Hover the card, animate the arrow" is a style setter on the arrow's `Motion.State`; when the selector stops matching, the setter's value goes and the state leaves. Reason: Avalonia styles already give full selector power.
- **Sugar has a fixed order and focus means `:focus-visible`.** `Hover`, `Focus` and `Press` map to `:pointerover`, `:focus-visible` and a synthesized `:pressed`, ordered Hover < Focus < Press before the `States` entries, so press wins over hover; `When=":focus"` covers focus from any input, and focus is read from `Classes` like every pseudo-class. Reason: a mouse click should not play the focus motion.
- **Press is synthesized for every element.** The tracker keeps its own pressed flag from a left-button (or touch or pen) `PointerPressed`, seen with handled events, until a left release or capture loss, and `:pressed` reads only that flag. Reason: only `Button` and its subclasses set `:pressed`, and a Button that handles its own press must still count; a touch that becomes a scroll ends the press.
- **Entering uses the state's timing; leaving uses `Leave`, which only times.** A property leaving a state goes to the next winner or rest on the leaving state's `Leave` builder, else its own timing, and a value in `Leave` throws. Reason: not recorded; it mirrors `LayoutMotion`'s timing-only rule.
- **Rest is the base value, read as the property returns.** For an `AvaloniaProperty` rest is its value below `Animation` priority (its default when unset), and the tracker reverts its animation-priority value once the return completes; a shorthand's rest is its identity. Reason: a binding, style or local value changed mid-state is honored, and the revert does not snap because the values are already equal. Rejected: capturing rest before the first activation.
- **Values in a state are absolute.** Relative values and from-only tweens throw in a state. Reason: a relative value would accumulate on every re-entry, and a from-only tween is meaningless as a target.
- **Arrival snaps; detach reverts.** On attach the holding states are applied with `Presto.Set`; detach cancels and reverts every state animation while the definitions stay. Reason: a recycled container or a view shown again arrives in its state instead of replaying it. Rejected: an opt-in to animate initial states.
- **State animations compose by replace.** Each transition plays one animation per entering and per leaving state, covering only the properties whose winner changed, from where they stand. Reason: a hover during an entrance goes straight to the lift while the entrance keeps its other properties, the look the recipes are designed around.
- **Interrupted state springs restart at rest velocity.** States do not reuse `Animatable`'s velocity preservation. Reason: not recorded; only `Animatable` measures velocity. Rejected: a `MotionState.PreserveVelocity` option, or running states on an `Animatable`.
- **Every state plays on the UI thread.** States never play through the compositor, even transform- and opacity-only ones. Reason: input arrives on the UI thread anyway, so the gain would be smoothness under load, not responsiveness; left out unless a sample shows the need.
- **The tracker re-reads a state when the reduced flag has flipped.** A state whose values were read under the other setting is re-read at its next transition; a state already held keeps its values until left. Reason: not recorded.
- **`Motion.State` propagates by explicit resolution.** A tracker without its own `Motion.State` reads the nearest visual ancestor's, re-resolved on attach and whenever an element sets or clears it; a source's change walks its descendants; classes still come from the follower, and a follower attached under an open source arrives snapped. Reason: parity with Framer Motion variants and Compose `updateTransition`, where `Motion.States` was per element. Rejected: Avalonia property inheritance.
- **Orchestration times only followers whose winners change, by layout position.** Followers are ordered by layout-slot origin with render transforms ignored and delayed by `DelayChildren` plus the core `Stagger(StaggerChildren, From = StaggerFrom)`; `BeforeChildren` and `AfterChildren` sequence the source; `StaggerFrom` is a property of its own; under `ReduceAll` all land at once. A state animation still in its delay when retargeted is removed, not trimmed. Reason: replace composition trims only begun tweens, so returning mid-stagger must never start a waiting child.
- **Features that manage their own lifetimes pass `WhenDetached.Continue`.** See BRIDGE-HELPERS.
- **Reduced motion is an app-set flag with a per-builder alternative.** See CORE-REDUCED.

## Non-goals

- No selector engine: descendant, child, template and functional selector parts are rejected; selector reach goes through style setters on `Motion.State`.
- No opt-in to animate the states that already hold at attach.
- One-shot triggers (`Animate.OnTapped`, `Animate.OnChanged`) are not states; see BRIDGE-ATTACH.
- Relative and from-only values in a state.

## Open items

- `MotionState.Leave` validation rejects a per-property `Delay`, while the tracker reads a per-property `Delay` from `Leave` timing (dead path) and `MotionState.Leave`'s doc says "duration, delay and eases"; only an animation-level delay is usable.
- A property returning from a leaving state to a lower, still-active looping or keyframed state lands on that state's final value and does not resume its loop or keyframes.
- The original design named `IsPointerOverProperty` as hover's source; the code synthesizes hover from `PointerEntered`/`PointerExited` with a layout-slot hold, and `:pointerover` in `Classes` is ignored.
- A function-valued state `Delay` is dropped (read as 0) when orchestration adds a stagger or `DelayChildren` delay.
- A rest-less property (neither resolvable nor a shorthand) throws `NotSupportedException` only when its return starts, from inside an input or class-change handler, not when the state is set.
- `Motion.Add` (the `While*` path) creates a tracker even in design mode, unlike the attached properties.
- Untested: canonicalization of property spellings (010), rest-less property error (020), hover listen gating (026), no play while detached (033), tracker release on clearing the last state (038), rebuild on property or list change (039), design-mode guard (040), `Order = Together` timing (052), diagnostics origin (058).
