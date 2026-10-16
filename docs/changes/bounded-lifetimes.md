# Bounded lifetimes

## Why

Before an early preview, nothing a consumer writes in the obvious way should grow without bound or wait forever. Four places fail that today:

- **Scopes grow.** `Scope.Register` prunes only canceled, uncompleted animations that a later animation of the same target and property can take over (CORE-SCOPE-024). Everything else stays until `Revert`: reverted timers, finished timelines, emptied nested scopes, reverted text splits. A scope that lives as long as a page and keeps creating work grows for as long as the page is open. The Typography word rotator runs `Rotate` from its cycle timer's `OnLoop`, outside the page scope, for this reason (backlog, CORE-SCOPE). Each word adds a `TextSplitter` (`TextSplitter.cs:53`), its resize timer, the reveal animation and the exit ghost animation (`Presence.cs:957`). The wordmark's `Lean` adds a nested `Scope` per re-split.
- **Hidden loops run.** `Animate.OnAttached` plays from attach to detach, whatever the element's visibility (`Animate.cs:322-384` has no visibility handling). A guide shown only during breaks loops all session. Tempo works around it by setting `OnAttached` from a `.breathing` class bound to `IsBreak` (`Tempo.axaml:393-396`), so the loop starts and is reverted with the class (backlog, BRIDGE-ATTACH).
- **Awaits hang.** `Then()` resolves only from an unmuted `OnComplete` (`Renderer.cs:209`, `:346`). `Cancel`, `Revert` and a muted completion leave it pending by design (CORE-CALLBACKS-034, "An interrupted timer never resolves `Then`"). Code that must not hang has no way to wait (backlog, CORE-CALLBACKS). The library hangs on itself in three places, shown under What.
- **Statics hold.** A survey of the bridge's static tables and long-lived subscriptions found two that keep entries a consumer can no longer release. The evidence is under What.

## What

**1. Scopes prune what has nothing left to restore.** An entry is *released* when reverting it again would restore nothing:

- a timer, animation or timeline that has been reverted and not revived since;
- a canceled plain timer (no tweens, no children);
- an `Animatable` after its `Revert`;
- a nested scope that has been reverted and has had nothing registered or added since;
- a bridge revertible after its own `Revert` (split, scroll observer, draggable, transformable, motion value source, layout and content-size transitions, compositor animation);
- a Presence exit-ghost animation once its ghosts are dismissed.

`Revert` and `Refresh` skip released entries, and the power-of-two prune drops them. The existing handover prune also covers completed animations: a canceled animation, completed or not, is dropped when every original value it captured can be handed to a later registered animation of the same target and property. A canceled timeline is dropped with its children when every child animation is dropped by either rule. A dropped timer or scope that is revived (`Reset`, `Seek`, `Restart`, `Play` after a cancel) or used again (`Add`, `Register`) registers again with the scope that dropped it.

What still grows is stated in CORE-SCOPE as a limit. That is a completed animation whose original no later animation takes over, on targets the scope never sees again: the scope must still restore it on `Revert`.

**2. `OnAttached` pauses while its visual is hidden.** The trigger watches `Visual.IsEffectivelyVisibleChanged` (Avalonia 12). While the visual is attached but not effectively visible, its `OnAttached` animation is paused. When the visual is shown again, the animation resumes, but only if the trigger paused it. A visual that attaches already hidden plays and is paused at once, so it starts when first shown. Detach, a new value and `null` behave as today. `OnTapped` and `OnChanged` are unchanged.

**3. `ThenOrCancel`.** `Timer.ThenOrCancel(CancellationToken cancellationToken = default)` returns a `Task<Timer>` (`Task<Animation>` on `Animation`). The task completes when `Then` would resolve. It is canceled when the timer is canceled before that (`Cancel`, `Revert`, `Complete(true)`, replacement by composition, a parent's cancel), or when the token is canceled. Canceling the token ends the wait only; the timer keeps playing. `Then` and `await timer` are unchanged.

**4. Fixes from the survey and the library's own hangs.**

- `Presto.Delay` hangs when its timer is reverted by the scope it was created in, and its token registration is disposed only on completion (`Presto.cs:42-56`). After the change it waits through `ThenOrCancel`, so a revert completes the task as canceled and disposes the registration.
- `Recipes.ThemeReveal` removes its overlay pictures only from `reveal.Then` or the next reveal on the same surface (`Prebuilt/Reveals.cs:47`, `:80`). A caller that cancels or reverts the returned animation leaves both snapshots on the overlay layer, and after a revert the new picture covers the surface unclipped. After the change, removal runs when the reveal ends either way.
- A page transition's `HostRun` completes `_done` from `_animation.Then` or its own `Stop` (`PrestoPageTransition.cs:186`, `:214`). An animation on the same host property cancels the run's animation by composition, `Finish` never returns, and the hosts are never released. After the change, an interruption completes the run.
- `ScrollContainer.Containers` is a static `Dictionary<ScrollViewer, ScrollContainer>` (`Events/ScrollContainer.cs:27`). An entry leaves only when its last observer, value or parallax leaves (`:163-176`), and a `ScrollObserver` leaves only on `Revert`: it has no detach handling. One unreverted observer created outside a scope roots its `ScrollViewer`, and the tree under it, for the life of the process. The registry becomes a `ConditionalWeakTable`, as the bridge's other per-visual tables are.
- `LayoutTransition.s_prepared` (`Compositor/LayoutTransition.cs:26`) drops a key's entry only when that key is taken or prepared again (`:168`, `:181`). A `Connected.Key` that leaves and never returns (a deleted row keyed by id) leaves its entry behind for good (`Connected.cs:210`). Expired entries are now swept when a key is prepared.

Found but too large for this change; they go to the backlog (see Tasks):

- **Additive animations escape BRIDGE-HELPERS-031.** `Additive.Lookups` is a strong, per-engine `Dictionary<object, …>` (`Core/Additive.cs:10`). Entries leave only when a tween's siblings are removed on cancel (`Core/Composer.cs:77`), but the detach tracker pauses rather than cancels (`Targets/DetachTracker.cs:108-111`). A detached control with a paused additive loop is therefore never collected, and `Additive.Update` sums its tweens every frame.
- **Scroll observers on detached targets stay live.** An observer whose target detaches stays in its container and is evaluated on every scroll frame until reverted (`ScrollObserver.cs` has no detach handler). Observers created in code for recycled rows of a long-lived scroller therefore accumulate.
- **Motion looping states run while hidden.** `MotionTracker` starts looping states on attach with `WhenDetached.Continue` (`MotionTracker.cs:339`, `:511`) and never reads visibility.
- **Bridge handles have no interruption-aware wait.** `ThreadedAnimation`, `LayoutTransition`, `ContentSizeTransition`, `CompositorAnimation` and `ParticleEffect` each have their own `Then`, and none has a `ThenOrCancel`.

What does not change: `Then`'s hang-on-interruption contract (CORE-CALLBACKS-034); what a scope's `Revert` restores for any entry that still has something to restore; the ported timing tests; the tick path (no per-frame work is added).

## Decisions

- **Prune by "released", not by "finished".** An entry leaves the scope when reverting it would restore nothing, not when it completes. Reason: a completed animation still holds the original values `Revert` must write back (CORE-SCOPE-025), so pruning finished work would break the scope's one promise. Rejected: pruning completed animations outright; capping the list and reverting the oldest.
- **`Revert` skips released entries even when no prune has dropped them yet.** Reason: otherwise a scope's result would depend on whether its count had crossed a power of two. Reverting an already reverted animation only writes stale originals over values the app has set since. Rejected: keeping upstream's re-revert, which makes the pruned and unpruned paths differ.
- **The handover prune extends to completed animations.** Revert runs newest first, so the oldest original of a property is the one left written, whether the completed animation reverts itself or hands its original to its successor. Reason: repeated retargeting that lets each animation finish (hover in, hover out) grew the list exactly as superseded retargeting did before CORE-SCOPE-024. An animation with a bridge revert hook is never handed over, because its revert does more than write values.
- **A dropped entry that comes back registers again with the scope that dropped it.** A timer or scope keeps a reference to the scope it registered with and a pruned flag. Reason: any reverted timer can be revived (`Restart` after `Revert`), and a scope can be reused after `Revert` (CORE-SCOPE-036); without re-registration the parent's `Revert` would silently miss it. Entries cleared by the scope's own `Revert` or `Refresh` are not re-registered, as today.
- **"Released" is internal to the library.** Core and bridge revertibles report it through an internal interface; an `IRevertible` from outside the library is never released and stays until `Revert`, as CORE-SCOPE-037 requires. Reason: no consumer has asked, and a default interface member can add it later without a break. Rejected: a public `IRevertible.IsReleased` now.
- **Exit ghost animations stay registered and become released once dismissed.** Reason: a scope's revert mid-exit still cancels and dismisses the ghosts, as today, and once dismissed nothing they changed is on screen. Rejected: creating them outside the current scope, as `PieceGhosts` does for its split, which would let ghosts keep playing over the next page after a navigation.
- **Visibility pauses only `OnAttached`.** Reason: `OnAttached` is the "while attached" trigger for loops and timers, so "while shown" is its meaning, and its trigger already owns the animation it would pause. `OnTapped` and `OnChanged` are one-shots that end and revert. Rejected: pausing every animation on a hidden visual from the detach tracker. Code-driven animations on hidden visuals are often deliberate (pre-positioning before a show, a Presence hide hold), and every tracked visual would watch visibility.
- **Hidden pauses, it does not revert.** Reason: a hide is usually brief and reversible (a tab, an expander, a carousel page), and a resumed loop keeps its phase. A paused animation leaves the engine list, so a page of hidden loops lets the engine idle. Rejected: revert on hide and replay on show, which would replay a finished `OnAttached` entrance on every expand and break BRIDGE-ATTACH-006.
- **Effective visibility only.** The trigger reads `IsEffectivelyVisible`, which follows `IsVisible` up the tree. It does not consider opacity, clipping or being scrolled out of view. Reason: Avalonia raises `IsEffectivelyVisibleChanged` during the visibility walk it already does, so watching costs one handler per `OnAttached` trigger and nothing per frame. Viewport intersection would need layout-pass work per element. Rejected: polling; viewport-based pausing.
- **Resume only what the trigger paused.** Reason: a pause from elsewhere (DevTools freeze, a detach-paused root) must survive a show.
- **`ThenOrCancel` is a separate, opt-in method.** Reason: CORE-CALLBACKS keeps `Then` hanging on interruption, because a canceled task would crash every `async void` handler on a double-click. Code that must not hang asks for cancellation by name, as Flutter's `TickerFuture.orCancel` does. Rejected: `Then(CancellationToken)`, which reads as "cancel my wait" and hides that interruption also cancels; a `ThenOptions` record, too heavy for one flag; a `Task<bool>` "ended" wait, which never throws but cannot join `Task.WhenAll` failure handling or `OperationCanceledException` filters.
- **The token cancels the wait, not the timer.** Reason: the waiter owns its wait, and the timer belongs to whoever created it. `Presto.Delay` owns its timer, so it still cancels it (CORE-PRESTO-053).
- **`ThenOrCancel` takes no callback.** Reason: `Then(callback)` exists for work that must run inside the tick, and this variant is for awaiting. Mixing in a callback would raise the question of whether it runs on interruption.
- **An interruption ends the wait for good.** Once canceled, the task stays canceled even if the timer is revived; a new call gives a new task. A paused timer keeps the task pending; pass a token to bound that wait. Reason: a revived timer is a new run, and pause is not an interruption.
- **Library code that must finish on interruption waits with `ThenOrCancel`.** This covers `Presto.Delay`, `ThemeReveal` and the page-transition host run. Reason: each runs cleanup or completes a caller's task, and none of them can tell an interruption from a completion today.
- **Small static-table leaks are fixed here; leaks that need a design go to the backlog.** Reason: the scroll registry and the prepared-rect sweep are local changes with a GC or unit test each. Additive lookups, detached scroll observers and Motion loops each need a behavior decision.
- **The variant is spelled `ThenOrCancel(CancellationToken cancellationToken = default)`.** Reason: it names Flutter's idiom and sits beside `Then` in completion lists. Rejected: `OrCancel()`, `Finished()`, `Then(ThenOptions)`, and a `Task<bool>` "ended" wait.
- **Pause-while-hidden is always on, with no opt-out property.** Reason: no case for a loop running unseen has come up, and an `Animate.PauseWhenHidden` property can be added later without a break.

## Spec changes

**CORE-SCOPE** (`docs/specs/core/scope-and-defaults.md`):

- Added: "A registered entry shall be released when reverting it again would restore nothing: a timer reverted and not revived since, a canceled timer with no tweens and no children, an animatable after its `Revert`, a nested scope reverted with nothing registered or added since, or a library revertible after its own `Revert`."
- Added: "`Revert` and `Refresh` shall skip released entries."
- Added: "When a prune runs, the scope shall drop every released entry."
- Added: "When a prune runs, the scope shall drop a canceled timeline together with its child animations when each child is released or can hand over its originals."
- Added: "When a timer or scope dropped by a prune is revived or used again, it shall register again with the scope that dropped it."
- Added: "An `IRevertible` from outside the library shall never be released."
- CORE-SCOPE-024 changed: "When a registration brings a list to a power-of-two count of at least 32, the scope shall drop each canceled animation, completed or not, with no bridge revert hook, whose every captured original value it can hand to a later registered animation of the same target and property, and hand them over."
- CORE-SCOPE-025 changed: "Pruning shall keep a completed animation that holds an original no later animation can take, so its targets are still restored on revert."
- Threading and lifetime: replace "until `Revert` or a prune drops them" with the limit, worded: "A scope holds what it must restore until `Revert`: a completed animation whose original no later animation takes over is held even when its targets are gone for good; create such animations outside the scope, revert them when their target leaves, or give them `WhenDetached.Revert`." Provenance: extend the pruning bullet to released entries and completed handovers. Decisions as above.

**CORE-CALLBACKS** (`docs/specs/core/callbacks.md`):

- Added: "When `ThenOrCancel` is called on a timer that has not completed, it shall return a task that completes with the timer when `Then` would resolve."
- Added: "When the timer is canceled before that (`Cancel`, `Revert`, `Complete(true)`, replacement by composition, or its parent's cancel), the task shall complete as canceled."
- Added: "When `ThenOrCancel` is called on a completed timer, it shall return an already completed task; on a canceled, uncompleted timer, an already canceled one."
- Added: "When the token is canceled first, the task shall complete as canceled and the timer shall keep playing."
- Added: "A canceled task shall stay canceled when the timer is revived; a later call shall return a new task."
- Added: "The task shall run its continuations asynchronously, and `Animation.ThenOrCancel` shall complete with the animation."
- Non-goals: strike "no variant that throws on interruption (a candidate in BACKLOG)". Decision "An interrupted timer never resolves `Then`" gains: "Code that must not hang uses `ThenOrCancel`." Allocation: one task completion source per pending `ThenOrCancel`, plus the token registration when a token is given.

**CORE-PRESTO** (`docs/specs/core/presto.md`):

- Added: "When `Delay`'s timer is canceled or reverted before it completes (as by the scope it was created in), the task shall complete as canceled."
- CORE-PRESTO-054 changed: "`Delay` shall dispose its token registration when the timer completes or is interrupted, and its continuations shall never run inline in the engine tick."

**BRIDGE-ATTACH** (`docs/specs/avalonia-bridge/xaml-attach.md`):

- Added: "While the visual is attached but not effectively visible, `Animate` shall keep its `OnAttached` animation paused."
- Added: "When the visual becomes effectively visible again, `Animate` shall resume the animation if, and only if, it paused it."
- Added: "When `OnAttached` plays on a visual that is not effectively visible, the animation shall start paused."
- Added: "A completed `OnAttached` animation shall be left as it is by visibility changes."
- Added: "Visibility shall not affect `OnTapped` or `OnChanged`."
- Threading and lifetime: one `IsEffectivelyVisibleChanged` handler per `OnAttached` trigger, removed with the trigger. Reduced motion: a reduced form pauses like any other, and one that completed at once has nothing to pause. Decisions as above.

**BRIDGE-PRESENCE**: added: "Once an exit's ghosts are dismissed, its ghost animation shall count as released to the scope it registered with (CORE-SCOPE)."

**BRIDGE-TEXT, BRIDGE-SCROLLOBSERVER, BRIDGE-DRAGGABLE, BRIDGE-HELPERS, BRIDGE-FOLLOW, BRIDGE-LAYOUT, BRIDGE-CONTENTSIZE, BRIDGE-THREAD**: in each spec owning a type that registers with the current scope (`TextSplitter`, `ScrollObserver`, `Draggable`, `Transformable`, `MotionValueSource`, `LayoutTransition`, `ContentSizeTransition`, `CompositorAnimation`), added: "Once reverted, it shall count as released to the scope it registered with (CORE-SCOPE)."

**BRIDGE-RECIPES**: added: "When a `ThemeReveal` animation is canceled or reverted before it completes, the reveal shall remove its pictures from the overlay layer."

**BRIDGE-PAGES**: added: "When a host's animation is canceled by anything other than the transition itself, that host's run shall complete, so the transition finishes and releases its hosts."

**BRIDGE-SCROLLOBSERVER**: added: "The scroller-to-container registry shall not keep a scroller alive: a scroller that leaves the tree with unreverted observers shall be collectable with its container and observers." Threading and lifetime: reword "The container keeps its observers alive…" to say this keeping lasts only while something else holds the scroller.

**BRIDGE-LAYOUT**: added: "When a key is prepared, entries older than three seconds under other keys shall be discarded, so prepared keys that are never taken do not accumulate."

## Tasks

Each task updates the specs named for it above (requirements, `Tests:` lines, Decisions) as it lands.

1. **Released entries in the core.** Add an internal released state to `Timer` (set at the end of `Revert`, cleared in `ReviveTimer`), `Animatable` and `Scope`, and an internal interface the scope reads. `Revert` and `Refresh` skip released entries. The prune drops them, extends the handover to completed animations without `_onRevert`, drops a canceled timeline with its children, and marks what it drops for re-registration. Tests (`ScopeTests`): `ReAnimatingATargetThatFinishesEachTimeStaysBounded`, `ReplayedTimelinesInsideAScopeStayBounded`, `RevertedTimersArePrunedAndNotRevertedAgain`, `RevertDoesNotRewriteAValueSetAfterTheAnimationsOwnRevert`, `ARevivedPrunedTimerIsRevertedByItsScope`, `AReusedPrunedNestedScopeIsRevertedByItsParent`, `ARevertibleFromOutsideTheLibraryIsNeverPruned`. Keep `CompletedAnimationsStayRegistered` and `ReplacedAnimationsDoNotAccumulateAndRevertStillRestoresTheOriginal` passing unchanged. Run the full core suite: no ported timing test may change.
2. **`ThenOrCancel`** on `Timer`, narrowed on `Animation`, with the cancel hook on the cancel path (`DetachTweens`), never in `Renderer`. Tests (`PromisesTests`): `ThenOrCancelCompletesWhenTheTimerCompletes`, `ThenOrCancelIsCanceledByCancelRevertAndAMutedComplete`, `ThenOrCancelIsCanceledWhenCompositionReplacesTheAnimation`, `ThenOrCancelIsCanceledWhenTheParentTimelineIsReverted`, `ThenOrCancelOnACompletedOrCanceledTimerReturnsAtOnce`, `TheTokenCancelsTheWaitButNotTheTimer`, `ARevivedTimerKeepsItsCanceledTaskAndGivesANewOne`, `ThenOrCancelResumesOutsideTheTick`; and `AllocationTests.CancelWithoutAPendingThenOrCancelAllocatesNothing`.
3. **`Presto.Delay` on interruption.** Tests (`PrestoDelayTests`): `RevertingTheScopeCancelsTheTask`, `TheRegistrationIsDisposedWhenTheTimerIsInterrupted`.
4. **Released bridge revertibles and ghosts.** Implement the internal interface on the bridge's registered types, and mark a Presence ghost animation released in `Dismiss`. Tests (bridge headless, new `ScopePruningTests`): `ARevertedSplitIsPrunedFromItsScope`, `TextSwapsWithPresenceInsideAScopeStayBounded` (100 arrive/exit swaps of a `TextReveal.Pieces="Chars"` block in a scope keep `RevertibleCount` under 64 and `Revert` still restores), and the GC test `SwappedOutBlocksAreCollectableWhileTheScopeLives` (`WeakReference` to the swapped-out blocks and their ghosts, collected with the scope still alive).
5. **`OnAttached` pauses while hidden.** Tests (`AnimateAttachedTests`): `AHiddenVisualsLoopIsPausedAndResumesWhereItWasWhenShown`, `HidingAnAncestorPausesTheLoop`, `AttachingHiddenStartsPausedAndPlaysWhenFirstShown`, `ACompletedAnimationIsLeftAloneWhenHidden`, `ShowingDoesNotResumeAPauseTheTriggerDidNotMake`, `DetachingWhileHiddenRevertsAsBefore`, `VisibilityDoesNotAffectOnTappedOrOnChanged`, `ClearingOnAttachedRemovesTheVisibilityHandler`, `ATextRevealOnAHiddenBlockWaitsUntilShown`, `AReducedLoopPausesWhenHidden`.
6. **Survey fixes.**
   - `ThemeReveal` removes its pictures on interruption. Test: `PageBloomTests.CancelingOrRevertingAThemeRevealRemovesItsPictures`.
   - `PrestoPageTransition.HostRun` completes on an outside interruption. Test: `PageTransitionTests.AnAnimationReplacingTheHostsFinishesTheTransition`.
   - `ScrollContainer.Containers` becomes a `ConditionalWeakTable`. GC test: `ScrollObserverTests.AScrollerDroppedWithAnUnrevertedObserverIsCollectable`.
   - `LayoutTransition.Prepare` sweeps expired entries. Test: `ConnectedTests.AKeyThatNeverReturnsDoesNotAccumulate`.
7. **Sample: the Typography rotator moves back inside its scope.** `OnLoop = _ => Scope.Execute(_ => Rotate())`, deleting any comment that explains the rotator's exclusion. Tests (`TypographyPageTests`): `TheRotatorDropsTheOldWordAsCharGhostsThenRisesTheNewOneWithoutFlashing` still passes; add `LeavingThePageMidSwapRevertsTheRotatorsWords`.
8. **Sample: Tempo's breathe guide.** Move `presto:Animate.OnAttached` from `TextBlock.breathe.breathing` onto `TextBlock.breathe` and drop the `.breathing` class and `Classes.breathing` binding; `IsVisible="{Binding IsBreak}"` now pauses it. Test: `FocusViewTests.TheBreatheGuideIsPausedOutsideABreakAndPlaysDuringOne`.
9. **Visual checks on the Desktop sample.**
   - `--page typography`: two minutes of rotation, then navigate away mid-swap. Look for no stray ghost over the next page and no flash on return.
   - A theme reveal interrupted by a second one and by navigating away.
   - Tempo (`dotnet run --project samples/Tempo/Tempo -- --skip-intro`): start and end a break, checking that the guide breathes only while shown.
   - Each in `--motion full` and `--motion reduced`.
10. **Backlog.** Remove the CORE-SCOPE "prunes only superseded animations" line, the CORE-CALLBACKS `Then` variant line and the BRIDGE-ATTACH "keeps a loop running while its element is hidden" line. Add:
    - [BRIDGE-HELPERS] A detached control with a paused `Composition.Additive` animation is not collectable: `Additive.Lookups` holds the target strongly until cancel, and the tracker only pauses, so BRIDGE-HELPERS-031 fails for additive composition.
    - [BRIDGE-SCROLLOBSERVER] An observer whose target detaches stays in its container and is evaluated every scroll frame until reverted; decide between reverting it and suspending it on detach.
    - [BRIDGE-MOTION] Looping `Motion` states keep running while their element is hidden.
    - [BRIDGE-*] `ThenOrCancel` for the bridge handles (`ThreadedAnimation`, `LayoutTransition`, `ContentSizeTransition`, `CompositorAnimation`, `ParticleEffect`).
    - [CORE-SCOPE] Completed animations on targets gone for good are held by the scope until `Revert` (the stated limit); a bridge-side weak hold for completed animations on visuals would lift it.

## Open questions

None.
