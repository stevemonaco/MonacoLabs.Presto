# Declarative layout animation — `AnimateLayout` from XAML

Phase 32 in [PLAN.md](PLAN.md). Status: **steps 1–4 built; sample migration (step 5) pending**. This plan follows the post-Phase-27 gap review. Framer Motion's most
used feature is its `layout` prop: any change of an element's layout box eases instead of jumping. Presto already has the
engine for it, `AnimateLayout` (COMPOSITOR-PLAN §4.4, FLIP inverted onto the composition `Translation`), but only from code.
Every list in the sample arms it from a `ContainerPrepared` handler inside a scope, and every panel re-arms its children in
`OnAttached`. This phase adds no motion capability. It makes the existing capability declarative, tied to the element's
attachment, and correct for recycled containers.

| Phase | Scope | Size |
|---|---|---|
| 32 | `Animate.Layout`, `Animate.LayoutMode` and `Animate.ItemLayout` attached properties, `{presto:Reflow}` markup extension, recycle and scroll rules, sample migration | small–medium |

## 1. How it works today

- `AnimateLayout(control, …)` constructs a `LayoutTransition` that stays armed: it subscribes to `LayoutUpdated` and eases
  every later move until `Revert`. It is not a one-shot.
- One live transition per control (`LayoutTransition.s_live`): arming again reverts the previous one. `GalleryFlipPage.Rearm`
  relies on this to switch mode.
- First is measured at construction (`_first = first ?? Measure(control)`). A control not yet in a `TopLevel` measures
  null, so its first laid-out pass records instead of easing.
- Scrolling is not a move: `Measure` adds back every `ScrollContentPresenter.Offset`, so positions are in content
  coordinates.
- `Connected` flights are the same type, built with `once: true` and a first rect taken from another control.
- Nothing reverts a transition when its control detaches. A recycled container keeps its transition, and one re-armed from
  `ContainerPrepared` measures its *old* slot as First. On a virtualizing panel, that means a row reused for another item
  animates from where it last stood. No sample shows this today only because both list call sites use non-virtualizing
  panels.

## 2. Decisions

1. **Home: `Animate`, named `Layout`.** `presto:Animate.Layout` reads as the method it wraps, `AnimateLayout`, and decision
   17 in PLAN §7 already makes `Animate` the home of attachment-scoped triggers, so no new class is needed.
   - A `Layout` class was rejected. It shadows the `Avalonia.Layout` namespace inside any `namespace Avalonia.*` block,
     which is the reason for decision 6.
   - `Motion` was rejected because it means state motion (decisions 24–28). `Presence` was rejected because it means
     arrival and departure moments. Layout animation is neither.
2. **Value: `AnimationBuilder?`,** like every other lifecycle property. It goes through `LayoutMotion.From`, so only
   timing, ease and the per-axis eases `.X(ease:)`, `.Y(ease:)` and `.Scale(ease:)` are allowed. Anything else throws
   `NotSupportedException` when the property is applied, exactly as the code API does.
   - The mode is a separate `Animate.LayoutMode` (`LayoutAnimateMode`, default `Position`), so a style can set it on its
     own and a `ToggleButton` can bind it.
3. **XAML shorthand: `{presto:Reflow}`.** A `RecipeExtension` over a new `Recipes.Reflow(duration, delay, ease)` factory.
   - With no arguments it uses `Recipes.Tokens.Medium` and `Recipes.Tokens.Ease`, so `Animate.Layout="{presto:Reflow}"`
     is the whole ceremony.
   - `{presto:Reflow 320}` sets the duration, and `Ease=` is parsed by `EaseParser`.
   - It has no exit form: `Out` is ignored.
   - Its `WhenReduced` alternative is an instant move: a reflow is a spatial change, and reduced motion should land it
     rather than shorten it.
4. **Lifetime is attachment,** following `Animate.OnAttached`'s `Trigger`:
   - Attaching arms a transition. Detaching reverts it, which writes identity.
   - Changing the builder or the mode reverts the live transition and arms a new one; `null` stops listening.
   - Design mode does nothing.
   - Arming never registers with an ambient scope, because the attachment is the scope.
5. **Arming on attach records; it never eases.** The attached form passes "First unset" so its first laid-out pass only
   records, whatever the control's bounds said when it attached.
   - This needs an internal `LayoutTransition` option. Today's constructor measures at once, which is right for the code
     API ("arm, then change layout in the same pass") but wrong for a container arriving with stale bounds.
   - A control appearing is never a layout move. Its entrance belongs to `Presence`.
6. **Lists: `Animate.ItemLayout` on the `ItemsControl`.** It arms every item container, replacing the sample's
   `ContainerPrepared` handler and the need for an `ItemContainerTheme` just to reach the containers. `Animate.LayoutMode`
   set on the `ItemsControl` applies to its containers.
   - `ContainerPrepared` arms, or rebases a container that is already armed.
   - `ContainerClearing` rebases.
   - `ContainerIndexChanged` does nothing: an insert or removal above a row is exactly the move that should ease.
   - *Rebase* is a new internal `LayoutTransition.Rebase()`. It cancels any move in flight (writing identity) and forgets
     First, so the next pass records.
   - A recycled container therefore lands at its new index in one frame instead of flying there from its old one.
7. **Scrolling a virtualizing list does not ease.** Offsets are already taken out (§1), but a virtualizing panel also
   repositions realized rows as estimated sizes are corrected during a scroll.
   - A container armed through `ItemLayout` rebases instead of easing on any pass where its `ScrollViewer`'s offset
     changed and its list had no pending collection change.
   - This reuses the offset bookkeeping `Presence.ItemChanges` keeps for `Jumped`, extracted so both can read it.
   - `Animate.Layout` set directly on an element inside a scroller gets the same rule when the element is an item
     container.
8. **Precedence with `Connected` flights.** A keyed flight wins its move; the layout property resumes afterwards.
   - A flight arms a one-shot transition on the same control, which reverts the armed one (one live transition per control).
   - The attachment watches for that. When the flight's `Then()` completes and the control is still attached, it re-arms
     with a rebase.
   - The same happens when code calls `AnimateLayout` or `AnimateSharedElement` on a control that has the property:
     the code call wins, and the property takes over again at the next attach or value change. No re-arm happens after
     a persistent code transition.
9. **Composition with `Presence`: container moves, content arrives.** Entrance and exit recipes run on the compositor's
   `Translation` and `Scale` (decision 34), which are the same tracks a layout transition inverts onto. When both are on
   one element, the later owner takes the track and the other freezes (the registry rule, with its Debug warning).
   - The documented pattern is the one `MicroInteractionsPage` already uses for toasts: layout on the container or a
     wrapper, `Presence` on the content.
   - `ItemLayout` gives lists that split for free: the container reflows while the template content enters, and siblings
     close the gap behind an exit's ghost.
   - An opacity-only or blur entrance does not conflict and may share the element.
   - Detecting the conflict and throwing was rejected, because sequential use (an enter that finishes before the first
     reflow) is legitimate.
10. **Style setters work unchanged.** Every property is a plain attached property, so a style can set them:
    `<Style Selector="StackPanel.accordion > Border"><Setter Property="presto:Animate.Layout" Value="{presto:Reflow}"/>`.
    A setter shares one builder value across elements, and that is safe because `LayoutMotion.From` snapshots it per arm.
11. **Reduced motion follows the builder.** `LayoutMotion.From` calls `ToParams()`, so `WhenReduced` applies when the
    builder is read at arm time.
    - A change of `PrestoDefaults.ReducedMotion` takes effect at the next arm, not mid-life.
    - The attachment re-reads the flag each time it arms, and re-arms on a detected flag change at its next layout pass,
      as `MotionTracker` does for states.
    - `ReduceAll` must make a layout move instant. Whether the mirror's collapsed duration already reaches the compiled
      compositor track is unverified; step 1 pins it with a test and fixes it if not.

## 3. API

```csharp
public static class Animate
{
    public static readonly AttachedProperty<AnimationBuilder?> LayoutProperty;          // on Control
    public static readonly AttachedProperty<LayoutAnimateMode> LayoutModeProperty;      // on Control, default Position
    public static readonly AttachedProperty<AnimationBuilder?> ItemLayoutProperty;      // on ItemsControl
}

public static partial class Recipes { public static AnimationBuilder Reflow(double? duration = null, double? delay = null, EaseValue? ease = null); }
public sealed class ReflowExtension : RecipeExtension { }
```

```xml
<ItemsControl ItemsSource="{Binding Rows}" presto:Animate.ItemLayout="{presto:Reflow 320}">
    <ItemsControl.ItemTemplate>
        <DataTemplate><Border presto:Presence.Enter="{presto:Fade}" presto:Presence.Exit="{presto:Blur}" /></DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>

<Border presto:Animate.Layout="{presto:Reflow}" presto:Animate.LayoutMode="PositionAndSize" />
```

## 4. Implementation steps

1. **`LayoutTransition` internals.** Add a record-first option to the constructor, and `Rebase()`. Extract the
   scroll-offset bookkeeping from `Presence.ItemChanges` into a shared per-list helper. Add a `ReduceAll` test for
   today's `AnimateLayout` and fix the path if it animates.
2. **`Animate.Layout` / `LayoutMode`.** A `LayoutArm` holder beside `Trigger` that handles attach, detach, value changes,
   the re-arm after a flight (decision 8) and the reduced-motion re-read.
3. **`Animate.ItemLayout`.** Subscribe to the list's container events, arm and rebase containers, and read the container
   rebase-on-scroll rule. Unsubscribing reverts every container it armed.
4. **`Recipes.Reflow` and `{presto:Reflow}`,** with the builder extension method's XML docs pointing at the attached form.
   Add README rows next to `AnimateLayout`.
5. **Sample migration (§6),** then watch it on the Desktop head. Per COMPOSITOR-PLAN §4.4, headless green is not
   acceptance for layout motion.

## 5. Tests (`AnimateLayoutAttachedTests`, bridge, headless Skia)

- Setting `Animate.Layout` on an attached control and changing its `Margin` eases the rendered position, asserted with a
  pixel probe as in `LayoutTransitionTests`. Detaching reverts to identity, and re-attaching does not ease the arrival.
- A control attached with stale bounds, and one re-attached somewhere else, record on the first pass and do not fly.
- Changing the builder mid-move reverts to identity and eases the next move with the new duration; `null` stops easing.
  Changing `LayoutMode` to `PositionAndSize` makes a resize scale.
- `ItemLayout` on a non-virtualizing `ItemsControl`: inserting an item at index 0 eases every other container, and the
  new container does not fly from the panel origin.
- `ItemLayout` on a `VirtualizingStackPanel` list:
  - Scrolling by a page eases nothing.
  - A recycled container placed at a new index lands in one frame.
  - Removing a visible item eases the rows below it while the `Presence.Exit` ghost plays.
- A keyed `Connected` flight on an element with `Animate.Layout` flies, and after `Then()` a later `Margin` change
  eases again.
- A style setter applies one `{presto:Reflow}` value to several elements, and each gets its own transition.
- `PrestoDefaults.ReducedMotion` set: a `{presto:Reflow}` move is instant. `ReduceAll`: a plain builder move is instant.
- A builder with a value or a loop throws `NotSupportedException` when applied.
- `RecipeMarkupTests`: `{presto:Reflow 320}` parses its duration and ease.

## 6. Samples

| Call site | Today | Phase 32 |
|---|---|---|
| `EffectsPage` rows (`ItemsControl`, blur-out exits) | `ContainerPrepared` + scope | **Migrates** to `Animate.ItemLayout` in XAML; the printed snippet becomes XAML |
| `StorefrontPage` products (`ItemsControl` + `WrapPanel`) | `ContainerPrepared` | **Migrates** to `Animate.ItemLayout` |
| `MicroInteractionsPage` accordion sections (XAML children) | armed in `OnAttached` | **Migrates** to a style setter with `LayoutMode="PositionAndSize"` |
| `MicroInteractionsPage` toast slot (a `Panel` built in code) | `slot.AnimateLayout` | Stays: built in code, and it is the container/content split decision 9 documents |
| `LayoutPage` cards, grow box, shared square | armed in code, re-armed by the speed slider | Stays: this is the reference page for the code API. Add one XAML card beside it that uses `Animate.Layout` |
| `BoardPage` cards | armed where the card and its `Draggable` are built | Stays: built in code alongside the draggable |
| `GalleryFlipPage` cards | `Arm`/`Rearm` on a mode toggle | Stays: built in code (see §7, moved items). Its mode toggle could bind `Animate.LayoutMode` once the cards are templated |
| `ConnectedPage` tile art and title | armed in `OnAttached` | Stays: built in code by `BuildTile` |

The Lifecycle or Activity feed page gains a short note that layout goes on the container and `Presence` on the content.

## 7. Open questions (confirm before the phase)

- **Moved items that get a new container.** `ItemsControl` rebuilds the container of an item that `ObservableCollection.Move`
  moves (the reason `GalleryFlipPage` builds its cards in code). The new container records and does not ease. An opt-in
  per-item memory (the last slot keyed by item, as `RevealOnce` keys by item) could fly it from where its item stood.
  That is Framer's `layoutId` inside a list. It is worth doing only if a templated sample needs it.
- **Counter-scaling content under `PositionAndSize`.** *Resolved by Phase 43* ([GAPS-PLAN](GAPS-PLAN.md) §3):
  `LayoutMode="PositionAndSizeCorrected"` counter-scales direct children, and `Animate.ContentSize` eases the real size.
- **`ItemLayout` for `ItemsRepeater`-style hosts** without `ContainerPrepared` events. Nothing in the sample uses one.

## 8. Deviations as built

- **No `Rebase()`.** Rebasing is a revert plus a record-first re-arm. `LayoutTransition`'s internal constructor gained
  `recordFirst` (leave First unset so the first pass only records) and `scoped: false` (skip the ambient scope).
  `ContainerClearing` releases the container's arm rather than rebasing it, and `ContainerPrepared` arms it again as if
  attaching, so a recycled container records its new slot.
- **Scroll rule lives on the transition.** `Presence.ItemChanges` and `AfterLayout` moved to an internal top-level
  `ItemChanges` (`ItemChanges.cs`) rather than a new helper, adding `Pending` (a collection change not yet realized) and
  `Scrolled` (an offset change this pass, readable from whichever handler runs first). A transition with the internal
  `List` option cancels to identity and records on a pass that scrolled with nothing pending. `LayoutArm` sets it for
  `ItemLayout` containers and for `Animate.Layout` on an item container, found through `Presence.OwnerOfItem`, now internal.
- **`LayoutArm` has its own file** (`LayoutArm.cs`, internal), not a nested class beside `Trigger`. The list side is
  a private `ItemArms` class in `Animate.cs`, which reads `Animate.LayoutMode` from the list.
- **Flight precedence** uses an internal `Replaced` callback, `Once` and `Live(control)` on `LayoutTransition`. The re-arm
  waits for the flight's `Then()` after the next layout pass, because a flight has nothing to await until it plays. Arming
  while a flight is already in the air follows that flight as well. A transition reverted earlier in the same layout pass
  no longer plays from the event's snapshot.
- **`ReduceAll` needed a fix.** A move whose compiled compositor track has zero duration and zero delay is no longer
  started, because a zero-length composition animation can draw its first key for a frame. It lands in one frame.
- **`Reflow` lives in a new `Prebuilt/Layouts` family,** not `Interactions`, whose "short, reduced keeps colour" summary
  would not fit. `ReflowExtension` takes the duration positionally. `Stagger` makes the delay per-target, which
  `LayoutMotion.From` rejects.
- **Invalid builders throw where they are set.** `Animate.Layout` validates even while detached, and `Animate.ItemLayout`
  validates before any container is realized.
