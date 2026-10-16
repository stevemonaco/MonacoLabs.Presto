# Consumer API gaps found by Tempo

## Why

Tempo (`samples/Tempo/Tempo/`) builds an app with Presto the way an outside consumer would, and its "Tempo findings" in `docs/BACKLOG.md` list where the API made it write code-behind or restate a library rule. Six of them are small API-shape fixes that are cheaper to make before an early preview release than after, when they become breaking changes or permanent workarounds in users' apps:

- **A. [BRIDGE-SUBMIT]** `ProgressRing` raises `Confirmed` but has no `Command`, and raises nothing when a hold starts or ends short. Tempo's abandon ring handles `PointerCaptureLost` and Space/Enter `KeyUp` and tracks whether `Confirmed` came first (`FocusView.AbandonReleased`); its confirm dialog treats any `Value` above 0 as pressed through `PropertyChanged` (`ConfirmDialog`), and both reach their view-model command from a handler. Separately, `SubmitButton.Run` has no guard against overlapping runs (BRIDGE-SUBMIT open item).
- **B. [BRIDGE-SWIPE]** `Swipe.Command` gets only the `Edge`, with one command per element, so Tempo maps edges to actions on the item's view model (`TaskItemViewModel.SwipeCommand`) and raises `DeleteRequested` back to `MainViewModel` to delete.
- **C. [BRIDGE-HELPERS]** There is no typed `Fill` or `Stroke` builder method, so a shape's paint is `Property("Fill", …)` by name (Tempo's ring accent, the Glyph Lab and Title Sequence pages). And nothing checks that every shorthand has a typed method (BRIDGE-HELPERS open item).
- **D. [CORE-KEYFRAMES]** `TweenKeyframesBuilder.Color` requires `from`, so recoloring in OKLCH from wherever a property stands needs a hand-written `FunctionValue` reading the target (`TempoMotion.Accent`). `Tints.Recolor` has that reader but keeps it private, and has no reduced form, so `ReduceAll` snaps it and Tempo restates its accents under `WhenReduced`.
- **E. [BRIDGE-ACCORDION]** On Semi's `Expander`, `Accordion.Enabled` finds no chevron (it knows only Fluent's parts, BRIDGE-ACCORDION-007). Tempo marks `PART_PathIcon` with `Accordion.Chevron` from a `/template/` style and clears Semi's `Template`-priority 250 ms `RenderTransform` transition with an activator selector (`:not(.semi-transitions)`), or the chevron double-eases.
- **F. [BRIDGE-POPOVER]** A flyout has no bindable `IsOpen`, so Tempo opens its settings flyout from a view-model command in code-behind and sets the button's `open` class from `Opened`/`Closed`.

Each part below (A–F) is self-contained: its decisions, spec changes and tasks touch no other part's, and any one can be dropped from scope without affecting the rest. D's Tempo task uses the new `Color` overload through `Tints.Recolor`; without D, C's typed `Fill` is still usable on its own.

## What

### A. ProgressRing command and hold events; one owner per submit run

```xml
<presto:ProgressRing HoldDuration="900" Command="{Binding AbandonCommand}"
                     HoldStarted="AbandonPressed" HoldCanceled="AbandonLetGo" />
```

- `ProgressRing` gets `Command` and `CommandParameter`, as on `Button`. A hold that reaches 1 raises `Confirmed`, then executes the command. While the command cannot execute, the ring is disabled and takes no holds.
- `HoldStarted` is raised when a hold starts (pointer or Space/Enter). `HoldCanceled` is raised when a hold stops before the ring fills, as the rewind starts. A hold that confirmed raises no `HoldCanceled` when it is let go.
- `SubmitButton.Run` called while an earlier `Run` on the same button is in progress takes the button over: the earlier run stops writing `State` but still awaits its work and returns its outcome. Today the earlier run's `Idle`, written after its outcome hold, collapses the later run's `Busy` spinner.

Unchanged: the hold's timing, rewind and reduced form, `Confirmed`, and every `SubmitButton` state.

### B. Per-side swipe commands with a parameter

```xml
<Setter Property="presto:Swipe.LeadingCommand" Value="{Binding CompleteCommand}" />
<Setter Property="presto:Swipe.TrailingCommand" Value="{Binding $parent[ItemsControl].((vm:MainViewModel)DataContext).DeleteTaskCommand}" />
<Setter Property="presto:Swipe.CommandParameter" Value="{Binding}" />
```

- New attached `Swipe.LeadingCommand`, `Swipe.TrailingCommand` and `Swipe.CommandParameter`. A commit on a side runs that side's command with `CommandParameter`. When a side has no command of its own, the commit falls back to `Swipe.Command` with the `Edge`, as today, so the toast's and the Gestures page's one-command dismissals keep working.
- A side commits only when it has a command it can execute, or `Dismiss` is set. A side whose command cannot execute stops at its actions' width, and a release past half the width there springs back instead of committing.

Unchanged: dragging, revealing, the commit threshold, dismissing and the context-request path.

### C. Typed Fill and Stroke; a check that every key has a method

- `Fill` and `Stroke` builder methods, with the same overloads as `Background` (`TweenValue`, tween settings, `FunctionValue`, keyframes, `Color`, `IBrush`), writing `"Fill"` and `"Stroke"`. They resolve on any target with those properties: every `Shape`, and `ProgressRing.Fill`.
- A reflection test fails when a `PrestoKeys` key has no `PropertyBuilderExtensions` method of the same name. The four warp shorthands are exempt, because they are a BRIDGE-WARP non-goal.

### D. Color keyframes from the current color

```csharp
.Fill(k => k.Color(accent.ToPresto(), ColorSpace.Oklch, duration: 700, ease: Eases.InOutSine))
```

- `TweenKeyframesBuilder.Color(to, space, hue, duration, delay, ease, segments)`, an overload with no `from`, starts each target from its property's current value. The value is read per target when the animation is built, through the target's accessor and codec (as `Presto.Get` reads it). A value that decodes as a color starts from that color; anything else (a gradient, null) starts from transparent. The core reads it generically, so this works on any target whose adapter decodes colors.
- `Tints.Recolor` uses the overload and drops its private reader. It also carries itself as its reduced form, so `ReduceAll` leaves a recolor alone, as BRIDGE-RECIPES-MEDIA already says reduced motion does ("a color change is not motion").
- Tempo's `Accent` becomes `Tints.Recolor(accent, property, ColorSpace.Oklch, duration: 700, ease: Eases.InOutSine)`.

Unchanged: the two-ended `Color`, its fitting, its per-target `FunctionValue` ends and its exact ease.

### E. Accordion on Semi; chevron transitions lifted

- `Accordion.Enabled` also finds Semi's chevron: an element named `PART_PathIcon` among the item's or its `ExpanderHeader` toggle's template elements gets `Chevron = 180`, as Fluent's `ExpandCollapseChevronBorder` does.
- While a chevron is linked (whether found automatically or marked by the app), its `RenderTransform` transitions are lifted, the way Popover lifts a presenter's `Opacity` transitions (BRIDGE-POPOVER-008). `Transitions` is set at `Animation` priority to its base list without them, re-read at each turn, and the lift is disposed on unlink. This stops a theme transition from easing each frame the spring writes.
- Tempo deletes both of its chevron styles.

Unchanged: the body fold, fade and entrance replay, groups, `Pane`, and the Fluent path's identity transform.

### F. Popover: no bindable IsOpen (non-goal)

Nothing ships. `BRIDGE-POPOVER` records a bindable flyout `IsOpen` as a non-goal with its reason (see Decisions), and the backlog item closes. Tempo keeps its code-behind; only its comments stop pointing at the backlog.

## Decisions

### A

- **The ring executes `Command` after raising `Confirmed`.** Same order as `Button`, which raises `Click` before its command. Reason: a handler that animates the confirm (Tempo's abandon cross) runs before the view model reacts to it. Rejected: a command that replaces the event.
- **A command that cannot execute disables the ring.** While `Command.CanExecute(CommandParameter)` is false, the ring's `IsEffectivelyEnabled` is false, so it takes no presses, keys or focus. It re-queries on `CanExecuteChanged`, subscribed only while it is attached. Reason: Avalonia's `Button` behaves this way, and a hold that fills to a confirmation that does nothing reads as broken. Rejected: holding normally and skipping the command at 1.
- **`HoldStarted` and `HoldCanceled` are plain CLR events, like `Confirmed`.** Reason: all three describe one control's hold and are handled on that control. A routed event would bubble to ancestors that have no reason to handle it. Rejected: routed events, and an `IsHolding` property (the dialog needs "was pressed since it opened", which is a latch over an event, not a state).
- **`HoldCanceled` fires only for a hold that ends short.** It is raised as the rewind starts, before `Value` moves back. Reason: an early release is the moment to react (Tempo's shake), and a release after `Confirmed` is not a cancel. Rejected: a `HoldEnded` with a confirmed flag, which every handler would have to filter.
- **The latest `Run` owns the button.** Each run takes a token, and a run whose token is stale writes no `State`. It still awaits its work, holds for its outcome and returns its own result. Reason: the work has already started when `Run` is called, so refusing the second run would lose its outcome, and with no guard the first run's late `Idle` collapses the second's spinner. Rejected: throwing `InvalidOperationException` (the caller's task is already running); queueing the second run (the button would show a stale outcome first).

### B

- **One command per side, plus a shared parameter.** Reason: a row's two sides usually do different things to the row's item, and the parameter lets a list-level command (delete) take the item without an event back from the item's view model. Rejected: passing a `SwipeCommit(edge, item)` record to `Command` (the view model then depends on a Presto type, and one command still has to switch on the edge); passing the `DataContext` implicitly (Avalonia's `Button` passes null without a `CommandParameter`, and an implicit parameter is surprising).
- **`Command` stays as the fallback for a side with no command of its own, and keeps receiving the `Edge`.** Reason: "dismiss either way" is one command (`Toast`, the Gestures page), and the edge is what it needs. `CommandParameter` goes only to the side commands, so `Command`'s parameter never changes meaning. Rejected: dropping `Command` before the preview, which turns every either-way dismissal into two identical setters.
- **A side commits only when its command can execute or `Dismiss` is set.** `CanExecute` is read at each drag move and release, as the command itself already is. Reason: today a `Dismiss` slides out and hides a row whose command declines, so the row disappears and nothing happens. Per-side commands also make "this side does nothing" common (a done task can only be deleted).

### C

- **`Fill` and `Stroke` join the typed methods, with `Background`'s overloads.** They write the plain names, which resolve on every `Shape` and on `ProgressRing.Fill`. Reason: they are the shape paints, used by name about as often in the samples as `Foreground`. A typed `AvaloniaProperty` overload would bind to one owner type and miss the ring. Rejected: `Track` or other control-specific brushes (use `Property(ProgressRing.TrackProperty, …)`).
- **The key-to-method check is a reflection test with an explicit exemption list.** For each `PrestoKeys` field, `PropertyBuilderExtensions` must declare a method of that name whose `TweenValue` overload writes that key. The warp shorthands are listed as exempt, citing BRIDGE-WARP's non-goal. Reason: it closes the open item in about 20 lines, and a new shorthand fails it until it gets a method or a reasoned exemption. Rejected: a source generator for the methods (too heavy for about 40 names); checking method names only (a misspelled key would pass).

### D

- **`from` may be omitted, and the color is read when the animation is built.** The overload resolves a per-target start through the property's accessor and codec, exactly where a function-valued end resolves, so the expansion code and the per-target segment counts are unchanged. Reason: the earlier rejection ("starting from the current color needs a per-tween color space") assumed the read happens as the tween starts. Read at build time, it is just another per-target end. A delayed animation therefore starts from the color at its creation, as a plain from-current tween already does. Rejected: making `from` a trailing optional parameter of the existing method (it would reorder `from, to` and break every caller); a separate `ColorFromCurrent` name (two names for one tween).
- **A value that is not a color starts from transparent.** Reason: this matches `Tints.Recolor` today (BRIDGE-RECIPES-MEDIA-017). Under premultiplied mixing (CORE-COLORS), transparent fades in without tinting the path. Rejected: starting from `to` (no visible change) or throwing (a null brush is a normal starting state).
- **`Recolor` carries itself as its reduced form.** Reason: BRIDGE-RECIPES-MEDIA-018 already says a recolor is the same under reduced motion because a color change is not motion. Without a reduced form, `ReduceAll` snaps it, which is why Tempo restates it. Rejected: leaving `ReduceAll` to snap recolors; an app that wants recolors snapped sets `Duration(0)` in its own reduced form.
- **`Recolor` works as a `Motion` state.** With the start read per transition, a state recolors along its path from wherever the property stands, as Tempo's ring accents already do with the hand-written form. The return to rest is still a straight tween to the base value (BRIDGE-MOTION-012/017). Reason: Tempo shows this pattern works, so the non-goal no longer holds.

### E

- **Known theme chevrons are found by name, and others are marked with `Chevron`.** Fluent's (`ExpandCollapseChevronBorder` in the header's template) and Semi's (`PART_PathIcon` in the item's or the header's template) are looked up in that order. A part that already has a `Chevron` value keeps it. Reason: Fluent and Semi are the two themes the samples run on, and a name lookup per template apply is cheap. Rejected: a heuristic such as "the first `PathIcon` in the header", which would match icons in user header content.
- **A linked chevron lifts its `RenderTransform` transitions.** Same mechanism as BRIDGE-POPOVER-008: `Transitions` is set at `Animation` priority to the base list without `RenderTransform` entries, re-read from the base before each turn so a re-applied theme is followed, and disposed on unlink. Reason: the spring writes `RenderTransform` every frame, and a theme transition on it eases each write, which is the double-easing Tempo works around. Rejected: lifting transitions on every property Presto animates on any control. That is a cross-cutting change to BRIDGE-TARGETS with a cost on every write, and it stays in the backlog as its own item.

### F

- **A bindable flyout `IsOpen` is not Presto's job.** Opening and closing a flyout from a view model is an MVVM gap in Avalonia (`FlyoutBase.IsOpen` is read-only and `ShowAttachedFlyout` is a method), not a motion one, and Presto ships no MVVM helpers. Popover's motion already plays for any opener: `ShowAt` or `ShowAttachedFlyout` plays `Open`, and any `Hide()` plays `Close` (BRIDGE-POPOVER-010), so an app or community `IsOpen` behavior gets the motion unchanged. For a state held while open, Avalonia's `Button.Flyout` sets `:flyout-open`, which `Motion` already takes as a condition. Rejected: a `Popover.IsOpen` attached property (it would own anchor and placement policy that Avalonia leaves to the app). Tempo cannot use `Button.Flyout`, because its compact layout opens a sheet from the same button, so it keeps its code-behind.

## Spec changes

### A

**BRIDGE-SUBMIT** (`docs/specs/avalonia-bridge/progress-and-submit.md`): Purpose mentions the ring's `Command` and hold events. Added under *Progress ring: hold to confirm*:

- When a hold starts, the ring shall raise `HoldStarted` once, before `Value` begins to move.
- When a hold stops before `Value` reaches 1, the ring shall raise `HoldCanceled` once as its rewind starts; a hold that raised `Confirmed` shall raise no `HoldCanceled` when let go.
- When a hold reaches 1, the ring shall raise `Confirmed` and then, if `Command.CanExecute(CommandParameter)` is true, execute `Command` with `CommandParameter`.
- While `Command` is set and `CanExecute(CommandParameter)` is false, the ring shall be disabled and take no holds; it shall query again on `CanExecuteChanged` and when `Command` or `CommandParameter` changes.
- Defaults: `Command` and `CommandParameter` are null.

Added under *Submit button: Run*:

- When `Run` is called on a button while an earlier `Run` on it is in progress, the earlier run shall stop writing `State`, and still await its work, hold and return its own outcome.

The edge case "`Run` called again … overlaps the two" is struck and replaced by the requirement above, and the open item "`Run` does not guard against overlapping runs" is removed. *Threading and lifetime*: the ring subscribes to `CanExecuteChanged` only while attached. Decisions as above.

### B

**BRIDGE-SWIPE** (`docs/specs/avalonia-bridge/swipe-actions.md`):

- BRIDGE-SWIPE-001: "…`Command`, `LeadingCommand`, `TrailingCommand` or `CommandParameter` alone shall not."
- BRIDGE-SWIPE-016: "…the range on each side shall be its actions' width, or the whole element's width when that side commits (BRIDGE-SWIPE-024); …unless the element has no actions at all and a side commits, when that side ranges over its width."
- BRIDGE-SWIPE-024: "When a drag is released with `|x|` past half the element's width on a side that commits (its command can execute, or `Dismiss` is set), the swipe shall commit by position alone, regardless of velocity."
- BRIDGE-SWIPE-025: "A commit shall execute the side's own command (`LeadingCommand` for the leading side, `TrailingCommand` for the trailing) with `CommandParameter`, or, when that side has none, `Command` with `Edge.Right` for the leading side and `Edge.Left` for the trailing; only when its `CanExecute` of that parameter is true."
- Added: When the command a side would run has `CanExecute` false at a release past half the width and `Dismiss` is false, the release shall settle as one short of committing.
- Defaults added: `LeadingCommand`, `TrailingCommand` and `CommandParameter` are null.
- Edge case "`Command` is read on each drag move and release" extended to the side commands and their `CanExecute`; "A commit needs `Command` or `Dismiss`" reworded per side. Decisions as above.

### C

**BRIDGE-HELPERS** (`docs/specs/avalonia-bridge/control-helpers.md`):

- BRIDGE-HELPERS-012: "…`Background`/`Foreground`/`Fill`/`Stroke(Color)` as an immutable solid brush…"
- Added: Every `PrestoKeys` key except the warp shorthands shall have a typed builder method of the same name writing that key.
- Decision "Typed methods cover the most-used properties" adds `Fill` and `Stroke` and the reason above. The open item about a shorthand without a builder method is removed.

**BRIDGE-WARP** (`docs/specs/avalonia-bridge/text-warp.md`): the non-goal "Typed builder methods for the warp shorthands" names the exemption test.

### D

**CORE-KEYFRAMES** (`docs/specs/core/keyframes.md`):

- Added under *Color segments*: When `Color` is given no `from`, each target shall start from its property's current value read when the animation is built: a value its codec or the parser reads as a color starts from that color, and any other value from transparent.
- Added: The start read for a target shall be shared by all of that target's segments, as a function-valued end is (CORE-KEYFRAMES on per-target ends).
- Decision "Both ends of a color expansion are known at build time" is rewritten: `from` may be omitted, read per target at build like a function-valued end; the rejected alternative becomes a `from` parameter after `to` and a second method name. Edge case added: a delayed animation reads the start color at its creation, not after the delay.

**BRIDGE-RECIPES-MEDIA** (`docs/specs/avalonia-bridge/recipes-media.md`):

- BRIDGE-RECIPES-MEDIA-017: "…as `TweenKeyframesBuilder.Color` without `from` reads it (CORE-KEYFRAMES): a solid brush's color with its alpha multiplied by the brush opacity, a `Color` property's value, and transparent for anything else."
- BRIDGE-RECIPES-MEDIA-018: "`Recolor` shall carry itself as its reduced form, so it is the same while `ReducedMotion` is set and `ReduceAll` leaves it alone, since a color change is not motion."
- Non-goal "`Recolor` as a `Motion` state" is struck. Edge case added: as a `Motion` state, a recolor follows its path into the state, and the return to rest is a straight tween to the base value (BRIDGE-MOTION-012).

### E

**BRIDGE-ACCORDION** (`docs/specs/avalonia-bridge/accordion-and-pane.md`):

- BRIDGE-ACCORDION-007: "When enabled on an item whose `ExpanderHeader` toggle holds `ExpandCollapseChevronBorder` and `ExpandCollapseChevron` (Fluent), the accordion shall set `Chevron = 180` on the border and give the path an identity `TransformOperations` render transform; otherwise, when the item's or the toggle's template holds an element named `PART_PathIcon` (Semi), it shall set `Chevron = 180` on it. A part that already has a `Chevron` value shall keep it."
- Added under *Chevron*: While a chevron is linked, its `Transitions` shall be lifted, set at `Animation` priority to its base list without the `RenderTransform` transitions and re-read from the base at each turn; the lift shall be disposed when the chevron unlinks.
- BRIDGE-ACCORDION-005 adds the chevron's transition lift to what `Enabled` false releases.
- Edge case "An item without the Fluent header parts gets no automatic chevron" becomes "without the Fluent or Semi parts". Invariant added: an unlinked chevron has no lifted `Transitions` value. Decisions as above.

### F

**BRIDGE-POPOVER** (`docs/specs/avalonia-bridge/popovers.md`): Non-goal added: "A bindable `IsOpen` for flyouts (see A bindable flyout `IsOpen` is not Presto's job)", with the decision above under that title. No requirement changes.

## Tasks

### A

1. `ProgressRing.Command`, `CommandParameter`, `HoldStarted`, `HoldCanceled` and the `CanExecute` enable state. Tests (`ProgressRingTests`, headless): `AConfirmedHoldExecutesTheCommandAfterConfirmed`, `ACommandThatCannotExecuteDisablesTheRingAndFollowsCanExecuteChanged`, `AHoldRaisesHoldStartedOnceByPointerAndByKey`, `ReleasingShortRaisesHoldCanceledOnceAndAConfirmedReleaseRaisesNone`.
2. `SubmitButton.Run` ownership token. Tests (`SubmitButtonTests`): `ASecondRunTakesTheButtonOverAndTheFirstStillReturnsItsOutcome`.
3. Update BRIDGE-SUBMIT (requirements, `Tests:` lines, edge case, open item, Decisions).
4. Tempo: bind `Command` on `AbandonRing` (`AbandonCommand`) and `Hold` (`ConfirmCommand`). Delete `ConfirmDialog.HoldConfirmed` and the `PropertyChanged` latch; `_pressed` is set from `HoldStarted`. In `FocusView`, `AbandonConfirmed` keeps only its two animations, `AbandonReleased` becomes a `HoldCanceled` handler that only shakes, and `_abandoned`, the key filter and the `PointerCaptureLost`/`KeyUp` wiring go. Update the code-behind list in Tempo's README. Tests that must stay green: `FocusViewTests.LettingGoOfAbandonEarlyShakesItAndKeepsTheSession`, `SettingsTests.AnEscapeAfterAnEarlyReleaseShakesTheDialogAndKeepsItOpen`, `SettingsTests.HoldingToConfirmClearEmptiesTheListsAndTheLogAndSaves`, `SettingsTests.CancelOrAnEscapeBeforeAnyPressClosesTheDialogAndChangesNothing`.
5. Visual check on Desktop (`dotnet run --project samples/Tempo/Tempo -- --skip-intro`, full and reduced motion): the abandon ring's early release shakes and a full hold crosses and abandons; in the confirm dialog, Escape before a press closes it and after an early release shakes it.

### B

6. `Swipe.LeadingCommand`, `TrailingCommand`, `CommandParameter`; per-side commit and range, with `CanExecute` gating. Tests (`SwipeTests`): `EachSideRunsItsOwnCommandWithTheParameter`, `ASideWithoutItsOwnCommandFallsBackToCommandWithTheEdge`, `ASideWhoseCommandCannotExecuteSpringsBackInsteadOfCommitting`, `ASideWithNoCommandStopsAtItsActionsWidth`. Keep `AFullSwipeCommitsAndRunsTheCommand` and the toast's swipe tests green.
7. Update BRIDGE-SWIPE (requirements -001, -016, -024, -025, new ones, edge cases, Decisions).
8. Tempo: the task row style binds `LeadingCommand` to a new `TaskItemViewModel.CompleteCommand`, `TrailingCommand` to `MainViewModel.DeleteTaskCommand` (now a `[RelayCommand]`), and `CommandParameter` to the item; the done-list style clears `LeadingCommand`; the action buttons bind the same commands. Delete `TaskItemViewModel.SwipeCommand`, `DeleteRequested` and its subscription in `MainViewModel.Track`. Tests that must stay green: `TasksViewTests.DeletingFromTheSwipeActionsRemovesTheRowBehindItsExitAndUndoRestoresIt`, `TasksViewTests.ASwipeThatDriftsVerticallyKeepsSwipingAndNeverReorders`.
9. Visual check on Desktop (Tempo, wide and `--size 360x780`): a full leading swipe completes, a full trailing swipe deletes with Undo, a short swipe springs back, and a done row swipes only to delete.

### C

10. `Fill` and `Stroke` builder methods (six overloads each, as `Background`). Tests (`AnimationBuilderBridgeTests`): extend `TypedMethodsWriteTheIndexerKeys`, `EveryOverloadWritesTheSameKey` and `TypedValuesAreWrappedAsTheIndexerWouldWrapThem`; add `FillAndStrokeAnimateAShapeAndTheRingsFill`.
11. Reflection test `PrestoKeysTests.EveryKeyButTheWarpShorthandsHasATypedBuilderMethod`.
12. Update BRIDGE-HELPERS (-012, new requirement, Decision, open item) and the BRIDGE-WARP non-goal.
13. Gallery: `GlyphLabPage` and `TitleSequencePage` use `.Fill(…)` in place of `Property("Fill", …)`; their page tests stay green.

### D

14. `TweenKeyframesBuilder.Color(to, …)` without `from`: the builder receives its property key from `Property(name, keyframes)` and resolves a per-target start through the accessor and codec. Tests (`ColorKeyframesTests`, core, with a plain-object target and a test codec): `WithoutFromEachTargetStartsFromItsCurrentColor`, `WithoutFromANonColorValueStartsFromTransparent`, `WithoutFromAMidFlightRetargetStartsFromTheDrawnColor`.
15. `Tints.Recolor` uses the overload, drops `Current`, and carries itself as its reduced form. Tests (`RecipeTests`): `RecolorIsLeftAloneByReduceAll`; keep `RecolorStartsEachElementFromItsOwnColorAndStaggers` and `RecolorIsTheSameUnderReducedMotionAndFromXaml` green.
16. Update CORE-KEYFRAMES (requirements, Decision, edge case) and BRIDGE-RECIPES-MEDIA (-017, -018, non-goal struck, edge case).
17. Tempo: `TempoMotion.Accent` becomes `Tints.Recolor(accent, property, ColorSpace.Oklch, duration: 700, ease: Eases.InOutSine)`. Tests that must stay green: `TempoMotionTests` (the accent states), `MainWindowTests` and `FocusViewTests` accent assertions.
18. Visual check on Desktop (Tempo): switching Focus → Short break → Long break recolors the ring, pill and labels through OKLCH with no gray midpoint, in both motion modes.

### E

19. Semi chevron discovery and the `RenderTransform` transition lift on linked chevrons. Tests (`AccordionTests`, headless with a test `ControlTheme` shaped like Semi's, with `PART_PathIcon` and a `Template`-priority `RenderTransform` transition): `TheChevronIsFoundOnASemiShapedTemplate`, `ALinkedChevronLiftsItsRenderTransformTransitionsAndUnlinkRestoresThem`, `AnAppSetChevronIsKept`. Keep `TheChevronSpringsRoundInPlaceOfFluentsFlip` green.
20. Update BRIDGE-ACCORDION (-005, -007, new requirement, edge case, invariant, Decisions).
21. Tempo: delete the `Expander.done /template/ PathIcon#PART_PathIcon` style and the `:not(.semi-transitions)` `Transitions` style with its audit comment. In ARCHITECTURE §5 "Semi.Avalonia (Dark), not Fluent", drop "and clear theme transitions on properties Presto animates" if no other such style remains.
22. Visual check on Desktop (Tempo, `--motion full` and `reduced`): the Done group's chevron springs once, with no lag or double ease, both ways.

### F

23. Add the BRIDGE-POPOVER non-goal and decision. In Tempo's `ShellView.axaml.cs`, reword the two comments that cite "(BACKLOG, BRIDGE-POPOVER)" to state the reason directly.

### Closing

24. Remove the resolved backlog lines: the six Tempo findings above, the BRIDGE-HELPERS status line's "plus no test that every shorthand has a typed builder method", and the BRIDGE-SUBMIT status line's "plus no guard against overlapping `Run` calls".

## Open questions

None.
