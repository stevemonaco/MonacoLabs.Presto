# Page transitions — a Presto `IPageTransition`

Phase 31 in [PLAN.md](PLAN.md). Status: **planned**. Avalonia's page hosts take an `IPageTransition`, and the ones it
ships (`CrossFade`, `PageSlide`, `Rotate3DTransition`) are fixed shapes on Avalonia's own animation system: no Presto
eases or springs, no recipes, no reduced motion, no render-thread playback. This phase adds one adapter that plays two
`AnimationBuilder`s, so every host uses Presto motion from XAML with no code-behind.

| Phase | Scope | Size |
|---|---|---|
| 31 | `PrestoPageTransition` (timed and progress-driven), page recipes and markup extensions, Presence and Connected interplay | medium |

## 0. What Avalonia 12.1.2 does (verified against the source)

- **The contract** (`src/Avalonia.Base/Animation/IPageTransition.cs`) is one method,
  `Task Start(Visual? from, Visual? to, bool forward, CancellationToken)`. Either side may be null (a modal shown or
  dismissed). 12.x adds `IProgressPageTransition` (`IProgressPageTransition.cs`): `Update(progress, from, to, forward,
  pageLength, visibleItems)` for swipe- or offset-driven progress and `Reset(Visual)` to clear what `Update` applied;
  `visibleItems` (`PageTransitionItem`) is non-empty only when a carousel shows several pages at once.
- **The built-ins own the end state.** `CrossFade.Start` and `PageSlide.Start` set `to.IsVisible = true` first and, when
  not cancelled, `from.IsVisible = false`; `PageSlide` also clears both `RenderTransform`s. On cancellation they return
  normally and leave values where they stand. `PageSlide` reads its distance from the common visual parent's `Bounds`
  and throws when `from` and `to` have different parents. `CompositePageTransition` runs its children in parallel with
  `Task.WhenAll`.
- **`TransitioningContentControl`** (`src/Avalonia.Controls/TransitioningContentControl.cs`) has two presenters and
  alternates between them; `from` and `to` are the **presenters**, not the content. Content is placed in `UpdateContent`
  (the new view attaches there), the transition starts in the next `ArrangeOverride` after the base arrange, and
  `forward` is `!IsTransitionReversed`. A new swap cancels the running token first, and the presenter that was `to`
  becomes the next `from` mid-animation. On completion the continuation sets the old presenter's `Content = null`
  *then* `IsVisible = false`. The first content after attach is shown without a transition.
- **`Carousel`** (`VirtualizingCarouselPanel.cs`) passes realized item containers, computes `forward` from the index
  change, cancels and recycles the previous `from` when a new selection arrives, and recycles `from` after completion
  (`IsVisible = false`, then `Reset` when the transition is an `IProgressPageTransition`). Swipes and offset animations
  drive `Update` instead of `Start`. `Carousel.GetTransitionAxis` special-cases `PageSlide` to pick its layout axis.
- **`NavigationPage`** (`src/Avalonia.Controls/Page/NavigationPage.cs`) passes its two page presenters with
  `forward = !isPop`, sets their `ZIndex` itself (incoming above on push, outgoing above on pop), and after a completed
  transition hides the old presenter and writes `RenderTransform = null` and `Opacity = 1` as local values. Modals use
  `ModalTransition` with one side null. `TabbedPage` forwards its `PageTransition` to its `TabControl`.

## 1. Decisions

1. **One adapter type, `PrestoPageTransition`,** implementing `IPageTransition` and `IProgressPageTransition`. It holds
   builders, not animations: `Enter` plays on `to`, `Exit` on `from`, and each `Start` builds fresh params
   (`ToParams()`), so `Recipes.Tokens`, reduced motion and function values are read per transition. It is a plain
   sealed class with settable properties, like `CrossFade`, so it is XAML-constructible and shareable across hosts.
   The name follows `PrestoEasing`, the bridge's other adapter of an Avalonia interface.
2. **It animates the hosts it is given.** `from` and `to` are presenters or containers; Presto animates them and never
   reaches into the content. Distances that depend on size (a full-width slide) are function values that read the
   common visual parent's `Bounds`, as `PageSlide` does, because a `NavigationPage`'s incoming page has not been
   measured when `Start` runs.
3. **Entrances state start values, exits end values** (decision 34 in PLAN §7), so `Recipes.FadeUp` and
   `Recipes.FadeOut` work as `Enter`/`Exit` unchanged. Staging (Material's fade-through: exit, then enter) is a `Delay`
   on `Enter`; there is no sequential mode.
4. **Backward navigation reverses the pair by default.** When `forward` is false and no `BackEnter`/`BackExit` is set,
   the incoming host plays `Exit` reversed and the outgoing host plays `Enter` reversed, which is exactly the mirrored
   motion a push/pop or a slide needs. Reversal plays the eases backwards (an out-ease becomes an in-ease); a pair that
   must keep its ease shape sets `BackEnter`/`BackExit` explicitly.
5. **Stacking is part of the transition.** `Stacking` (default `IncomingAboveForward`: incoming above going forward,
   outgoing above going back) writes the two hosts' `ZIndex` at `Animation` priority for the transition's life and
   reverts it after, so `TransitioningContentControl`'s alternating presenters stack like `NavigationPage`'s, and
   `NavigationPage`'s own local `ZIndex` is uncovered afterwards. `Stacking.Host` leaves the host's order alone.
6. **Render thread when it can.** `UseCompositor` (default null = auto) plays both sides through `AnimateOnCompositor`
   when every property of the builders in play has a composition counterpart (`CompositorProperty.Find`), and on the UI
   thread otherwise; true throws `NotSupportedException` as `AnimateOnCompositor` does, false forces the UI thread. A
   page transition is the moment the UI thread is busiest (template instantiation, first layout of the new page),
   which is why auto is the default. Progress-driven `Update` always seeks UI-thread animations: a compositor
   animation seeks by recompiling, which is too costly per pointer move.
7. **End state.** `to.IsVisible = true` before anything plays and the incoming host is set to its start values in the
   same call, so its first rendered frame is never at rest. On completion, `from.IsVisible = false` is written *first*
   and then both animations and the `ZIndex` writes are reverted, so the hosts return to their base values without a
   visible snap and nothing is left at `Animation` priority where a host's local reset (`NavigationPage`) could not
   reach it. The task completes normally; it never faults for cancellation.
8. **Cancellation hands over, it does not revert.** When the token is cancelled, both animations are cancelled where
   they stand and the task completes. Each host remembers the transition driving it, and a later `Start` that is given
   that host takes it over from its current values: `TransitioningContentControl`'s mid-entrance presenter becomes the
   next `from` and exits from where it is. A host no `Start` claims before the next frame is reverted, so switching a
   host to another transition or to none leaves nothing behind.
9. **Progress-driven transitions seek.** `Update` creates the two animations paused on first use for a (from, to,
   forward) triple and seeks them to `progress × duration`; `Reset(visual)` reverts that visual's animation. When
   `visibleItems` is non-empty (a carousel showing peeking neighbours) `Update` does nothing, as `PageSlide` does.
10. **Presence sees a page transition as navigation.** Today a view swapped into a `TransitioningContentControl`
    arrives in a loaded presenter and classifies as an `Enter`, and on completion the host nulls the old presenter's
    content while it is still visible at opacity 0, so `ClassifyDeparture` sees a lone element leaving a loaded parent
    and ghosts it: an `Exit` on a page root pops back into view after the fade. Two changes in `Presence`:
    - Content of a presenter or container whose host's `PageTransition` is a `PrestoPageTransition` classifies its
      arrival as `Initial` and its departure as `Unload`, as a page navigated in or away already does. Rows inside the
      page keep their `Initial` stagger, which then plays under the page transition, the intended layering.
    - Independently of Presto's transition, a departure whose parent chain is not effectively visible or has an
      effective opacity of 0 is `Unload`: nothing is ghosted that was not on screen. This also fixes the pop under
      Avalonia's own `CrossFade`.
11. **Connected flights compose with the host's motion.** A claimed element flies on its composition `Translation`
    inside the incoming host, so a transition that moves the host adds its motion to the flight's path (the flight
    still lands right). Opacity- and scale-led pairs (`PageFade`, `PageFadeThrough`) are the documented pairing for
    connected navigation; counter-moving keyed elements is an open question (§6).
12. **Reduced motion needs nothing new.** Builders carry their `WhenReduced` alternatives (decision 35) and
    `ReduceAll` collapses the rest (decision 36). Every page recipe supplies a short crossfade alternative.

## 2. API

```csharp
public sealed class PrestoPageTransition : IPageTransition, IProgressPageTransition
{
    public AnimationBuilder? Enter { get; set; }       // plays on `to`; null leaves it still (it is shown)
    public AnimationBuilder? Exit { get; set; }        // plays on `from`; null leaves it until it is hidden
    public AnimationBuilder? BackEnter { get; set; }   // forward == false; default: Exit reversed
    public AnimationBuilder? BackExit { get; set; }    // forward == false; default: Enter reversed
    public PageStacking Stacking { get; set; } = PageStacking.IncomingAboveForward;
    public bool? UseCompositor { get; set; }           // null = auto
}

public enum PageStacking { IncomingAboveForward, IncomingAbove, OutgoingAbove, Host }
```

Page recipes on the `Recipes` facade (a new `Prebuilt.Pages` family), each returning a new transition that reads
`Recipes.Tokens` when called, all transform and opacity only so they run on the compositor:

| Recipe | Motion |
|---|---|
| `PageFade(duration?)` | crossfade |
| `PageFadeThrough(duration?)` | Material fade-through: exit fades fast, enter fades and scales from 0.92 after it |
| `PageSharedAxis(PageAxis axis, distance?)` | Material shared axis: short slide plus fade along X, Y or Z (scale); `PageAxis` is a new enum |
| `PageSlide(Edge edge)` | both pages slide the full width or height, incoming from `edge` |
| `PagePush(Edge edge)` | iOS push: incoming slides over from `edge`, outgoing moves a third of the way and dims |

```xml
<TransitioningContentControl Content="{Binding CurrentPage}" ClipToBounds="True"
                             IsTransitionReversed="{Binding IsBack}"
                             PageTransition="{presto:PagePush Edge=Right}" />

<TransitioningContentControl.PageTransition>
    <presto:PrestoPageTransition Enter="{presto:FadeUp Delay=90}" Exit="{presto:Fade Out=True Duration=90}" />
</TransitioningContentControl.PageTransition>
```

`RecipeExtension` provides the exit form when its target is `PrestoPageTransition.Exit` or `BackExit`, as it does for
`Presence.Exit`, so `Exit="{presto:Fade}"` works without `Out=True`. Code: `host.PageTransition = Recipes.PagePush(Edge.Right);`
or `new PrestoPageTransition { Enter = Recipes.SlideIn(Edge.Right), Exit = ... }`.

## 3. Implementation steps

1. **Verify two assumptions** in the Desktop sample before building on them: what `IProvideValueTarget.TargetProperty`
   is for a CLR property of a plain object in compiled XAML (decides how `RecipeExtension` detects `Exit`), and the
   Presence pop of decision 10 under Avalonia's `CrossFade` today. Record both in §7.
2. **`PrestoPageTransition.Start`:** builder selection by `forward`, reversal, stacking writes, compositor predicate,
   show-and-set-start-values, completion order (hide, then revert), the per-host takeover registry and the
   unclaimed-host revert on the next frame.
3. **`IProgressPageTransition`:** paused animations per triple, seek, `Reset`.
4. **Presence:** the page-host classification hook and the not-on-screen departure rule.
5. **Page recipes,** their markup extensions (`{presto:PageFade}`, `{presto:PagePush Edge=Right}`, …) and the
   `RecipeExtension` target check. README: `IPageTransition` row in the cheat sheet, a note that slides need
   `ClipToBounds` on the host.

## 4. Tests (`PageTransitionTests`, Avalonia.Headless)

- `Start` on a `TransitioningContentControl` swap: `Enter` plays on the new presenter and `Exit` on the old; at
  completion the old presenter is hidden before either reverts, and both hosts end with no `Animation`-priority value
  on `Opacity`, `RenderTransform` or `ZIndex`.
- The incoming host's first rendered frame is at the enter's start values (pixel test).
- `forward = false` with no back builders plays the reversed pair: a `SlideIn(Edge.Right)` enter becomes an outgoing
  slide to the right; explicit `BackEnter` wins.
- Stacking: `IncomingAboveForward` puts the incoming presenter above going forward and below going back; `Host`
  writes nothing; `NavigationPage`'s own `ZIndex` is intact afterwards.
- Cancellation: a second swap mid-transition completes the first task normally, the mid-entrance presenter exits from
  its current opacity (no jump), and a host no later `Start` claims is reverted after one frame.
- Null sides: a modal-style `Start(null, to, …)` and `Start(from, null, …)` each play one side.
- Compositor auto: transform/opacity builders produce `CompositorAnimation`s; a `Blur` builder falls back to the UI
  thread; `UseCompositor = true` with `Blur` throws `NotSupportedException`.
- Progress: `Update(0.5, …)` seeks both sides to half; `Reset` reverts; non-empty `visibleItems` changes nothing.
- A `Carousel` selection change and a `NavigationPage` push and pop run the transition end to end.
- Presence: under a Presto transition a page root with `Presence.Enter` does not play it, one with `Presence.Initial`
  does, and its `Presence.Exit` produces no ghost; under Avalonia's `CrossFade` the old page's `Exit` no longer ghosts
  after the fade.
- Reduced motion: `PrestoDefaults.ReducedMotion` plays each page recipe's crossfade alternative.
- Recipes: each page recipe's builders pass `CompositorProperty.Find` for every property.

## 5. Samples

- **Shell:** `MainView`'s `PageHost` becomes a `TransitioningContentControl` with `Recipes.PageFadeThrough()`, and
  `IsTransitionReversed` follows whether the selected gallery entry moved up the list, so every page change in the
  gallery uses the phase.
- **New Reference page "Page transitions":** a `TransitioningContentControl` with a picker over the five recipes and
  a custom builder pair, the builder printed beside it; a `Carousel` with `IsSwipeEnabled` on a `PageSlide` so swipes
  drive `Update`; a `NavigationPage` push/pop stack with `PagePush`; a Freeze-the-UI-thread button showing the
  compositor path keep playing.
- **Connected XAML showcase:** the cross-fade switch uses `Recipes.PageFade()` in place of Avalonia's `CrossFade`.

## 6. Open questions (confirm before the phase)

- Counter-moving keyed elements: while a claimed `Connected` element flies, subtract its host's transition translation
  so the flight path ignores the page motion. Needs the flight and the page transition to share a clock or at least a
  start frame.
- `Carousel.GetTransitionAxis` only recognises `PageSlide`, so a carousel with a Presto slide lays out horizontally
  whatever the edge. Accept it, or have `PageSlide`/`PagePush` recipes return a `CompositePageTransition` containing a
  zero-duration `PageSlide` with the matching axis purely to steer layout.
- Multi-page carousels (`visibleItems` non-empty): upstream `CrossFade` maps offsets to opacity; whether Presto should
  offer an offset-to-builder mapping for peeking layouts.

## 7. Deviations as built

(Filled in as the phase lands.)
