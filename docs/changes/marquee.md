# Marquee

## Why

Continuously scrolling content (logo walls, news and price tickers, a "now playing" title too long for its label) is a common UI pattern, and nothing in Presto provides it. Building it from parts is harder than it looks: a seamless loop needs the content duplicated and measured, the period recomputed on resize, copies hidden from screen readers and keyboard focus, a pause that eases rather than stops dead, and a reduced-motion form. A looping `"X"` animation gets none of this right, so it belongs in a control, beside `Pager` and `ToastHost`.

## What

A new `Marquee` control in `MonacoLabs.Presto.Controls`:

```xml
<presto:Marquee Speed="60" Gap="48" FadeEdges="32" Content="{Binding Logos}">
  <presto:Marquee.ContentTemplate>
    <DataTemplate>
      <ItemsControl ItemsSource="{Binding}" ItemTemplate="{StaticResource Logo}">
        <ItemsControl.ItemsPanel><ItemsPanelTemplate><StackPanel Orientation="Horizontal" Spacing="48"/></ItemsPanelTemplate></ItemsControl.ItemsPanel>
      </ItemsControl>
    </DataTemplate>
  </presto:Marquee.ContentTemplate>
</presto:Marquee>

<presto:Marquee Mode="WhenOverflowing" Content="{Binding TrackTitle}"/>
```

- **Content.** `Marquee` is a `ContentControl`. The content is laid out once at its desired size along the scroll axis (unconstrained on that axis) and repeated end to end, `Gap` apart, enough times to cover the viewport plus one period. When `Content` is data, each copy is a real element built from the same `ContentTemplate` (so buttons in copies are clickable). When `Content` is a `Control`, which cannot have two parents, the copies are `VisualBrush` mirrors of it that take no hits.
- **Motion.** A private strip moves along the axis at `Speed` DIPs per second in `Direction` (`Left`, `Right`, `Up`, `Down`), wrapping its phase modulo the period (content length + `Gap`) so the loop has no seam. The strip moves through the render-transform composer's `"X"`/`"Y"` shorthand, so it costs a transform per frame and no layout.
- **Pausing.** `IsPaused` stops the strip (two-way bindable, for a pause button). With `PauseOnHover` (default true) the strip also stops while the pointer is over it or keyboard focus is inside it. All speed changes ease: to a new `Speed`, to 0 on pause and back on resume, over `Recipes.Tokens.Medium` with `Recipes.Tokens.Ease`.
- **Fade edges.** `FadeEdges` (DIPs, default 0) fades the leading and trailing edges through an opacity mask.
- **Overflow mode.** With `Mode="WhenOverflowing"` the marquee only scrolls when the content is longer than the viewport. Otherwise it shows the content once, aligned by `HorizontalContentAlignment`/`VerticalContentAlignment`, with no copies and no ticking.
- **Reduced motion.** While `ReducedMotion` is set, the marquee does not move: it rests at phase 0 with the content start-aligned and clipped.

Nothing else changes. The control is independent of `Pager`, and the core is untouched.

## Decisions

- **One `ContentControl`, not an `ItemsControl`.** Most marquees are one row, and a list is just a `ContentTemplate` with an `ItemsControl` in it, as above. Reason: a single content repeated end to end is the unit of the loop. An `ItemsControl` would need its item containers generated several times over, which its generator does not support. Rejected: `ItemsSource`/`ItemTemplate` on the marquee itself.
- **Data content is copied as real elements; control content as `VisualBrush` mirrors.** Reason: real copies keep interaction working in a logo wall of links, and a control instance cannot be parented twice. Rejected: always mirroring (copies dead to the pointer); requiring data content (breaks `<presto:Marquee><TextBlock …/></presto:Marquee>`).
- **Copies are invisible to accessibility and keyboard.** Each copy host gets `AutomationProperties.AccessibilityView="Raw"` and `KeyboardNavigation.TabNavigation="None"`, so screen readers and Tab see the content once. Reason: duplicated announcements and tab stops are the classic marquee accessibility failure.
- **The engine drives the phase, not the compositor.** The marquee runs an engine timer that advances the phase by the current speed times elapsed engine time. The current speed is an `Animatable` channel, so every speed change eases from the speed currently shown, and a pause decelerates. Reason: compositor loops can only change speed by recompiling (BRIDGE-THREAD-035/037), so pausing would snap. Engine-driven motion also tests deterministically under `ManualFrameSource`, pauses with `Engine.Hidden` and the DevTools freeze, and resumes without a jump. Rejected: an infinite linear `"X"` loop on the compositor, at least as the default (see Open questions).
- **`Speed` is in DIPs per second whatever the engine's `TimeUnit`.** Reason: it is a physical rate, not a duration, so seconds mode must not rescale it.
- **Direction is signed velocity.** Switching between `Left` and `Right` (or `Up` and `Down`) eases through 0 and keeps the phase. Switching axis resets the phase to 0. Reason: a direction toggle should look like a reversal, not a jump.
- **Pausing on hover and focus follows `Pager`'s autoplay rules.** Focus that arrives by Tab or directional navigation pauses; focus that arrives by pointer or programmatically does not (BRIDGE-PAGER-023/024). Reason: one consistent hold rule across controls. A pause mechanism is also what WCAG 2.2.2 (Pause, Stop, Hide) asks of auto-moving content.
- **Reduced motion is static, not slower.** Reason: continuous motion is exactly what reduced-motion users opt out of. A slower crawl still moves.

## Spec changes

New spec `docs/specs/avalonia-bridge/marquee.md`, added to the Controls table in `docs/specs/avalonia-bridge/index.md`:

```yaml
id: BRIDGE-MARQUEE
title: Marquee
package: MonacoLabs.Presto.Avalonia
namespaces: [MonacoLabs.Presto.Controls]
sources: [Controls/Marquee.cs, Controls/MarqueeDirection.cs, Controls/MarqueeMode.cs]
types: [Marquee, MarqueeDirection, MarqueeMode]
tests: [MarqueeTests, ControlsPageTests]
depends: [BRIDGE-TRANSFORMS, BRIDGE-TOKENS, BRIDGE-INIT, CORE-ANIMATABLE, CORE-TIMER, CORE-REDUCED]
```

Draft requirements:

**Layout and copies**
- The marquee shall measure its content unconstrained along the scroll axis and constrained to the available size across it.
- The period shall be the content's length along the axis plus `Gap`.
- While scrolling, the marquee shall show the content followed by as many copies as cover the viewport plus one period, each one period after the previous.
- When `Content` is not a `Control`, each copy shall be built from the resolved `ContentTemplate` over the same `Content`.
- When `Content` is a `Control`, each copy shall be a `VisualBrush` mirror of it that is not hit-test visible.
- Each copy host shall set `AutomationProperties.AccessibilityView` to `Raw` and `KeyboardNavigation.TabNavigation` to `None`.
- When the marquee is arranged at a new size, or the content's length or `Gap` changes, the marquee shall recompute the period and the copy count and keep the phase modulo the new period.
- The marquee shall clip its content to its bounds.

**Motion**
- While scrolling, the marquee shall advance the phase every engine frame by the current speed times the elapsed engine time in seconds, wrapped into [0, period).
- The marquee shall write the phase to the strip's `"X"` shorthand (horizontal) or `"Y"` shorthand (vertical), negated for `Left` and `Up`.
- When `Speed` changes, the current speed shall ease from its displayed value to the new one over `Recipes.Tokens.Medium` with `Recipes.Tokens.Ease`.
- When `Direction` changes along the same axis, the current speed shall ease through 0 to the opposite sign, keeping the phase.
- When `Direction` changes axis, the marquee shall reset the phase to 0 and re-lay out the copies along the new axis.
- When `Speed` is 0, or the period is 0, the marquee shall rest and stop requesting frames.

**Pausing**
- While `IsPaused` is true, the current speed shall ease to 0, and when it becomes false, back to `Speed`.
- While `PauseOnHover` is true and the pointer is over the marquee, or keyboard focus inside it arrived by Tab or directional navigation, the marquee shall pause as for `IsPaused`, without changing `IsPaused`.
- Focus that arrives by pointer or programmatically shall not pause the marquee.
- Once paused and settled at speed 0, the marquee shall stop requesting frames.

**Overflow mode**
- While `Mode` is `WhenOverflowing` and the content's length is no greater than the viewport's, the marquee shall show the content once with no copies, placed by its content alignment on the axis, and shall not tick.
- When the content starts overflowing in that mode, the marquee shall start from phase 0, and when it stops overflowing, it shall return the strip to rest.

**Fade edges**
- While `FadeEdges` is above 0, the marquee shall apply an opacity mask that ramps from transparent to opaque over `FadeEdges` DIPs at both ends of the axis, with the ramp capped at half the viewport.

**Reduced motion**
- While `ReducedMotion` is set, read when it is next checked (each attach, size change and frame), the marquee shall set the phase to 0 and rest.

**Lifetime**
- When the marquee is detached, it shall stop its timer and keep its phase; when attached again, it shall continue from that phase.
- While the engine is hidden or frozen, the phase shall not advance, and it shall continue without a jump afterwards.

**Defaults**
- When not set: `Speed` 60, `Direction` `Left`, `Gap` 24, `FadeEdges` 0, `PauseOnHover` true, `IsPaused` false, `Mode` `Always`.
- A binding to `IsPaused` that names no mode shall bind it two-way.

Allocation: the per-frame tick shall not allocate.

Non-goals: drag or wheel to scrub the strip; a bounce (back-and-forth) mode for overflowing labels; an `ItemsSource` on the marquee itself; per-item speed or parallax lanes; a `MotionValue` exposing the phase; bringing a keyboard-focused copy or element into view.

## Tasks

1. `MarqueeDirection`, `MarqueeMode` and the `Marquee` control: measure/arrange with copies, the engine-driven phase over an `Animatable` speed channel, pausing, overflow mode, fade mask, reduced motion and lifetime. Tests (`MarqueeTests`, headless with `ManualFrameSource`): the phase advances at `Speed` DIPs/s in both time units; a seamless wrap at the period; the copy count follows a resize; data content builds real, clickable copies and control content builds mirrors; copies are `Raw` and not tabbable; a speed change and a pause ease rather than snap; a same-axis direction flip passes through 0 keeping the phase; hover and Tab focus pause but pointer focus does not; `WhenOverflowing` rests when the content fits and starts when it overflows; reduced motion rests at phase 0; detach and hidden keep the phase; the tick allocates nothing (allocation test).
2. Write `docs/specs/avalonia-bridge/marquee.md` from the draft above with `Tests:` lines, add it to the bridge index, and let `SpecCoverageTests` pass.
3. Sample: a Marquee section on `ControlsPage` with a clickable logo wall (data content, fade edges, pause button bound to `IsPaused`), a vertical ticker, a `WhenOverflowing` title, and a speed slider. Cover it in `ControlsPageTests`.
4. Visual check on the Desktop head (`--page controls`, full and reduced motion): no seam at the wrap, the fade, the eased pause on hover, and a resize while scrolling.
5. README: add `Marquee` to the controls list.

## Open questions

- Render-thread mode: should `Marquee` offer an opt-in `RunOnCompositor` (infinite linear loop on the render thread, with instant speed changes and pauses) for heavy UI-thread pages? Recommendation: leave it out of v1 and revisit if the sample stutters under load.
- Should hover slow the marquee (a `HoverSpeed` factor) rather than stop it? Recommendation: keep `PauseOnHover` as a stop for v1. A slowdown does not satisfy WCAG 2.2.2.
- Default `Speed` and `Gap`: 60 DIPs/s and 24 DIPs are guesses; settle them against the sample.
