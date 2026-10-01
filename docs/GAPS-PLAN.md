# Gaps — what GSAP, Motion, Compose and Flutter do that Presto does not

Phases 41–49 in [PLAN.md](PLAN.md). Status: Phase 41 **implemented**; Phases 42–49 **planned**. This plan comes from the post-Phase-40 review against GSAP (with
its plugins), Motion (Framer Motion), Jetpack Compose, Flutter and SwiftUI. It was checked against the committed code
(`0106efe`) and the Avalonia 12.1.2 assemblies.

Phase 41 is designed in full here. Phases 42–49 are design sketches: each one settles its decisions in this document,
or in its own plan document if it outgrows a section, before it starts.

| Phase | Gap | Scope | Size |
|---|---|---|---|
| 41 | Value-driven animation (implemented) | bridge | medium |
| 42 | Derived values | core + bridge | medium |
| 43 | Layout without distortion | bridge | medium–large |
| 44 | Lottie playback | bridge (new package) | medium |
| 45 | Scroll storytelling | bridge | medium |
| 46 | Drag-to-reorder lists | bridge | small–medium |
| 47 | Orchestration | bridge | medium |
| 48 | Presto DevTools overlay | bridge | medium |
| 49 | Small items | core + bridge | small each |

Phase 42 builds on Phase 41's binding plumbing, and Phase 46 builds on `Animate.ItemLayout` (Phase 32). The other
phases are independent.

## 0. Coverage today

At parity or better, so not in this plan:

- **Timing and easing:** timelines, staggers (including grids), keyframes, springs (`Bounce`/`Duration` as well as
  mass, stiffness and damping), and inertia with snapping and bounds.
- **Momentum:** velocity-preserving retargets (Phase 18), plus a public `VelocityTracker`.
- **Values:** gradient and shadow tweens.
- **Arrivals, departures and moves:** `Presence` (≈ `AnimatePresence`, `AnimatedVisibility`, `AnimatedList`),
  `Connected` (≈ Hero, `layoutId`), FLIP layout moves, and page transitions.
- **Interaction:** hover, press and focus states, and drag and multi-touch gestures.
- **Extras:** text splitting, particles, sprite sheets, image effects, render-thread animation, and reduced motion as an
  app-set flag.

Gaps that are known but not planned here:

- **Shaders** (Flutter `FragmentProgram`, Compose `RuntimeShader`, SwiftUI `layerEffect`) remain a non-goal
  (IMAGE-EFFECTS-PLAN §3).
- **Rive** has no official .NET runtime outside Unity.
- **Lottie beyond playback** (dotLottie state machines, or a native interpreter) is covered in §4.1.
- **PLAN §7's open items** stay where they are: relative `To` for blended tweens, per-target modifiers, lazily
  resolved timeline-child targets, and one playback model per `CompositorAnimation`.

### Why `Connected` does not cover derived values

`Connected` (Phases 16–17) is a *shared-element transition*. It covers the same thing shown in two places, with one
element flying from where the other was drawn. It measures two rectangles once and plays one flight between them.
Nothing in it reads a changing value and feeds it to other properties.

*Derived values* (Motion's `useMotionValue`/`useTransform`, GSAP's `quickTo` fed from a scroll or pointer) are a
reactive graph. One live source, such as a scroll offset, drag position, pointer or animated value, maps through
ranges and eases into several properties every frame. `Connected` does not cover this, so it is Phase 42.

## 1. Phase 41 — Value-driven animation

### 1.1 Gap

Compose's `animate*AsState`, Flutter's `AnimatedContainer`/`TweenAnimationBuilder`, SwiftUI's implicit animations
and Motion's `animate` prop all make the same promise: *when this value changes, animate to the new one*. In MVVM that
means a bound view-model property drives a control smoothly, with no code-behind.

Presto has nothing that does this:

- **`Animate.OnChanged`** replays a *fixed* builder when its watched value changes. That gives a pulse, not motion to a
  new value.
- **Avalonia's `Transitions`** cannot host a Presto motion. In 12.1.2, `ITransition.Apply` and `TransitionBase.Apply`
  are `internal abstract`. The only extension point is `InterpolatingTransitionBase<T>.Interpolate(progress, from, to)`,
  where Avalonia owns the duration and the progress. That rules out springs (whose duration comes from their physics),
  velocity carried across an interruption, and reduced-motion handling. `PrestoEasing` only swaps the curve.
- **A custom `BindingBase` subclass is not possible.** In 12.1.2, `BindingBase.CreateInstance` is `internal abstract`,
  so the binding has to be built from parts Avalonia does expose.

Getting this right matters more than anything else in this plan, because it is the feature an MVVM app reaches for
first. It sits behind a spike (§1.4 step 1).

### 1.2 Decisions

1. **The shape is a binding wrapper, written as a markup extension.**

   ```xml
   <Border Width="{presto:Animated {Binding Level}, Spring=Gentle}" />
   ```

   The wrapped binding is an ordinary binding. Its value is the *target*, and the property shows the *animated* value.
   An extension is the right shape because it sits where the binding already is. There is no second attached property
   to keep in step, and changing which binding is animated is a one-attribute change.

2. **Input: the wrapped binding resolves against the control itself.** `ProvideValue` binds the wrapped binding to a
   hidden companion attached property on the *target control*. That way `DataContext`, `ElementName`,
   `RelativeSource`, `TemplatedParent`, converters and `FallbackValue` all resolve exactly as they would on the real
   property.
   - There is one companion property per target property, registered lazily and cached. The registry is
     single-threaded, so this is safe. Avalonia's analyzer flags any registration outside a static initializer
     (`AVP1001`), so the one call is suppressed.
   - A companion change is the "value changed" event.
   - The companion is `object`-typed, so nothing converts what arrives on it. A `FallbackValue` written in XAML
     arrives as a string, and a recycled row's arrives as `null`. The follower converts each value to the property
     type with `TypeUtilities.TryConvert`, as a binding on the real property would. A value that does not convert
     becomes `UnsetValue`.

3. **Output: an ordinary binding at the priority the wrapped binding would have had.**
   - Each use gets a *follower*: an `IObservable<object?>` that the extension returns as `follower.ToBinding()`.
     Avalonia then treats the property as bound at `LocalValue` priority. Inside a template this is also
     `LocalValue`, the same as a bare `{Binding}` there; only literals are held at `Template` (§1.7 Q2).
   - So styles at a lower priority are overridden as usual, DevTools shows a binding expression, and a Presto or
     Avalonia animation at `Animation` priority layers *over* the followed value and reverts back to it.
   - The follower starts at `UnsetValue`. A value-typed property's value store throws if a subscribe hands it `null`.

4. **The follower drives its own animation, not the control's.** Each follower runs a core `Animatable` over an
   internal channel holder. It never registers the control's property with Presto's composition. So a
   `control.Animate(...)` on the same property neither cancels the follow nor is cancelled by it (decision 3 covers the
   layering).

5. **Retargeting.**
   - **Springs:** a new target starts from the *displayed* value and keeps the current velocity. This is Phase 18's
     `PreserveVelocity`, on by default here.
   - **Velocity per channel:** today `PreserveVelocity` applies only to the scalar `Animatable.Set`. This phase extends
     it to `Set(property, IReadOnlyList<double>)` per channel, so colours, thicknesses and points keep momentum too.
     That is the core change in this phase.
   - **Duration-based eases** restart from the displayed value with their full duration, as Compose does.

6. **When the follower snaps instead of animating.**
   - **First value:** the first value snaps. `AnimateInitial = true` opts in to animating from the property's
     pre-binding value instead.
   - **New item:** a value caused by a `DataContext` change on the control snaps. That is a recycled container or a
     swapped item, not the same item changing. `DataContextChanged` on the target always comes first, synchronously,
     so it sets a flag. The next companion change snaps and clears the flag. A `Dispatcher` post at `Background`
     also clears it, in case the new item leaves the value unchanged (§1.7 Q4).
   - **Values that cannot be interpolated:** strings, enums, `bool`, `null`, `UnsetValue`, `BindingNotification`
     errors, or a type change pass through instantly. A `null` on a value-typed property passes through as
     `UnsetValue` (decision 2).
   - **Reduced motion:** with `PrestoDefaults.ReducedMotion` set, the follower snaps unless the extension's
     `WhenReduced` gives a timing.

7. **Lifetime.**
   - On detach, the follower stops ticking and lands on the latest target.
   - On reattach, it starts from the current target with no animation.
   - Disposing the output binding tears down both halves: the follower's subscription is disposed, and it disposes
     the companion binding. Disposing the expression or binding the property again both count. A `ClearValue` or a
     local write does not, because in Avalonia 12 a `LocalValue` binding survives both and the next push restores it
     (§1.7 Q3). The follower behaves the same.

8. **Types.** Anything a Presto codec handles: `double`/`float`/`int` (ints round), `Color`, solid and gradient brushes,
   `Thickness`, `CornerRadius`, `Point`, `Size`, `Rect`, `Vector`, `RelativePoint`, and `BoxShadows`. Once Phase 30
   lands, a `ColorSpace` parameter is passed through.

9. **One-way only.** `TwoWay` or `OneWayToSource` on the wrapped binding throws from `ProvideValue`. Writing animated
   intermediate values back to a view model would be wrong.
   - `BindingBase` has no `Mode`, so it is read from `ReflectionBinding`, `CompiledBinding`, `MultiBinding` or
     `TemplateBinding`.
   - `Default` resolves against the companion, which is `OneWay`. So only an explicit mode throws, even on a
     property that defaults to `TwoWay`, such as `TextBox.Text`.

10. **Timing.** The extension takes `Duration`/`Ease`, or `Spring` (a preset name, or `"bounce,duration"`), or
    `Bounce`/`Duration`, or a `Builder` (an `AnimationBuilder`, taking only its timing and eases, as `Animate.Layout`
    does), or a `Token` from `MotionTokens`. The default is `Springs.Gentle`, which is critically damped (§1.7,
    timing).

11. **Code API, the same machinery.** For example:
    `control.BindAnimated(Border.WidthProperty, source, new AnimatedBindingParams { ... })` returns an `IDisposable`.
    The source can be an `IObservable<T>` or a `BindingBase`.
    - Code needs no `BindingBase`, so `BindAnimated` binds the follower directly with
      `Bind(property, follower, BindingPriority.LocalValue)`.

12. **Not in style setters.** One setter is shared by every control it applies to. With `BindingBase` sealed off,
    there is no per-instance hook, so an extension inside a `Setter` throws with a message that points at templates
    instead. Control templates work, because their content is built per instance.
    - A setter is recognised by `TargetObject is Setter`; its `TargetProperty` is the CLR `Value` property.
    - More generally, `ProvideValue` throws unless `TargetObject` is an `AvaloniaObject` and `TargetProperty` is an
      `AvaloniaProperty` (§1.7 Q1).

13. **Transform and effect shorthands.**
    - **The problem:** `X`, `Y`, `Scale`, `Rotate` and `Blur` are not Avalonia properties, so an extension cannot sit
      on them.
    - **Recommended:** a bridge `Follow` class with attached properties per shorthand, for example
      `presto:Follow.X="{Binding Offset}"`, plus a `Follow.Timing`. These feed the same follower. The attached
      property is the input itself, so it needs no companion.
    - **Layering (§1.7 Q6):** the followed value is a *base* layer in the composer. An animation of the same
      shorthand overrides it while it runs, holds its end value until it is reverted, and then hands back to the
      latest followed value. This is the same rule as a real property, where `Animation` priority sits over a
      `LocalValue` binding.

### 1.3 API

```csharp
// XAML: {presto:Animated {Binding Level}, Spring=Gentle}
public sealed class AnimatedExtension : MarkupExtension
{
    public AnimatedExtension(BindingBase binding);
    public double? Duration { get; set; }
    public EaseValue? Ease { get; set; }
    public string? Spring { get; set; }          // a Springs preset name, or "bounce,duration"
    public double? Bounce { get; set; }
    public AnimationBuilder? Builder { get; set; }
    public bool AnimateInitial { get; set; }
    public AnimationBuilder? WhenReduced { get; set; }
}

public sealed record AnimatedBindingParams { /* the same options, init-only */ }

public static class AnimatedBindingExtensions
{
    public static IDisposable BindAnimated(this AvaloniaObject target, AvaloniaProperty property,
        BindingBase source, AnimatedBindingParams? options = null);
    public static IDisposable BindAnimated<T>(this AvaloniaObject target, AvaloniaProperty<T> property,
        IObservable<T> source, AnimatedBindingParams? options = null);
}

public static class Follow                         // shorthands, decision 13
{
    public static readonly AttachedProperty<object?> XProperty; // also Y, Scale, ScaleX, ScaleY, Rotate, Blur
    public static readonly AttachedProperty<AnimationBuilder?> TimingProperty;
}

public static class Springs                        // bridge root namespace; a fresh Spring per read, since a Spring is stateful
{
    public static Spring Gentle { get; }           // Bounce 0,    Duration 500: critically damped, the default
    public static Spring Snappy { get; }           // Bounce 0.15, Duration 300
    public static Spring Bouncy { get; }           // Bounce 0.35, Duration 500
    public static Spring Stiff { get; }            // Bounce 0,    Duration 250
}
```

Core: `PreserveVelocity` applies to the channel form of `Animatable.Set`.

### 1.4 Implementation steps

1. **Spike (done, headless; written up in §1.7):**
   - Does `IProvideValueTarget` give the target object and property for attributes on controls and inside templates?
   - Which priority does a returned `Binding` over a follower land at? Is a direct `Bind(IObservable)` the better
     output?
   - Can a `DataContext` change be detected before or with the value it causes?
   - Per-frame cost of an INPC follower against a direct observable, measured with an allocation budget.
   - How the shorthand layers (decision 13) work.
2. Core: per-channel `PreserveVelocity`, with tests first.
3. Bridge: follower, companion registry, `BindAnimated`, then `AnimatedExtension`, then `Follow`. (Done.)
4. Sample and docs. (Done: the *Value-driven* Reference page and DESIGN-NOTES §5.)

### 1.5 Tests

- **Core (`AnimatablesTests`):**
  - A channel retarget keeps each channel's velocity.
  - Channels that do not move stay at rest.
- **Bridge (`AnimatedBindingTests`, headless):**
  - The first value snaps, and `AnimateInitial` animates it.
  - A change animates, and the end lands exactly.
  - A retarget mid-flight keeps velocity: the step on either side of the retarget matches.
  - A `DataContext` swap snaps, including recycling a virtualized container.
  - Non-interpolable values pass through.
  - `TwoWay` throws.
  - A `Setter` throws.
  - Reduced motion snaps.
  - Detach lands on the latest target, and reattach snaps.
  - A style value at a lower priority is overridden.
  - A `control.Animate` on the same property layers over the follow and reverts back to it.
  - `ElementName`, `RelativeSource` and converters resolve.
- **Bridge (`AllocationTests`):** a followed `double` stays within 192 B per tick through `{presto:Animated}`
  (160 measured) and within 128 B through `BindAnimated` (104 measured). The follower's core holder adds nothing.
- **Bridge (`AnimatedBindingPlumbingTests`, kept from the spike):** pins the Avalonia behaviour listed in §1.7.

### 1.6 Sample

A *Value-driven* Reference page:

- A slider and buttons on a view model drive a bar (`Width`), a gauge (`Follow.Rotate`), a colour swatch (`Color`) and
  a card's `Margin`.
- Mashing the buttons shows the momentum.
- A virtualized list of rows with bound progress bars shows recycling snap rather than sweep.
- A reduced-motion toggle.

Existing sample pages that animate on a view-model change through `OnChanged` and code-behind move over where it reads
better.

### 1.7 Spike results

Headless probes on Avalonia 12.1.2 with compiled XAML and compiled bindings (`x:DataType`). Probes that pin behaviour
the later items rely on are kept in `AnimatedBindingPlumbingTests`. They run over `AnimatedBindingXamlView` and a
test-local `ProbeExtension` that does the plumbing without the animation. Measurements and throwaway variants were
deleted after their numbers were recorded here.

**Q1: what `IProvideValueTarget` gives.**
- **Control attribute:** `TargetObject` is the control and `TargetProperty` is the `AvaloniaProperty` itself, such
  as `Layoutable.WidthProperty`. It is not the `IPropertyInfo` that `RecipeExtension` sees on plain CLR objects.
- **`ControlTemplate`:** the same, once per instance. `TemplatedParent` is already set when `ProvideValue` runs.
- **`DataTemplate`/`ItemTemplate`:** the same, once per built row. `DataContext` is not set yet, so the first value
  comes after a `DataContextChanged`.
- **Style `Setter`:** `TargetObject` is the `Setter` and `TargetProperty` is a `ClrPropertyInfo` named `Value`. It
  is provided once per style, and its result is applied at `StyleTrigger` priority.
- **Design:** decision 12 detects the setter and also throws on any non-Avalonia target.
- **Evidence:**
  - `TheTargetIsTheControlAndItsPropertyOnAnAttributeAndInsideTemplates`
  - `InAStyleSetterTheTargetIsTheSharedSetterAndItsValueClrProperty`

**Q2: the output and its priority.**
- **`Binding` over an INPC follower:** lands at `LocalValue` and overrides a style's `Width`.
- **`follower.ToBinding()`:** the same `LocalValue`, including inside a `ControlTemplate`, where a bare `{Binding}`
  also lands at `LocalValue` and only a literal is held at `Template`. It needs no reflection or expression trees,
  and its subscription gives the lifetime signal directly (Q3).
- **A direct `target.Bind(property, observable)` in `ProvideValue`:** works on an attribute when the extension
  returns `UnsetValue`. But compiled XAML casts any non-binding result to the property type inside a template, so
  `UnsetValue` and `BindingOperations.DoNothing` both throw `InvalidCastException` there. It is ruled out for the
  extension.
- **Layering:** `control.Animate` on the same property runs at `Animation` priority. View-model changes keep
  updating the base value underneath, the animation holds its end value until it is reverted, and the revert
  uncovers the latest followed value with the binding still live.
- **Design:** decision 3 now returns `ToBinding()`.
- **Evidence:**
  - `AReturnedObservableBindingLandsAtLocalValueOverAStyleAndInsideATemplateLikeABareBinding`
  - `AnAnimationLayersOverTheReturnedBindingAndRevertsToItsLatestValue`

**Q3: the companion property.**
- **Registration:** registering lazily per property with `RegisterAttached<Owner, AvaloniaObject, object?>` works.
  It needs `AVP1001` suppressed.
- **Resolution:** all of these resolve on the companion exactly as on the property: a compiled `{Binding}`,
  `{ReflectionBinding}`, a converter, `FallbackValue`, `#Name` (`ElementName`), `RelativeSource AncestorType`,
  `RelativeSource TemplatedParent` and `{TemplateBinding}`.
- **Untyped values:** the companion is `object`-typed, so values are not converted. The XAML `FallbackValue=12`
  arrives as the string `"12"`. An INPC `Binding` output hid this, because it converts. Decision 2 now converts.
- **Mode:** see decision 9.
- **Lifetime:** a `ClearValue` or a local write leaves a `LocalValue` binding live, and the next push restores it.
  Disposing the expression, or binding the property again, unsubscribes the follower. Decision 7 is rewritten to
  match.
- **Evidence:**
  - `TheWrappedBindingResolvesOnTheCompanionAsItWouldOnTheProperty`
  - `OnlyRemovingTheBindingExpressionUnsubscribesTheFollower`

**Q4: detecting a `DataContext` change.**
- **Order:** on the target, `DataContextChanged`, then `PropertyChanged(DataContext)`, then the companion change
  it causes. All three run synchronously in the same call. This holds for a direct swap, an inherited swap and a
  `ContentControl` whose template root is reused for new content.
- **Virtualized `ListBox`:** rows are not reused. Each newly realized item gets a freshly built template root (a
  first value, which snaps). A row scrolled away is handed `null` as its `DataContext` clears.
- **Rule:** decision 6. `DataContextChanged` sets a flag, the next companion change snaps and clears it, and a
  `Background` dispatcher post clears a flag that no value consumed.
- **Evidence:**
  - `ADataContextChangeIsRaisedBeforeTheCompanionValueItCauses`
  - `AReusedTemplateRootSeesItsNewItemBeforeItsValue`
  - `AVirtualizedListBuildsANewRowPerItemAndNullsTheRowsItRecycles`

**Q5: cost per frame.** Bytes per push of a new `double` into `Border.Width`, using the `AllocationTests` method:

| Output | B/push |
|---|---|
| Plain `Width = x` (Avalonia's change args) | 80 |
| `AnimatedValue<double>` at `Animation` priority (reference) | 80 |
| Direct `Bind(property, IObservable<object?>, LocalValue)` | 104 |
| `ToBinding()`, untyped or typed `IObservable<double>` | 160 |
| INPC follower + `Binding` or `CompiledBinding` | 192 |

- **Input side:** a view-model change reaching the companion costs 216 B. That is once per change, not per frame.
- **Budget:** set in §1.5, with the core holder writing without allocating.

**Q6: `Follow.*` layering.**
- **Today:** a follower writing `X = 40` through `RenderTransformComposer.Set` is overwritten by an `X` animation,
  which starts from 40. Reverting that animation sets `X` back to its identity, 0, and removes the composer. The
  followed value is lost until its next change.
- **Rule:** decision 13. The followed value is a base layer. An animation overrides it while it runs and holds its
  end value until it is reverted, then the latest followed value shows again.
- **`RenderTransformComposer`:** needs `Follow(visual, shorthand, value)` and `Unfollow(visual, shorthand)`.
  - They keep a followed mask and a base per shorthand.
  - `Follow` writes through only while no animation holds that shorthand.
  - `Revert` falls back to the base, not the identity, and keeps the group while anything is followed.
  - `Get` returns the displayed value, so an animation starts from the followed value.
- **`EffectComposer`:** needs the same pair for `Blur`, and `Revert` falls back to the followed radius.
- The core is unaffected.

**Names.** `{presto:Animated}`, `BindAnimated` and `Follow` stay. XAML resolves `{presto:Animated}` to
`AnimatedExtension`. Nothing public is called `Animated` or `Follow`, and `AnimatedValue` is internal under
`Targets`.

**Default timing.** A critically damped spring: `Springs.Gentle`, with `Bounce = 0`. For a spring built from
`Bounce`/`Duration`, bounce 0 gives a damping ratio of exactly 1. It is a spring rather than a `MotionTokens` entry
so that retargets keep their velocity (decision 5). `Token` stays available as an option.

**Spring presets.** No named springs exist today; there is only `Eases.Spring(...)`. Add a static `Springs` class
in the bridge root namespace, with the values listed in §1.3.

- `Duration` is the spring's perceived duration.
- Each property returns a new `Spring`, because a `Spring` carries completion state.
- XAML names a preset as `Spring=Gentle` (the name is resolved), or as `Spring="0.2,400"` for bounce and duration.
- The presets live in the bridge because they serve XAML. The core keeps `Eases.Spring` as the one way to build a
  spring.

## 2. Phase 42 — Derived values

### 2.1 Gap

There is no way to say "this value follows that one through a mapping". Pages hand-write `OnUpdate`/`OnScroll`
handlers that read one value and set several, for example scroll → header height, opacity and blur, or drag X →
rotation and the colour of an underlying action.

Motion has `useMotionValue`, `useTransform`, `useSpring` and `useVelocity`. GSAP gets most of the way with `quickTo`
plus `utils.mapRange`/`pipe`. Presto has the pieces: `Animatable` (≈ `quickTo`), `Utils.MapRange`/`Snap`/`Clamp`,
`VelocityTracker`, `ScrollObserver.Progress`/`Velocity` and `Draggable.X`/`Y`. It has no graph that joins them.

### 2.2 Design sketch

- **Core `MotionValue`.** A `double` cell with `Value`, `Velocity` (engine time) and a `Changed` event, plus combinators:
  - `Map(input[], output[], ease, clamp)`
  - `Map(Func<double,double>)`
  - `Combine(a, b, f)`
  - `Spring(spring)`, which follows its source through a velocity-preserving spring
  - `VelocityOf()`
  - `Snap`
- **No glitches, at most one write per frame.**
  - Setting a source marks everything downstream dirty.
  - Derived values evaluate lazily, versioned so a diamond computes once.
  - Sinks flush once per engine frame from a post-update step. The engine gains an internal after-update list.
  - Setting a value outside a frame wakes the engine.
  - Allocation-free after construction.
- **Outputs beyond `double`.** `Map(input[], object[] outputs)` interpolates outputs through their codec's channels.
  That covers colours, thicknesses and gradients through the existing codecs, and colour spaces once Phase 30 lands.
- **Bridge sources:**
  - `PrestoAvalonia.ScrollValue(ScrollViewer, axis, progress: bool)`
  - `ScrollObserver.ProgressValue`
  - `Draggable.XValue`/`YValue`
  - `PointerValue(Control, axis)`
  - `ValueOf(AvaloniaObject, AvaloniaProperty)`
  - `Timer.ProgressValue`
  - `Animatable.ValueOf(property)`
- **Bridge sinks:**
  - `BindAnimated`'s plumbing from Phase 41, used *without* smoothing, so a `MotionValue` binds to any property at
    `LocalValue` priority.
  - `Follow.*` shorthands accept a `MotionValue`.
  - `timer.Scrub(motionValue)` seeks a timeline by a value. That generalises the scroll sync to any source.
- **XAML:** none in this phase. A view model or code-behind exposes a `MotionValue`, and Phase 41's extension binds it
  (`{presto:Animated {Binding Header.Opacity}, Duration=0}`). A `{presto:Map}` extension is an open question.

### 2.3 Tests and sample

- **Core `MotionValueTests`:**
  - Mapping and clamping.
  - A diamond evaluates once per frame.
  - A sink writes once per frame however many sources changed.
  - `Spring` keeps velocity across source jumps.
  - Velocity matches `VelocityTracker`.
  - No allocation per frame.
- **Bridge tests:** each source and sink.
- **Sample:** migrate the Scroll story header and the Swipe deck's drag → rotate/tint onto `MotionValue`s and show the
  code. A small *Derived values* Reference page drives five properties from one pointer value.

## 3. Phase 43 — Layout without distortion

### 3.1 Gap

`PositionAndSize` FLIP moves scale the element's visual, so its children, corner radius and shadow stretch mid-flight.
Motion counter-scales children and corrects radius and shadow. Compose (`animateContentSize`, `AnimatedContent`'s
`SizeTransform`), Flutter (`AnimatedSize`) and SwiftUI animate the *layout size* instead. Presto lists both as out of
scope today (COMPOSITOR-PLAN §4.4, DECLARATIVE-LAYOUT-PLAN §7).

### 3.2 Design sketch, in two parts

- **43a — `Animate.ContentSize` (UI thread, real layout).**
  - An attached property on a `Decorator`/`ContentControl` whose arranged size eases from the old desired size to the
    new one, clipping content meanwhile.
  - The child is laid out at its final size from the first frame, so text never reflows mid-animation.
  - It re-lays out per frame, so it is meant for a panel, not hundreds of rows.
  - Composes with `Presence` for a content swap (≈ `AnimatedContent`): the outgoing content ghosts, the incoming
    content enters, and the box resizes in between.
  - Corner radius and shadow stay correct, because nothing is scaled.
- **43b — scale correction for compositor FLIP.**
  - While an element's visual is scaled, its direct child visuals get an `ExpressionAnimation` of
    `1 / parent.Scale` with an offset fix-up, so content keeps its size.
  - The scope is opt-in (`LayoutAnimateMode.PositionAndSizeCorrected`), direct children only. Deeper correction is an
    open question.
  - Corner radius is not a composition property. Corrected mode documents that radius scales, and points to 43a for
    rounded surfaces.

### 3.3 Tests and sample

- **Tests:** headless pixel tests. A child's glyph box keeps its size across a corrected FLIP. `ContentSize` reaches the
  new size exactly, clips meanwhile, and never reflows the child.
- **Sample:** an expanding FAQ card and a content-swapping panel (43a), and a growing card with an avatar (43b) on the
  Layout page.

## 4. Phase 44 — Lottie playback

### 4.1 Decision: play through Skottie, do not interpret Lottie

| Option | Scope | Verdict |
|---|---|---|
| Skottie (Skia's Lottie player) behind a Presto-driven control | ≈ Phase 37 | **Do this** |
| dotLottie (`.lottie` zip: several animations, themes) | small on top | Fold in if the spike is easy |
| dotLottie state machines | medium | Later, if asked for |
| Native interpreter (layers → Presto tweens and Avalonia shapes) | as big as writing a renderer | No |

`SkiaSharp.Skottie` 3.119.x matches the SkiaSharp 3.119.4 that Avalonia 12.1.2 resolves here, and targets net8,
Android, iOS and macOS. `Avalonia.Skia.Lottie` exists, but only for Avalonia 11 (11.0.0), so it is not a drop-in.

### 4.2 Design sketch

- **Packaging:** a separate package, `Monaco.Presto.AvaloniaBridge.Lottie`, so the bridge does not take a Skottie
  dependency.
- **`LottieView : PrestoImageBase`:**
  - A `double` `Frame`, shown as given (Lottie is vector, so frames are not floored), plus `Progress` and `Source`
    (a URI, stream or JSON).
  - Every Presto feature works on it: tweens, timelines, seeking, reversing, scroll sync, and a Phase 42 `MotionValue`.
  - The base's filters (`Tint`, `Duotone` and the rest) come for free.
- **Drawing:** a custom draw operation through the Skia API lease, as `ImageEffectDrawOperation` does. It carries the
  frame and calls `Seek` and `Render` on the render thread, which owns the Skottie `Animation`.
- **Segments:** markers parsed from the JSON (`markers: tm/cm/dr`) become named segments.
  `Play()`/`Play("hover")`/`PlaySegment(from, to)` return the `Animation`. `Recipes.PlayLottie` and
  `{presto:PlayLottie}` follow the `PlayFrames` shape.
- **Optional render-thread playback**, as `CompositorScene` does, for spinners that must survive UI-thread stalls.
- **Out of scope:** per-layer recolouring (Skottie's property observers are not in SkiaSharp's public API),
  expressions (Skottie does not run them), and Lottie's text and effects beyond what Skottie draws.

### 4.3 Spike first

- The Browser head: is Skottie compiled into SkiaSharp's WASM native build?
- Parse and render cost of a typical 200 KB file.
- Seeking backwards at frame rate.

## 5. Phase 45 — Scroll storytelling

### 5.1 Gap

GSAP ScrollTrigger and ScrollSmoother support pinning, snapping, parallax and smoothed wheel scrolling. `ScrollObserver`
is a faithful port of anime.js `onScroll`: thresholds, enter/leave callbacks, linked timers with smoothing, and
`Velocity`. It has none of the four.

### 5.2 Design sketch (new `ScrollObserverParams` options and attached properties)

- **Pin.**
  - `Pin = true` holds the target in place between the enter and leave thresholds: a translate equal to the scroll past
    `OffsetStart`, set in the same scroll notification so it never lags.
  - `PinSpacing` (default on) grows a spacer by the pinned distance, so content below arrives after the pin ends.
- **Snap.**
  - `Snap` takes progress increments, the linked timeline's labels, or a `Func<double,double>`.
  - When scrolling settles (velocity under a threshold, no pointer down), the `ScrollViewer.Offset` eases to the snap
    point. A user scroll interrupts it.
- **Parallax.** `Scroll.Speed` attached property (≈ ScrollSmoother `data-speed`): the element translates by
  `(1 − speed) × scroll` relative to where it enters the viewport.
- **Smoothed wheel scrolling (open question).** An opt-in `Scroll.Smooth` that eases the offset towards the wheel's
  target. This must not fight touch and trackpad inertia.
- **Velocity as a `MotionValue`** once Phase 42 lands, for skew-on-scroll.

### 5.3 Tests and sample

- **Tests:** a pinned element stays put to within a pixel across thresholds; the spacer height; snapping to labels; a
  user scroll interrupting a snap; parallax offsets.
- **Sample:** the Scroll story page gains a pinned chapter, label snapping and a parallax hero.

## 6. Phase 46 — Drag-to-reorder lists

### 6.1 Gap

Motion's `Reorder`, Flutter's `ReorderableListView` and Compose's reorderable lists do this out of the box. In Presto
the pieces exist, `Draggable` and `Animate.ItemLayout`, but no recipe joins them.

### 6.2 Design sketch

- **Attaching:** `Reorder.Enabled` and `Reorder.Axis` on an `ItemsControl` whose `ItemsSource` is a mutable `IList`.
  An optional `Reorder.Handle` marks a drag handle inside the item template.
- **Grab:** lift the container (scale plus `Shadow*`, overridable `Reorder.Lift` builder), which then follows the
  pointer.
- **Swap:** crossing a neighbour's midpoint calls `Move(old, new)` on an `ObservableCollection`. Otherwise it raises
  `Reorder.Moving` (cancellable) or runs `Reorder.Command`, so the view model owns the change.
- **Neighbours:** they slide through `Animate.ItemLayout`'s FLIP. The dragged container is excluded from it and has its
  translation rebased after each move, so it stays under the pointer.
- **Edge auto-scroll:** reuses `Draggable`'s `ScrollSpeed`/`ScrollThreshold`.
- **Release:** a spring into the slot. Escape cancels back to the start.
- **Keyboard:** Alt+Up/Down moves the focused item, for accessibility.
- **Risk:** the dragged container must stay realized in a virtualizing panel while it is dragged off screen. Pin this
  first with a headless test.

### 6.3 Tests and sample

- **Tests:** `Move` calls and their order; a cancel restores the start; the dragged item never jumps on a move;
  auto-scroll; keyboard moves; a virtualized drag far past the viewport.
- **Sample:** a reorderable list on the Board page, plus `{presto:Reorder}` defaults shown on a Builder page.

## 7. Phase 47 — Orchestration

### 7.1 Gap

- **Variants:** a Motion parent state propagates to children, with `staggerChildren`, `delayChildren` and
  `when: "beforeChildren"`. Compose's `updateTransition` does the same with child transitions. `Motion.States` is
  per element.
- **Exit-then-enter:** `AnimatePresence mode="wait"` plays the exit before the enter. A Presto ghost leaves layout at
  once, which is Motion's `popLayout`, and the enter plays at the same time as the exit.

### 7.2 Design sketch

- **Propagation:** `Motion.State` set on a parent propagates to descendants that declare `Motion.States`, unless one
  sets its own. On the parent:
  - `Motion.StaggerChildren` (stagger amount, `from`, order by visual position as Phase 21's initial-load stagger does)
  - `Motion.DelayChildren`
  - `Motion.Order` = `Together` / `BeforeChildren` / `AfterChildren`
- **`Presence.Mode`** on a content host or `ItemsControl`, with values `Sync` (today), `Wait` and `PopLayout`.
  - `Wait` holds the incoming content at opacity 0, and its `Enter`, until the ghost's exit completes. Phase 38's
    hold-until-measured is the precedent.

### 7.3 Tests and sample

- **Tests:** order and timing of child plays; a child override wins; an interrupted parent state retargets children;
  `Wait` enters after the exit completes; interrupting `Wait` with a third content.
- **Sample:** a menu whose items stagger in from a parent `Open` state, and a wizard using `Presence.Mode=Wait`.

## 8. Phase 48 — Presto DevTools overlay

### 8.1 Gap

- **GSAP:** GSDevTools scrubs any timeline.
- **Compose:** Android Studio's animation preview inspects transitions.
- **Flutter:** `timeDilation` slows everything down.
- **Presto:** has `Engine.Speed` (global slow motion works today) and Avalonia DevTools for properties, but nothing that
  shows *animations*.

### 8.2 Design sketch

- **Enabling it:** `AppBuilder.WithPrestoDevTools(gesture)` (DEBUG only in the sample) opens an overlay window, or a
  docked adorner. It has:
  - A live list of the engine's root tickables, with id, type, targets, state and progress, refreshed each frame from
    the child list without allocating.
  - Per-item play, pause, reverse and a scrubber. Seeking a timeline pauses it.
  - A global speed control (`Engine.Speed`, 0.1×–2×), pause-all, and step-one-frame (pause the engine, then
    `Update()` with a fixed delta).
  - Highlighting a selected item's targets on screen.
  - Render-thread scenes listed separately, read-only.
- **Open question:** whether anything can plug into the `avdt` tool instead. It is not known to be extensible.

### 8.3 Tests and sample

- **Tests:** the listing reflects add, complete and cancel; scrubbing seeks; stepping advances exactly one frame;
  closing the overlay leaves the engine as it was.
- **Sample:** enabled in the gallery under DEBUG.

## 9. Phase 49 — Small items

Each item is independent and can land on its own.

| Item | Who has it | Sketch |
|---|---|---|
| Custom ease from path data | GSAP CustomEase | `Eases.FromPath("M0,0 C…")`: a small SVG-path parser in core (cubic, line and move only), sampled into a monotone lookup table, and parseable from strings |
| Wiggle, rough, slow-mo, expo-scale eases | GSAP EasePack, CustomWiggle | `Eases.Wiggle(count, type)`, `Eases.Rough(seeded)`, `Eases.SlowMo(ratio, power)`, `Eases.ExpoScale(from, to)`, all string-parseable |
| Motion path through points | GSAP MotionPath `curviness`, Compose `keyframesWithSpline` | `CreateMotionPath(points, curviness)` builds a Catmull-Rom path, with an `AutoRotate` offset |
| Follow the OS reduced-motion setting | Compose, Flutter, Motion `reducedMotion="user"` | Opt in with `PrestoAvalonia.FollowSystemReducedMotion()`. Use Avalonia 12's platform settings if they expose it, otherwise per-platform probes: Windows client-area animation, macOS/iOS reduce motion, Android animator scale, Browser `prefers-reduced-motion` |
| Rolling-digit counter | SwiftUI `numericText` | `Recipes.RollDigits`: digits split into pieces, each changed digit rolling vertically under a clip, built on `CountUp` |
| Projectile/gravity as a to-value | GSAP Physics2D, Flutter `GravitySimulation` | A `Projectile` to-value (velocity, angle, gravity, friction) resolved like `Inertia` |

## 10. Open questions (confirm before the relevant phase)

- **41 — names (settled):** keep `{presto:Animated}` / `BindAnimated` / `Follow`. The spike found no clash (§1.7).
- **41 — default timing (settled):** a critically damped spring, `Springs.Gentle` (§1.7).
- **41 — `Follow.*` layering (settled):** the followed value is the composer's base layer. An animation overrides
  it, holds until it is reverted, then hands back (decision 13, §1.7 Q6).
- **42 — XAML:** is a `{presto:Map}` extension worth having, or do derived values stay code and view-model only?
- **42 — name:** `MotionValue` matches Motion, but the bridge's `Motion` class is about state triggers.
- **43b — depth:** direct children only, or the whole subtree (costlier, and rarely needed)?
- **44 — scope:** dotLottie in the same phase if the spike shows it is cheap?
- **45 — smoothed wheel scrolling:** in scope at all? It changes how every scroller in an app feels.
- **48 — form:** a separate window or an in-window overlay? A separate window survives page navigation and does not
  perturb layout.
