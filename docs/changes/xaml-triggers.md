# View-model cues and XAML actions

## Why

The XAML triggers react to the visual tree (`OnAttached`), taps (`OnTapped`) and a watched value (`OnChanged` with `Watch`), and all of them can only play an `AnimationBuilder`. Two things a view model needs are therefore code-behind in every app, and Tempo hit both of them five times (BACKLOG, Tempo findings):

- **There is no event a view model can raise for XAML to react to.** A one-off moment like "session completed", "task deleted" or "task added" is a CLR event, and only code-behind can subscribe to it. Tempo's `FocusView.Celebrate` (BRIDGE-PARTICLES, `Recipes.Burst` on `FocusViewModel.SessionCompleted`), `ShellView.ShowUndo` (BRIDGE-TOAST, `ToastHost.Show` on `MainViewModel.TaskDeleted`) and `TasksView.RevealAdded` (BRIDGE-SCROLL, `ScrollToIndex` on `MainViewModel.TaskAdded`) exist only to forward an event.
- **A trigger can only play a builder.** Several effects are not builders: a burst, a toast, a scroll to an item and a glyph morph each have a method with its own arguments. So even a value XAML already watches cannot drive them. Tempo's `FocusView.Morph` and `MorphLabel` (BRIDGE-RECIPES) watch `RemainingText` and `StartPauseLabel` through `PropertyChanged` and call `Recipes.MorphGlyphs`, and they also need a rule that a trigger would have to express: morph a word only into another word, and set a label that appears or hides at once.

Two smaller XAML gaps sit beside them:
- The Undo button in a toast needs a `Click` handler to reach `ToastHost.GetToast(…).Dismiss()` (BRIDGE-TOAST).
- A modal bound through `IsOpen` opens with no origin, and `Open(from)` finds no origin for a button inside a flyout, which sits under another visual root (BRIDGE-MODAL-004). Tempo's `ShellView.ShowConfirm` works around both by converting the button's center through `PointToScreen`/`PointToClient` and calling `Open(Point)`.

The pattern is common outside Tempo too: shake a field when validation fails, confetti on a purchase, a toast on save, scroll to a new message. Each one is code-behind today.

## What

**A core cue.** The core adds `Cue` and `Cue<T>`, a raise-only signal a view model exposes as a property: `public Cue<TaskItemViewModel> TaskDeleted { get; } = new();` and `TaskDeleted.Raise(task)`. It is plain .NET with no Avalonia reference, so a core-only view model can use it. A cue holds no value. Subscribers present when `Raise` runs are notified in the order they subscribed. Both types also implement `IObservable<…>`, so Rx code can consume them.

**Cue triggers on `Animate`.** `Animate.Cue` takes a `Cue` (usually bound: `presto:Animate.Cue="{Binding TaskDeleted}"`) or any `IObservable<object?>`. While the element is attached it reacts to each raise:
- `Animate.OnCue` plays an `AnimationBuilder` on the element under the one-shot rules of `OnChanged`: it restarts on each raise and reverts on completion, on detach or when set to `null`. With this, a field can shake when its view model raises `ValidationFailed`.
- `Animate.DoOnCue` invokes a `CueAction` on the element, passing the raised value.

**Actions on a watched value.** `Animate.DoOnChanged` invokes a `CueAction` on each `Watch` value. Unlike `OnChanged`, it also runs on the first value and tells the action that it is the first. A morph needs this to show the initial text without animating it.

**`CueAction`.** `CueAction` is a public abstract class, designed for inheritance, with one method: `protected internal abstract void Invoke(Visual target, object? value, bool first)`. Apps can write their own actions. The bridge ships four as markup extensions, each acting on the element that carries the property:

| Extension | Target | Does |
|---|---|---|
| `{presto:Burst Count=…, Color=…}` | any `Control` | `Recipes.Burst(target, Count, Color)`; skips the first value |
| `{presto:ShowToast Duration=…, Content=…}` | `ToastHost` | `Show(Content ?? value, Duration)`; skips the first value |
| `{presto:ScrollToItem Alignment=…}` | `ItemsControl` | `ScrollToIndex` to the value's index in `Items`, `Nearest` by default; nothing when the value is not an item or the panel is not realized; skips the first value |
| `{presto:MorphGlyphs Reduced=…, MorphBlank=…}` | `TextBlock` | Sets the first value, and every value while the block is not loaded, as `Text` at once, completing a running morph. Otherwise it calls `Recipes.MorphGlyphs(target, text, Reduced)`. With `MorphBlank=False`, a change to or from empty text is also set at once. It completes its running morph when the block detaches. |

**Two small attached properties.**
- `ToastHost.DismissOnClick="True"` on a `Button` inside a toast dismisses that toast on `Click`. The button's command still runs.
- `Modal.IsOrigin="True"` on an element makes the element's activation (a `Button`'s `Click`, otherwise `Tapped`) the origin for any modal that opens without one in the same dispatcher job. This covers both a bound `IsOpen` and `Open()`.

Separately, `Open(from)` across visual roots goes through screen coordinates instead of finding no origin.

Tempo then spells its five handlers in XAML:

```xml
<presto:ProgressRing x:Name="Ring" presto:Animate.Cue="{Binding FocusCompleted}" presto:Animate.DoOnCue="{presto:Burst Color={x:Static m:TempoMotion.FocusAccent}}" … />
<TextBlock x:Name="Remaining" presto:Animate.Watch="{Binding RemainingText}" presto:Animate.DoOnChanged="{presto:MorphGlyphs Reduced=Instant}" />
<TextBlock x:Name="StartLabel" presto:Animate.Watch="{Binding StartPauseLabel}" presto:Animate.DoOnChanged="{presto:MorphGlyphs Reduced=Instant, MorphBlank=False}" />
<presto:ToastHost x:Name="Toasts" presto:Animate.Cue="{Binding TaskDeleted}" presto:Animate.DoOnCue="{presto:ShowToast Duration=5000}" … />
<ListBox x:Name="TaskList" presto:Animate.Cue="{Binding TaskAdded}" presto:Animate.DoOnCue="{presto:ScrollToItem}" … />
<Button x:Name="ClearButton" presto:Modal.IsOrigin="True" Command="{Binding RequestClearCommand}" />
```

What does not change:
- `OnAttached`, `OnTapped`, `OnChanged` and `Watch` keep their rules, including BRIDGE-ATTACH-015 (`OnChanged` ignores the first value).
- The recipe, toast, scroll and modal methods are unchanged.
- Trigger animations still run on the UI thread (BRIDGE-ATTACH-029).
- Timelines remain code-only (BRIDGE-ATTACH Decisions, "No timelines or keyframes in XAML").

## Decisions

- **A cue is a core object exposed as a property, not a CLR event bound by name.** Reason: a binding already reaches a view-model property in compiled XAML, while subscribing to an event by name needs reflection. That reflection is trim- and AOT-hostile (CORE-TARGETS, ARCHITECTURE §3), and a misspelled name only fails at run time. The core is the home because a core-only view model must be able to raise one, just as BRIDGE-ATTACH lets it pick a builder. Rejected:
  - an `EventName` attached property (reflection);
  - a `bool` or counter property the view model toggles (it has to be reset, and two raises in one frame collapse into one);
  - an `ICommand` the view sets on the view model (it inverts ownership and is still code).
- **`Animate.Cue` also accepts `IObservable<object?>`.** Reason: Avalonia apps on ReactiveUI already expose observables, and accepting them costs one type check. Covariance covers payloads of reference type. A value-type payload uses `Cue<T>`, which the bridge reads through its non-generic base without boxing on the subscriber side. Rejected: `Cue` only, which shuts out Rx view models.
- **A cue holds no value and replays nothing.** Reason: it marks a moment, and a view that attaches later must not burst for a session that ended before it existed. Rejected: a "last value" replay like `BehaviorSubject`.
- **Subscribe while attached, not while the property is set.** Reason: a view model often outlives its view, so a subscription made at set time would keep a detached view alive through the cue. Raises while the element is detached do nothing, as `Watch` changes do (BRIDGE-ATTACH-019). Rejected: weak subscriptions, which add a weak-reference type to the core for what attachment already bounds.
- **One action type with markup extensions, not a property per feature.** Reason: one source (`Cue` or `Watch`) and one action slot work for every effect, and apps can add their own by deriving from `CueAction`. Per-feature properties (`ToastHost.ShowOn`, `Recipes.BurstOn`, `Glyphs.MorphText`…) would be six spellings of the same idea. Rejected:
  - a dependency on Avalonia.Xaml.Behaviors (its actions bind through `AvaloniaObject`s, and the bridge decided against a behaviors layer in BRIDGE-ATTACH Decisions);
  - `AvaloniaObject` actions with bindable settings (the settings Tempo needs are constants).
- **An action acts on the element it sits on.** Reason: markup extension settings cannot hold element bindings, and `#name` lookup does not cross a `UserControl` boundary. Putting the cue on the `ToastHost`, the `ListBox` or the ring itself needs no element reference. An action whose target is the wrong type throws `InvalidOperationException` when it is invoked, as `OnTapped` does on a non-`InputElement` (BRIDGE-ATTACH-014).
- **Builders and actions take separate properties (`OnCue`/`DoOnCue`, `OnChanged`/`DoOnChanged`).** Reason: Tempo's ring watches `RemainingText` to pulse and also needs a cue to burst. If one action slot were fed by both sources, it would burst every second. Typed properties keep `OnChanged` an `AnimationBuilder?`, so existing bindings are unaffected. Rejected: retyping `OnChanged` to `object?`.
- **`DoOnChanged` runs on the first value; `OnChanged` still does not.** Reason: an action that owns a property it writes, as a morph owns `Text`, must set the initial value. Actions that only make a moment (`Burst`, `ShowToast`, `ScrollToItem`) skip it. Rejected: changing BRIDGE-ATTACH-015, which pulsing counters rely on.
- **`MorphBlank` is an option, on by default.** Reason: growing glyphs out of empty text is a legitimate effect. Tempo's label hides while it is empty, so a morph into or out of nothing is wasted there, and that is a property of the label, not of the morph.
- **The modal origin comes from the activating element, held for one dispatcher job.** Reason: in MVVM the button runs a command and the view model opens the modal, so nothing that knows the button calls `Open`. `Button.Click` is raised before the command executes, so the origin is recorded before the bound `IsOpen` flips. Holding it for one job means a later origin-less open, such as a keyboard shortcut, does not grow from a stale button. This keeps the existing decision that "the origin belongs to one opening". Rejected:
  - the focused element as origin (a flyout's button has focus in the popup's top level, not the modal's);
  - an `Origin` property on `Modal` (an element binding would not cross Tempo's `SettingsFlyout` boundary).
- **`Open(from)` under another visual root maps through screen coordinates.** Reason: it is exactly Tempo's workaround and is correct for popups on every desktop head. A root with no screen mapping still gives no origin.
- **`DoOnChanged` treats a change made while the `DataContext` is null as a rebind and sets the value at once.** Reason: a surface move drops and restores the `DataContext` while loaded, which a `Watch` would otherwise see as a change to 0 and back and morph twice; with this, Tempo's settings `Stepper` leaves code-behind. Rejected: leaving the Stepper in code-behind and the finding in the backlog.

## Spec changes

**CORE-CUE** (new, `docs/specs/core/cues.md`; types `Cue`, `Cue<T>`; add to `core/index.md`)
- "`Raise` shall notify the subscribers present when it starts, in subscription order, passing its value (`null` for `Cue`)."
- "A subscription shall end when its `IDisposable` is disposed, including from inside a raise, and later raises shall not reach it."
- "A cue shall keep no value: a subscriber added after a raise shall not receive it."
- "`Raise` with no subscribers shall do nothing and allocate nothing."
- "`Cue` shall implement `IObservable<object?>` and `Cue<T>` shall implement `IObservable<T>`; neither shall call `OnError` or `OnCompleted`."
- Threading: single-threaded like the rest of the core, with no locks.

**BRIDGE-ATTACH** (`xaml-attach.md`; types add `CueAction`)
- Added (new "Cue" section):
  - "While a visual with `Cue` set is attached, `Animate` shall subscribe to the cue. It shall unsubscribe on detach, when `Cue` changes and when `Cue` is set to `null`."
  - "`Cue` shall accept a `Cue` or an `IObservable<object?>`; any other value shall throw `ArgumentException`."
  - "On each raise, `Animate` shall play `OnCue` under the rules of `OnChanged` (-017, -018, -020, -022, -023, -038) and invoke `DoOnCue` with the raised value and `first` false."
  - "A raise while the visual is detached shall play and invoke nothing."
- Added (OnChanged and Watch):
  - "`DoOnChanged` shall be invoked with each `Watch` value while the visual is attached, with `first` true for the first value."
  - "When the visual attaches with a `Watch` value that `DoOnChanged` has not been invoked with, `DoOnChanged` shall be invoked with that value, with `first` true if it was never invoked."
- Changed: -026 (design mode) and -031 (defaults) list the new properties.
- Decisions: the first eight decisions above. The "One-shot triggers stay on `Animate`" decision gains `OnCue`.

**BRIDGE-PARTICLES**
- Added: "`{presto:Burst}` shall provide a `CueAction` that, for a value that is not first, calls `Recipes.Burst` on its target with `Count`, `Color` and `Reduced`, and throws `InvalidOperationException` for a target that is not a `Control`." Types add `BurstExtension`.

**BRIDGE-TOAST**
- Added: "`{presto:ShowToast}` shall provide a `CueAction` that, for a value that is not first, calls `Show(Content ?? value, Duration)` on its `ToastHost` target. It throws `InvalidOperationException` for another target or when both are null."
- Added: "When a `Button` with `ToastHost.DismissOnClick` true raises `Click` inside a toast's container, the host shall dismiss that toast, leaving the click unhandled so the button's command still runs."
- Types add `ShowToastExtension`.

**BRIDGE-SCROLL**
- Added: "`{presto:ScrollToItem}` shall provide a `CueAction` that, for a value that is not first, calls `ScrollToIndex` on its `ItemsControl` target with the value's index in `Items` and `Alignment` (`Nearest` by default). It does nothing when the value is not in `Items` or the items panel is not realized."
- Types add `ScrollToItemExtension`.

**BRIDGE-RECIPES**
- Added: "`{presto:MorphGlyphs}` shall provide a `CueAction` on a `TextBlock` target that sets `Text` at once, completing its running morph, for the first value, for any value while the block is not loaded, and with `MorphBlank` false for a change to or from empty text. Otherwise it calls `MorphGlyphs` with the value's text and `Reduced`. It completes its running morph when the block detaches."
- Types add `MorphGlyphsExtension`.

**BRIDGE-MODAL**
- Changed -004: "…record the center of `from` in the modal's coordinates as the origin, through screen coordinates when `from` is under another visual root, and none when `from` is null or its root has no screen…"
- Added:
  - "When an element with `Modal.IsOrigin` true raises `Click` (a `Button`) or `Tapped` (any other `InputElement`), the bridge shall record its center as the pending origin until the dispatcher's next job."
  - "A modal that opens without an origin while an origin is pending shall open from it, as -004 does."
- Decisions: reword "The origin is an argument to `Open`" to add the pending origin and its one-job lifetime. The Non-goal "no attached form" stays: `IsOrigin` marks an opener, not an overlay.

## Tasks

1. **Core cue.** Add `Cue`/`Cue<T>`, using copy-on-write subscriber arrays so `Raise` allocates nothing.
   - Tests (`CueTests`): order; unsubscribe inside a raise; a late subscriber gets nothing; `Cue<int>` raises without boxing; `IObservable` interop; a zero-allocation raise with no subscribers and with one (`AllocationTests`).
   - Write CORE-CUE and add it to `core/index.md`.
2. **`Animate.Cue`, `OnCue`, `DoOnCue`, `DoOnChanged`, `CueAction`.** Extend `Animate.cs`'s `Trigger` with the subscription lifetime above.
   - Tests (`AnimateAttachedTests`):
     - `OnCue` plays, restarts on a second raise and reverts on completion;
     - a raise while detached plays nothing;
     - detaching unsubscribes (a collected view is not kept alive by its cue);
     - an `IObservable<object?>` source works;
     - a bad `Cue` value throws;
     - `DoOnChanged` sees the first value with `first` true, and on reattach sees a value that changed while detached;
     - design mode does nothing.
   - Update BRIDGE-ATTACH.
3. **Action extensions.** Add `BurstExtension`, `ShowToastExtension`, `ScrollToItemExtension` and `MorphGlyphsExtension`, each with its internal action.
   - Tests:
     - `Burst` on a raise emits (`ParticleSceneTests`), and on a non-`Control` throws;
     - `ShowToast` shows the raised item and throws on a non-host;
     - `ScrollToItem` brings an added last item into view with `Nearest` and ignores a value that is not an item (`ScrollToIndexTests`);
     - `MorphGlyphs` sets the first value at once, morphs a later one, sets at once while unloaded, honors `MorphBlank=False` both ways and completes on detach (`GlyphRecipeTests`).
   - Update BRIDGE-PARTICLES, -TOAST, -SCROLL and -RECIPES.
4. **`ToastHost.DismissOnClick`.**
   - Tests (`ToastHostTests`): the click dismisses the toast and the command runs.
   - Update BRIDGE-TOAST.
5. **Modal origin.** Add `Modal.IsOrigin` and the pending origin. Make `Open(from)` work across roots.
   - Tests (`ModalTests`):
     - a bound `IsOpen` flipped by an `IsOrigin` button's command reveals from the button;
     - an origin-less open in a later job has no origin;
     - `Open(from)` with `from` in a popup gets the screen-mapped center.
   - Update BRIDGE-MODAL.
6. **Tempo adopts it.**
   - `FocusViewModel` exposes `Cue FocusCompleted` and a bindable summary.
   - `MainViewModel` turns `TaskDeleted` and `TaskAdded` into cues.
   - Delete `FocusView.Morph`, `MorphLabel`, `Celebrate` and `OnLoaded`'s text writes, `TasksView.RevealAdded`, and `ShellView.ShowUndo` and `Undo`. The Undo button gets `DismissOnClick` and binds `UndoDeleteCommand`.
   - `ShowConfirm` keeps only the flyout hide and `Dialog.Reset()`, with the confirm opened through the bound `IsConfirmOpen` and `IsOrigin` on the Clear and Restore buttons.
   - `Tempo.Tests` keep passing, and their deleted-handler tests move to the view-model cues.
7. **Gallery.** Add a Builder page section, "Cues", showing a validation shake (`OnCue`), a toast on save and a burst, with a printed snippet.
8. **Visual checks** on Desktop:
   - Tempo's countdown morphs only the changed digits;
   - the start label morphs word to word and appears and hides at once;
   - a completed focus session bursts from the ring;
   - delete then Undo shows and dismisses the toast;
   - a new task scrolls into view at the list's end;
   - Clear all data in the wide flyout and in the compact sheet grows the confirm from its button;
   - the gallery Cues section.
9. **Close.** Remove the resolved BACKLOG lines: BRIDGE-PARTICLES (`Recipes.Burst` is code-only), BRIDGE-TOAST, BRIDGE-RECIPES (`MorphGlyphs` is code-only), BRIDGE-MODAL (opening from an element) and BRIDGE-SCROLL (bringing an added item into view). Then add the remaining items listed under Open questions.

## Open questions

None. Out of scope here:
- **Popups above the scrim.** A modal opened while a light-dismiss flyout is open draws under the flyout, so `ShowConfirm` still hides the flyout itself. Avalonia exposes no list of open popups, so `Modal` does not close them; it goes back to the backlog under BRIDGE-MODAL.
- **First value of `OnChanged` (out of scope here).** A new dot that is already filled does not pop: BRIDGE-ATTACH-015 skips first values, and the Tempo finding is about that rule rather than a missing trigger. A `Presence.Enter` on a filled-dot style may already cover it. It stays in the backlog.
