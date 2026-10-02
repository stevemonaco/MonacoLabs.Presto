# Gaps — what GSAP, Motion, Compose and Flutter do that Presto does not

Phases 41–49 in [PLAN.md](PLAN.md). Status: Phases 41–43, 45, 46 and 49 **implemented**; Phases 44, 47 and 48 **planned**. This plan comes from the post-Phase-40 review against GSAP (with
its plugins), Motion (Framer Motion), Jetpack Compose, Flutter and SwiftUI. It was checked against the committed code
(`0106efe`) and the Avalonia 12.1.2 assemblies.

Phases 41–43, 45, 46 and 49 are designed in full here. Phases 44, 47 and 48 are design sketches: each one settles its decisions in this document,
or in its own plan document if it outgrows a section, before it starts.

| Phase | Gap | Scope | Size |
|---|---|---|---|
| 41 | Value-driven animation (implemented) | bridge | medium |
| 42 | Derived values (implemented) | core + bridge | medium |
| 43 | Layout without distortion (implemented) | bridge | medium–large |
| 44 | Lottie playback | bridge (new package) | medium |
| 45 | Scroll storytelling (implemented) | bridge | medium |
| 46 | Drag-to-reorder lists (implemented) | bridge | small–medium |
| 47 | Orchestration | bridge | medium |
| 48 | Presto DevTools overlay | bridge | medium |
| 49 | Small items (implemented) | core + bridge | small each |

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
   <Border Width="{presto:Follow {Binding Level}, Spring=Gentle}" />
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
    `Follow.Bind(control, Border.WidthProperty, source, new FollowParams { ... })` returns an `IDisposable`.
    The source can be an `IObservable<T>` or a `BindingBase`.
    - Code needs no `BindingBase`, so `Follow.Bind` binds the follower directly with
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
// XAML: {presto:Follow {Binding Level}, Spring=Gentle}
public sealed class FollowExtension : MarkupExtension
{
    public FollowExtension(BindingBase binding);
    public double? Duration { get; set; }
    public EaseValue? Ease { get; set; }
    public string? Spring { get; set; }          // a Springs preset name, or "bounce,duration"
    public double? Bounce { get; set; }
    public AnimationBuilder? Builder { get; set; }
    public bool AnimateInitial { get; set; }
    public AnimationBuilder? WhenReduced { get; set; }
}

public sealed record FollowParams { /* the same options, init-only */ }

public static class Follow                         // shorthands (decision 13) and the code form
{
    public static IDisposable Bind(AvaloniaObject target, AvaloniaProperty property,
        BindingBase source, FollowParams? options = null);
    public static IDisposable Bind<T>(AvaloniaObject target, AvaloniaProperty<T> property,
        IObservable<T> source, FollowParams? options = null);
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
3. Bridge: follower, companion registry, `Follow.Bind`, then `FollowExtension`, then `Follow`. (Done.)
4. Sample and docs. (Done: the *Value-driven* Reference page and DESIGN-NOTES §5.)

### 1.5 Tests

- **Core (`AnimatablesTests`):**
  - A channel retarget keeps each channel's velocity.
  - Channels that do not move stay at rest.
- **Bridge (`FollowBindingTests`, headless):**
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
- **Bridge (`AllocationTests`):** a followed `double` stays within 192 B per tick through `{presto:Follow}`
  (160 measured) and within 128 B through `Follow.Bind` (104 measured). The follower's core holder adds nothing.
- **Bridge (`FollowBindingPlumbingTests`, kept from the spike):** pins the Avalonia behaviour listed in §1.7.

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
the later items rely on are kept in `FollowBindingPlumbingTests`. They run over `FollowBindingXamlView` and a
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

**Names.** Renamed after the phase from `{presto:Animated}`/`BindAnimated`/`AnimatedBindingParams` to one name
for the whole feature: `{presto:Follow}`, `Follow.Bind` and `FollowParams`, beside the `Follow.*` shorthands. XAML
resolves `{presto:Follow}` to `FollowExtension` before the `Follow` class, so the two share the namespace. `Follow.Bind`
is a static method rather than an extension, since an extension named `Bind` loses to `AvaloniaObject.Bind`.

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

### 2.2 Decisions

- **Name:** `MotionValue`, as in Motion. The bridge's `Motion` class is about state triggers, a different thing, and the
  two never appear in the same call.
- **XAML:** no `{presto:Map}` extension. Derived values are built in code and view models, and reach XAML through
  Phase 41's extension: `{presto:Follow {Binding HeaderOpacity}, Duration=0}`.
- `Follow.*` over a `MotionValue` ignores `Follow.Timing` (DESIGN-NOTES §6).
- **The core owns the cell, the bridge owns the sources.** `MotionValue` has no Avalonia dependency. Bridge sources
  assign it through an internal `Assign`. `Set` on a derived or Presto-assigned value throws.

### 2.3 API

Core (`Monaco.Presto`):

```csharp
MotionValue Presto.CreateMotionValue(double initial = 0)

public sealed class MotionValue
{
    double Value { get; }               // a derived value recomputes here if a source moved
    double Velocity { get; }            // per engine-time unit; measuring starts on the first read
    event Action<MotionValue>? Changed; // from the engine frame, at most once per frame
    void Set(double value);             // sources only

    MotionValue Map(double[] input, double[] output, EaseValue? ease = null, bool clamp = true);
    MotionObjectValue Map(double[] input, object[] outputs, EaseValue? ease = null, bool clamp = true);
    MotionValue Map(Func<double, double> transform);
    MotionValue Snap(double increment);
    MotionValue Snap(double[] points);
    static MotionValue Combine(MotionValue a, MotionValue b, Func<double, double, double> combine);
    static MotionValue Combine(MotionValue[] values, Func<double[], double> combine); // the array is reused
    MotionValue Spring(Spring spring);
    MotionValue VelocityOf();           // cached: one per value
}

public sealed class MotionObjectValue { object Value { get; } event Action<MotionObjectValue>? Changed; }

MotionValue Timer.ProgressValue { get; }        // assigned on every render
Timer Timer.Scrub(MotionValue? progress)        // pauses, seeks to value × Duration on each change; null ends it
MotionValue Animatable.ValueOf(string property) // assigned whenever that property's animation renders
```

- `Map` ranges need two or more ascending breakpoints of equal length (`ArgumentException` otherwise). `clamp: false`
  extrapolates the end segments.
- `Map` to objects decodes the outputs once, through the `Presto.Codecs` entry found by exact type, base type or
  interface (so brushes resolve to `IBrush`), or through `ValueParser` for strings. Neighbouring outputs are reconciled
  per segment, and mismatched kinds or channels throw. A change eases the segment progress, interpolates the channels and
  encodes.
- `Scrub` survives the engine's cancel of a completed timer, so it can seek back. `Cancel`, `Revert` and `Scrub(null)`
  end it.

Bridge (`Monaco.Presto.AvaloniaBridge`), sources:

```csharp
MotionValue PrestoAvalonia.ScrollValue(ScrollViewer scroller, ScrollAxis axis = ScrollAxis.Vertical, bool progress = false)
MotionValue PrestoAvalonia.PointerValue(Control control, Orientation axis, bool relativeToBounds = false)
MotionValue PrestoAvalonia.ValueOf(AvaloniaObject target, AvaloniaProperty property) // Convert.ToDouble
MotionValue ScrollObserver.ProgressValue { get; }
MotionValue Draggable.XValue { get; }  // and YValue, rounded to the engine's precision
```

- `ScrollValue` is the offset rounded to whole pixels, or progress through the scrollable extent (0–1). Each call makes
  its own value, fed by the scroller's shared `ScrollContainer`, which now stays alive while any observer or value uses
  it. It refreshes when the extent or viewport changes.
- `ScrollValue`, `PointerValue` and `ValueOf` stop at their last value when their control leaves the visual tree or the
  ambient scope reverts. Reattaching does not revive them. `ProgressValue` and `XValue`/`YValue` freeze when the
  observer or draggable reverts.

Sinks:

```csharp
IDisposable Follow.Bind(AvaloniaObject target, AvaloniaProperty property, MotionValue source, FollowParams? options = null)
IDisposable Follow.Bind(AvaloniaObject target, AvaloniaProperty property, MotionObjectValue source, FollowParams? options = null)
Follow.SetX(visual, motionValue)   // X, Y, Scale, ScaleX, ScaleY, Rotate, Blur
{presto:Follow {Binding SomeMotionValue}, Duration=0}
```

- `Follow.Bind` binds at `LocalValue`. With no options, a `StyledProperty<double>` is written unboxed through
  `MotionValueBinding`. Any other property goes through `FollowBinding` with a zero-duration timing, which converts and
  rounds ints. With options, it smooths through the follower.
- `{presto:Follow}` listens to a `MotionValue` or `MotionObjectValue` that arrives on its companion. `Duration=0` passes
  values through, other timings smooth, and a new value snaps.
- Every sink stops listening while its visual is detached. On reattach it resubscribes and snaps to the latest value.

### 2.4 Evaluation

- **Lazy, versioned and observer-linked** (DESIGN-NOTES §6). A value counts as observed once it has a `Changed`
  handler, an observed dependent, a velocity read or a spring. Unobserved values are never queued.
- **Flush rule.** `Engine.Update` runs the children, then `Additive.Update()`, then steps the active values, then
  flushes the sinks. Each queued sink raises `Changed` once, and only if its value differs from the last raise. The
  queue is double-buffered: a value set from a handler queues into the other list and flushes next frame.
- **Wake and sleep.** Queueing a sink or activating a value calls `Wake()`. `OnFrame` keeps updating while there are
  children, queued sinks or active values, so the frame source stops once the springs and velocities settle.
- **Spring.** A closed-form damped oscillator (under-, critically and over-damped) from the `Spring`'s mass, stiffness
  and damping. It is stepped by elapsed engine time, so it does not depend on the frame rate. A new target keeps the
  current position and velocity. Inside `Spring.RestThreshold` (on displacement, and the same figure per millisecond on
  velocity) it lands exactly on the target and leaves the active list.
- **Velocity.** A `VelocityTracker` is created on the first `Velocity` read and sampled once per frame while the value
  moves. Activating it rebases to now, so time spent asleep does not count. `VelocityOf()` is assigned from that sample.
- **Allocations** (the two `AllocationTests` suites): a `Map` → `Combine` graph flushing every frame allocates 0 bytes
  per tick, as does a settling spring with `VelocityOf()`. A `Width` bound through `Follow.Bind` costs 80 bytes per
  tick (against 104 for `Follow.Bind` over a binding), and a `Follow.X` costs 80. Both are the property-changed args
  Avalonia raises (decision 9). `MotionObjectValue` boxes one struct per changed frame unless its codec reuses the
  instance, as the brush codec does.

### 2.5 Tests and sample

- **Core:** `MotionValueTests` (mapping, clamping, snapping, combining, diamonds, one flush per frame, wake, a set from a
  handler) and `MotionValueDynamicsTests` (spring settling, a spring over a derived value, velocity kept across jumps,
  frame-rate independence, `Velocity`/`VelocityOf` matching a `VelocityTracker`, `ProgressValue`, `Scrub`,
  `Animatable.ValueOf`), plus the allocation tests above.
- **Bridge:** `MotionValueCodecTests` (object maps through colours, thicknesses, brushes and gradients), `MotionValueSourceTests`
  and `MotionValueSinkTests`.
- **Sample:** the Scroll story rail (`ProgressValue` × track width) and vane (scroll velocity, mapped and sprung, to
  `ScaleY`; direction, sprung, to `Rotate`) and the Swipe deck's drag → tilt, lean, stamp opacities and tint run on
  `MotionValue`s and print their graph. The *Derived values* Reference page drives five card properties from one
  `PointerValue`, plus a view-model value read by `{presto:Follow ..., Duration=0}`.

## 3. Phase 43 — Layout without distortion

### 3.1 Gap

`PositionAndSize` FLIP moves scale the element's visual, so its children, corner radius and shadow stretch mid-flight.
Motion counter-scales children and corrects radius and shadow. Compose (`animateContentSize`, `AnimatedContent`'s
`SizeTransform`), Flutter (`AnimatedSize`) and SwiftUI animate the *layout size* instead. Before this phase Presto
listed both as out of scope (COMPOSITOR-PLAN §4.4, DECLARATIVE-LAYOUT-PLAN §7). Upstream's `layout/` module animates
real width and height too, overriding min/max, with a 1px tolerance.

### 3.2 Decisions as built

**43a — `Animate.ContentSize` and `PrestoAvalonia.AnimateContentSize` (UI thread, real layout).**

- **API.** `Animate.ContentSize` takes a builder (only timing and an ease; a property or anything else a layout
  transition cannot honour throws where it is set) and is meant for a `Decorator`, `ContentControl` or `Border`.
  `AnimateContentSize(host, duration, ease)` / `(host, builder)` / `(host, build)` and the matching extensions return a
  `ContentSizeTransition` with `Progress`, `Completed`, `Cancel`, `Revert`, `Then` and an awaiter, registered with the
  current scope. Attaching records the size without easing, detaching reverts, and a change of `ReducedMotion` re-arms,
  so a reduced resize lands in one frame.
- **Holding the child at its final size.** The host's sole visual child gets an extra right/bottom `Margin` at
  `Animation` priority equal to the drawn size minus the natural size, eased to zero by a mirror `Animation`. The host
  and its siblings lay out at the eased size while the child keeps its natural constraint, so text never reflows. The
  sketch held the host's `Width`/`Height` instead. The spike showed that this hides content that grows mid-flight,
  whereas a margin lets each pass measure the content as it is, so a change mid-flight retargets from the size drawn.
  Min/max are not overridden: the margin works inside the host's own constraint.
- **Which axes ease.** An axis eases only when the host grew by as much as its content did, within 1px (upstream's
  tolerance). A stretched axis therefore moves in one frame.
- **First-frame guard.** The margin is written in the `LayoutUpdated` that sees the change, not at the mirror's first
  frame, so the re-layout it triggers runs before the next render and that frame still shows the old size.
- **Clipping.** While a resize is in flight the content's `Clip` is held at `Animation` priority to the host's inner box
  (its border and padding deflated, its corners rounded), and released with the margin. The host itself is not clipped,
  so its shadow still draws outside it.
- **Presence.** Swapping `Content` or `Child` resizes from the size drawn now to the new content's. The old content
  ghosts out through its `Presence.Exit`, clipped to the box the host had when it was swapped, while the new one enters.
  A third swap mid-resize retargets from the current size, and a host removed mid-resize ghosts at its drawn size.
- **Layout moves.** ContentSize owns its host's size. On the same host, `Animate.Layout` or a `LayoutTransition` eases
  only position, whatever its mode. While a host resizes, layout moves of controls beside it under the same parent
  land in one frame, because the resize already eases them and a compositor flight would trail behind. Without this,
  `ItemLayout` neighbours of an expanding row lagged behind it. The host stays in that set through the pass that lays out
  its last frame.
- **Cost.** The host is laid out again every frame, so this is meant for a panel, not hundreds of rows.

**43b — `LayoutAnimateMode.PositionAndSizeCorrected`.**

- **Counter-scale.** While `LayoutTransition` scales a control's composition visual, each direct visual child gets an
  `ExpressionAnimation` of `Vector3(1 / parent.Scale.X, 1 / parent.Scale.Y, 1)` on the render thread, so it keeps its
  size and takes its descendants with it.
- **Anchoring.** The offset fix-up is a static `CenterPoint` on each child, not an expression. The anchor sits as far
  across the child as the child sits across the room the parent leaves it. A child flush top left stays there, a centred
  one stays centred, and a margin scales with the parent.
- **Ownership.** The child's `Scale` is taken through `CompositorRegistry` with the usual Debug warning, so a corrected
  grow takes it over from a compositor animation on the child. The transition releases it at completion, cancel or
  revert, writing identity and resetting the anchor. A grow interrupted mid-flight keeps the expressions it already
  started.
- **What still scales.** Everything the control draws itself scales: background, border, shadow and corner radius,
  which is not a composition property. Deeper descendants get no expression of their own: they ride their corrected
  child, which is drawn at its final layout size from the first frame. A child that fills the control is therefore drawn
  at the final size throughout and overhangs a growing control until it lands. The enum's docs point to
  `Animate.ContentSize` for rounded surfaces.

### 3.3 Tests and sample

- **Tests:** `ContentSizeTests` (eases to the new size and lands exactly, the frame after a change still shows the old
  size, wrapped text keeps its final layout and is clipped, a mid-flight change retargets, cancel/revert restore the
  user's values, attach records and detach reverts, invalid builders throw, reduced resizes are instant, `Then`).
  `ContentSizePresenceTests` (swap ghosts and resizes for a `Border` and a `ContentControl`, a swap mid-resize retargets
  and clips its ghost, a removed host ghosts at its drawn size, a host inside a transitioning content control, a layout
  transition on a host eases only position, rows below an expanding row follow it). `CorrectedLayoutTransitionTests`
  (a plain grow shrinks the child, the child keeps its alignment for each alignment pair, identity with nothing driving
  it after landing, cancel/revert restore it, an interrupted grow keeps the correction, a position-only move leaves it
  alone, the registry handover).
- **Sample:** the Layout page has an expanding FAQ card (`Animate.ContentSize`, following the Slow switch), a
  content-swap panel cycling three templates with `{presto:Fade}` Presence, and an avatar card beside the growing panel
  under `PositionAndSizeCorrected`, with an "uncorrected avatar" switch for comparison. `ReferencePagesTests` covers all
  three.

### 3.4 Deviations and visual check

- **Win32.** 43b was watched on the desktop head in a temporary sample spike: a 24×24 avatar kept its size while its
  panel grew. 43a was watched on the desktop head's Layout page with slow on: the FAQ card keeps its rounded corners
  and its shadow mid-resize while the answer is clipped to the card's inner box.

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

### 5.2 Decisions as built

Upstream has none of this, so `scroll.js` and `scroll.test.js` were structural references only, and the ported
`ScrollObserverTests` pass unchanged.

**Pin — `ScrollObserverParams.Pin` and `PinSpacing`.**

- **Mechanism.** `Pin = true` writes `Clamp(Scroll − OffsetStart, 0, Distance)` to the target's `"Y"` shorthand, or `"X"`
  on a horizontal observer, through `RenderTransformComposer.Set`, the path `Presto.Set` uses. Before the range the
  target scrolls normally, through it it stays put, and after it it rides at the pinned distance.
- **Synchronous hook.** `ScrollContainer` updates the translate inside the `Offset` change notification, not on the next
  scroll tick, so the pin never trails the content by a frame.
- **Measuring.** `TranslatePoint` includes the render transform, so the pin is lifted to 0 while the bounds are measured
  (with the linked object seeked to its start, as before) and re-applied after. A refresh after a resize re-pins at the
  new thresholds, and switching axis reverts the old shorthand.
- **Ownership.** The pin owns the target's `"X"`/`"Y"`, so a linked animation should drive the target's descendants.
  The docs on `Pin` say so.
- **Spacer.** `PinSpacing` (default true) adds `Distance` to the target's trailing margin (bottom, or right when
  horizontal) at `Animation` priority. It is recomputed from the margin the target had when pinning began on every
  measure, so a refresh does not accumulate it. The trailing margin lies outside `Bounds`, so it never moves the
  thresholds it is sized from. GSAP wraps the target in a spacer element; a margin needs no tree change.
- **Lifetime.** `Revert`, including through the linked object, releases the translate and the margin, so a pinned
  observer usually keeps `Repeat` on.

**Snap — `ScrollObserverParams.Snap` and `ScrollSnap`.**

- **Forms.** A `double` increment of progress (`.25`), a `Func<double, double>` or a `ChainedUtil`
  (`Utils.Snap([0, .3, 1])`), or `ScrollSnap.Labels`: the linked `Timeline`'s labels as fractions of its duration, plus 0
  and 1. Labels map linearly onto progress, so the timeline lands on one exactly only under a linear `Sync`. Settings use
  `with`: `Duration` (400 ms), `Ease` (`OutQuad`) and `Delay` (150 ms); the defaults follow seconds mode.
- **Settle rule.** Each offset change restarts a paused `Delay` timer. When it completes, the snap starts only if no
  pointer is down on the scroller (scrollbars included), `Velocity` is under a small private threshold (otherwise it
  waits again), and the offset lies within `[OffsetStart, OffsetEnd]`. The target is clamped to `ScrollBarMaximum`.
- **Writing.** A second paused timer eases `ScrollViewer.Offset` with `SetCurrentValue`, so it stays at the offset's
  local priority, below `Animation`, and the scroller's own input still moves it. Its last frame lands exactly.
- **Interruption.** Any offset change the snap did not write itself (wheel, drag, keyboard, a smooth-wheel frame)
  cancels the ease and restarts the wait, so a snap starts once a smooth scroll settles. A pointer release restarts the wait, and `Revert` cancels both timers.

**Parallax — `Scroll.Speed`.**

- **Formula.** An element inside a `ScrollViewer` with `presto:Scroll.Speed="0.6"` is translated along `"Y"` by
  `(1 − speed) × max(0, scroll − entry)`, or along `"X"` when the scroller scrolls only horizontally. `entry` is the
  scroll at which its leading edge meets the far edge of the viewport, clamped at 0 so an element already in view at load
  starts at its layout position. Below 1 the element lags, above 1 it leads.
- **Hook and measuring.** It is written in the same `Offset` notification as the pin. `entry` is measured with the
  translate lifted, when the element attaches, when its `Bounds` change and when the scroller's viewport or extent
  changes (at once, not after the observers' 250 ms debounce, since the element is visible meanwhile).
- **Lifetime.** Each element registers with `ScrollContainer` as a third kind of listener beside observers and
  `ScrollValue`, which keeps the container alive. Detaching it or setting the speed back to 1 reverts its shorthand and
  releases the container. It does nothing at design time.

**Smoothed wheel — `Scroll.Smooth` (shipped, Win32 only).**

- **Behaviour.** On a `ScrollViewer`, `presto:Scroll.Smooth="True"` catches wheel notches in the tunnel phase and adds
  each to a target 50 units away (Shift turns a vertical wheel horizontal), clamped to `ScrollBarMaximum`. A paused timer
  (300 ms, `OutQuad`) eases `Offset` there with `SetCurrentValue`. A notch at the end of the range passes through to the
  presenter, so scroll chaining to an outer scroller still works. Fractional deltas, touch, scrollbar, keyboard and snap
  input pass through, and any offset change the ease did not write cancels it.
- **Why Win32 only.** The spike (Avalonia 12.1.2, decompiled) found:
  - `PointerWheelEventArgs` and `RawMouseWheelEventArgs` carry only `Delta`: no precise, inertial or device flag.
  - Win32 sends `WM_MOUSEWHEEL` and `WM_POINTERWHEEL` as the wParam high word ÷ 120, so a notch arrives as a whole
    number. Precision touchpads and free-spinning wheels send fractions of 120, inertia included. Touch goes to
    `ScrollGestureRecognizer`, never to the wheel path, so nothing fights touch inertia.
  - Browser divides DOM deltas by 50 and ignores `deltaMode` (a Chrome notch is 2.0 times the DPR, a Firefox line notch
    0.06), and macOS passes accelerated, already-smoothed NSEvent deltas through. Neither has a reliable signal, so the
    property does nothing when `!OperatingSystem.IsWindows()`. X11 was not verified.
- **Caveat.** A touchpad delta that happens to be an exact multiple of 120 is smoothed, and the next fractional delta
  cancels that ease, costing at most part of one step.

**Velocity as a `MotionValue`.** Already met by Phase 42: `PrestoAvalonia.ScrollValue(scroller).VelocityOf()`, which the
Scroll story page's vane uses. Nothing new was built.

### 5.3 Tests and sample

- **Tests:** `ScrollPinTests` (the target stays put between its thresholds without a frame of lag, scrolls normally
  before and rides at the pinned distance after, `PinSpacing` pushes the following content back by the distance and is
  absent when off, the horizontal axis pins along X, a refresh after a resize keeps the pin without accumulating the
  spacer, revert restores the user's transform and margin, reverting the linked animation releases the pin, a linked
  animation of a child still scrubs). `ScrollSnapTests` (an increment snap eases to the nearest step, lands exactly and
  lets the engine idle; it waits while a pointer is pressed; a function snap; label snapping from either side; a user
  scroll mid-snap stops it and a new snap starts from there; nothing snaps outside the thresholds; revert mid-snap).
  `ScrollParallaxTests` (half speed lags by half the scroll since entry without a frame of lag, nothing before entry, an
  element in view at the top, speed 1 leaves the transform alone, a faster element leads, viewport and layout changes
  re-measure the entry, detach reverts and releases the container, a speed change takes effect at the next scroll,
  back to 1 reverts, a horizontal scroller, XAML via `ScrollParallaxXamlView`). `ScrollSmoothTests` (a notch eases to
  its step and lands, quick notches accumulate, the target clamps, a precise delta scrolls at once, a direct offset set
  cancels, a snap waits for the smooth scroll to settle, turning it off restores native notches).
- **Sample:** the Scroll story page pins a real stage inside the article instead of the overlay it used to fake one with.
  The `pin` observer has `Enter = "center center"`, `Leave = "center center+=960"`, `Pin = true` with spacing, a linear
  `Sync` and `Snap = ScrollSnap.Labels`. Its timeline has labels `step1` to `step4` and animates only the stage's
  children, and the stage shows the nearest label. The 1600px track and the fade tweens are gone. The hero's layers use
  `Scroll.Speed` 0.6, 0.8 and 1.3, a small `hero` observer keeps the title fade, and a switch turns on `Scroll.Smooth`.
  `ScrollStoryPageTests` adds `ThePinnedStageStaysPutWhileItsRangeScrollsAndWhatFollowsArrivesAfterIt`,
  `ScrollingThatStopsBetweenTwoLabelsSettlesOnTheNearerOne` and `TheHeroLayersAreTranslatedByTheirSpeeds`.

### 5.4 Deviations and visual check

- **Deviations from the sketch.** `Scroll.Speed` clamps its entry scroll at 0, found on the sample's hero, which was
  otherwise pushed off its layout position before any scroll.
- **Win32.** Watched on the desktop head's Scroll story page through DevTools: the stage carried its 960px spacer
  margin, sat at the same place in the viewport at two offsets inside its range, and the label snap settled offsets of
  1450 and 2003 onto `step1` (1303) and `step3` (1943). At an offset of 200 the hero's title had moved up past its
  ridge.

## 6. Phase 46 — Drag-to-reorder lists

### 6.1 Gap

Motion's `Reorder`, Flutter's `ReorderableListView` and Compose's reorderable lists do this out of the box. In Presto
the pieces exist, `Draggable` and `Animate.ItemLayout`, but no recipe joins them.

### 6.2 Decisions as built

**Spike results (Avalonia 12.1.2, pinned in `ReorderContainerTests`).** Each theory runs a non-virtualizing
`ItemsControl` and a `ListBox` over a `VirtualizingStackPanel`.

- **`Move` re-hosts the moved item.** Both lists raise `ContainerClearing` and then `ContainerPrepared` for it. The
  `ItemsControl` prepares a **new** container and detaches the old one, while the `ListBox` re-prepares the same
  instance. The other containers keep their identity and only change index.
- **FLIP.** With `Animate.ItemLayout` the neighbours ease. The moved container is armed as attaching, as a recycled
  one is, so it lands in its new slot in one frame.
- **Presence.** `Move` used to play the moved row's exit on a ghost and its enter on the new row. `ItemChanges` now
  tracks a `Move`'s items in `Moved` (counted in `Pending`) instead of `Added`/`Removed`. `Presence` classifies the
  arrival as `PresenceMoment.Move` and the departure as a recycle, so Enter, Exit and Reveal all play nothing.
- **Capture and focus.** `Move` takes the pointer capture from the moved container in both lists, and focus from a
  `ListBoxItem`. Capture held by the `ItemsControl` itself survives. A `ListBoxItem` handles the press, so a
  `Draggable` on it never grabs.
- **Realization.** A container that holds focus or the pointer capture stays realized while its list scrolls it far
  past the viewport. One that holds neither is recycled.
- **Composition.** A `RenderTransform` translation, which `Draggable`'s `X`/`Y` shorthands write through
  `RenderTransformComposer`, adds to the FLIP's compositor `Translation` on the same container instead of replacing it.

**API.** `Reorder` is a static class of attached properties in `Monaco.Presto.AvaloniaBridge`.

- `Reorder.Enabled` (`bool`) and `Reorder.Axis` (`Avalonia.Layout.Orientation`, default `Vertical`) go on the
  `ItemsControl`. No new enum is added.
- `Reorder.Handle` (`bool`) goes on any element in the item template. A container with a handle grabs only from its
  handles.
- `Reorder.Lift` (`AnimationBuilder?`, on the list) defaults to `Recipes.ReorderLift(scale, intensity, duration, delay, ease)`:
  scale 1.03, `ShadowBlur` 16, `ShadowY` 6, `ShadowOpacity` 0.3, and no translation, since the drag owns `X`/`Y`.
  `{presto:Reorder}` is `ReorderExtension : RecipeExtension` over that recipe, the way `{presto:Lift}` is over
  `Recipes.Lift`.
- `Reorder.Moving` is a bubbling `RoutedEvent<ReorderMovingEventArgs>` raised on the list. Its args carry `Item`,
  `OldIndex`, `NewIndex` and a settable `Cancel`. It is routed so XAML can attach it as `presto:Reorder.Moving`.
- `Reorder.Command` (`ICommand?`) takes that same `ReorderMovingEventArgs` as its parameter. When the command is set,
  the view model owns the change: Reorder calls `CanExecute`/`Execute` and moves nothing itself.

**Moving.** Each midpoint crossing is a one-step move. For several crossings in one frame, Reorder sends one step at a
time, so `NewIndex − OldIndex` is always ±1. The order is:

1. `Moving` is raised, and a cancel stops the step.
2. If `Command` is set, it runs.
3. Otherwise, an `ItemsSource` that is an `ObservableCollection<T>` gets `Move(old, new)`, through a delegate cached per
   type.

Any other `IList` needs `Command`. RemoveAt plus Insert is not used, because Presence and scroll anchoring would read
it as a removal and an insert. Without a command, the drag still lifts and springs back, and a trace warning says why.

**Gesture.**

- **Reorder owns the pointer, not `Draggable`.** A `Draggable` captures its own trigger
  and releases when that capture is lost, and a `Move` takes the capture from the container, or detaches the container
  outright in an `ItemsControl`. Reorder handles `PointerPressed` on the list in the tunnel phase, so a `ListBoxItem`
  cannot swallow it. It applies `Draggable`'s default drag threshold and then captures **the list**, plays the lift and
  raises the container's `ZIndex` at `Animation` priority so it draws over its neighbours.
- **Following the pointer.** Reorder tracks the dragged **item**. After each move's layout pass it re-finds the item's
  container with `ContainerFromItem` and moves the drag state onto it. It writes the `"Y"` (or `"X"`) shorthand
  through `RenderTransformComposer` as `pointer delta − (slot now − slot at grab)`, so the container never jumps. On a
  new instance, the lift is replayed seeked to its end.
- **Excluding the dragged container from FLIP.** The two translations compose, so Reorder keeps the dragged container's
  item `LayoutArm` disarmed from grab until the release spring settles. Reorder suspends the dragged container's item
  `LayoutArm` (`LayoutArm.Suspend` via `Animate.ItemArmOf`) whenever it moves onto a container, so an arm prepared by a
  move or scroll never arms it. The neighbours keep easing.
- **Keeping it realized.** Capture on the list does not keep the container realized, but focus does. Reorder focuses
  the container on grab and again after every move, because the move takes focus away. A non-focusable container can
  be recycled in a virtualized drag far past the viewport, and the docs on `Enabled` say so.
- **Edge auto-scroll.** `Draggable.AutoScroll`'s formula and its `ScrollSpeed`/`ScrollThreshold` defaults (1.5 and 20)
  move into an internal helper that both use. Reorder exposes no properties for them yet.
- **Release.** `Presto.Animate` takes the container's shorthand to 0 with the default spring
  (`Eases.Spring()`), and the lift animation is reversed. Once the spring settles, the container's `LayoutArm` resumes
  as if attaching, so it records its slot rather than flying there.
- **Escape.** Escape returns the item to its start index through the same path, one step at a time, then releases. A
  step that is cancelled or refused stops it where it is.
- **Keyboard.** Alt+Up/Down (Alt+Left/Right on a horizontal axis) on a focused container moves its item one step
  through the same path, and the container is focused again afterwards.

### 6.3 Tests and sample

- **Tests:** `ReorderContainerTests` (the spike above) and `ReorderTests` pin §6.2 (see PLAN.md decisions 49–53);
  `RecipeTests` and `RecipeMarkupTests` cover `ReorderLift` and `{presto:Reorder}`.
- **Sample:** the Board page has a horizontal, virtualized `Priorities` `ListBox` with a grip `Reorder.Handle` and
  `{presto:Reflow}`, pinned by `BoardPageTests.DraggingAPriorityByItsGripReordersTheListAndSettlesItInItsNewSlot`. The
  XAML Builder page has two lists, `{presto:Reorder}` and `{presto:Reorder Intensity=2}`, that print their XAML and the
  `Recipes.ReorderLift` calls, pinned by `BuilderPagesTests.XamlPageReorderListsLiftWithTheirRecipeAndReorder`.

### 6.4 Deviations and visual check

- **Deviations from the sketch.** Reorder owns the pointer (§6.2 Gesture), so edge-scroll settings are not exposed yet;
  `ReorderLift` gained `delay`; `ItemChanges` learned `Moved` (§6.2 Spike); the Board sample shows Alt+Left/Right
  because its list is horizontal.
- **Win32.** The desktop head's Board and XAML pages were opened through DevTools: the priorities row and both Builder
  lists render with their grips and rows. DevTools can send neither a drag nor a modified key, so the drag, Escape and
  Alt+arrow paths were verified headlessly only, by the tests above.

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
- **`Presence.Mode`** on a content host or `ItemsControl`, with values `Sync` (today) and `Wait`. Motion's `popLayout` was dropped: a ghost never holds layout, so it would equal `Sync`.
  - `Wait` holds the incoming content at opacity 0, and its `Enter`, until the ghost's exit completes. Phase 38's
    hold-until-measured is the precedent.

### 7.3 Tests and sample

- **Tests:** order and timing of child plays; a child override wins; an interrupted parent state retargets children;
  `Wait` enters after the exit completes; interrupting `Wait` with a third content.
- **Sample:** a menu whose items stagger in from a parent `Open` state, and a wizard using `Presence.Mode=Wait`.

### 7.4 Deviations and visual check

- **Deviations from the sketch.** `from` is its own property, `Motion.StaggerFrom`, beside the `double`
  `StaggerChildren`. Children are ordered by layout-slot origin under the source, ignoring render transforms, and only
  those whose winners change are counted. `Wait` also holds new content roots that have no Presence builder (PLAN.md
  decisions 54–56).
- **Win32.** Not watched yet. The stagger, `Order` and `From` timings, the `Wait` hold and the double-click case were
  verified headlessly only, by the tests pinning PLAN.md decisions 54–56 and `ReferencePagesTests`; the samples' `Order`, `From` and mode pickers are not covered by a test.

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

Each item is independent and landed on its own. None has an anime.js 4.5.0 upstream, so parity constraints do not apply;
PLAN.md decisions 57–63 pin the choices below.

### 9.1 Decisions as built

| Item | Who has it | Built |
|---|---|---|
| Custom ease from path data | GSAP CustomEase | `Eases.FromPath(pathData)` in core, and `fromPath(...)` in ease strings (the parser hands it the raw text between the parentheses). See below. |
| Wiggle, rough, slow-mo, expo-scale eases | GSAP EasePack, CustomWiggle | `Eases.Wiggle(wiggles = 10, WiggleType, Random?)`, `Eases.Rough(points = 20, strength = 1, clamp, RoughTaper, randomize, template, Random?)`, `Eases.SlowMo(linearRatio = .7, power = .7, yoyoMode)` and `Eases.ExpoScale(startScale, endScale, ease?)`, in `Eases.Pack.cs`. The strings are `wiggle(n[, type])`, `rough(points[, strength])`, `slowMo(ratio[, power[, yoyo]])` and `expoScale(start, end[, ease])`, camel or PascalCase. |
| Motion path through points | GSAP MotionPath `curviness`, Compose `keyframesWithSpline` | Core `MotionPath.Through(points, curviness = 1, closed)` with `Length`, `BezierPoints`, `Sample(distance, out x, out y, out angle)` and `Tweens(offset, rotateOffset)`. In the bridge, `PrestoAvalonia.CreateMotionPath(IReadOnlyList<Point>, curviness, closed, offset, autoRotate)`, `MotionPathGeometry(points, curviness, closed)` for drawing the track, and a trailing `autoRotate` on the `Shape` overload. |
| Follow the OS reduced-motion setting | Compose, Flutter, Motion `reducedMotion="user"` | `PrestoAvalonia.FollowSystemReducedMotion(Func<bool?>? probe = null, Action<bool>? changed = null)` returns an `IDisposable`, plus `PrestoAvalonia.WatchSystemReducedMotion(Action<bool?> changed, Func<bool?>? probe = null)` to read it without following and `PrestoAvalonia.RefreshSystemReducedMotion()`. The built-in probes are internal (`Platform/ReducedMotionProbe`). |
| Rolling-digit counter | SwiftUI `numericText` | `Recipes.RollDigits(textBlock, to, format, duration, delay, ease)` returns the playing `Animation`, as `CountUp` does. |
| Projectile/gravity as a to-value | GSAP Physics2D, Flutter `GravitySimulation` | `new Projectile(velocity) { Acceleration, Friction, Duration }` as a to-value, and `Projectile.Launch(speed, angleDegrees, gravity, friction)`, which returns an X and a Y projectile. `ProjectileSolver` resolves it where `Inertia` is resolved. |

**`Eases.FromPath`.**

- **Path data.** It reads M, L, H, V and C, absolute or relative (lowercase). x is progress and y is the eased value,
  with y pointing **up** and no flip, so a path that runs from `0,0` to `1,1` reads as it would on a graph.
- **Normalisation.** The path's start maps to (0, 0) and its end to (1, 1), and y may overshoot in between. When the
  start and end y are equal, y is only offset, not scaled.
- **Sampling.** Each cubic is flattened into 64 chords, and the result is sampled at equal x steps into a 513-entry
  table. Calling the ease looks up the table and interpolates linearly, with no allocation.
- **Errors.** An x that decreases, or a path with no width, throws `ArgumentException`. Any other command, or malformed
  data, throws `FormatException`.

**The ease pack.**

- **Wiggle returns to 0.** `Wiggle` is 0 at both ends, as GSAP's CustomWiggle is, so the tween's to-value acts as the
  swing amplitude and the property ends where it started. `WiggleType` gives the envelope: `EaseOut` (the default),
  `EaseInOut`, `Anticipate` (a strong first swing against the to-value), `Uniform`, and `Random` (a random amplitude per
  swing, decaying).
- **Rough** is built on `Eases.Linear` stops jittered around `template` (default `None`), and is exactly 0 and 1 at the
  ends. `RoughTaper` fades the jitter `In`, `Out` or at `Both` ends.
- **SlowMo and ExpoScale** follow GSAP's formulas. `ExpoScale` interpolates the scale geometrically, and throws
  `ArgumentOutOfRangeException` for a scale that is not positive or for equal scales.
- **Seeding.** In code, `Rough` and `Wiggle(…, WiggleType.Random)` draw from `Random.Shared` unless they are given a
  `Random`. The parser seeds both with `new Random(1)`, so a cached string always parses to the same curve.
- **Parser arguments.** `EaseParser` now keeps both the raw argument text and its comma tokens. Numbers parse as
  invariant-culture doubles, as before. `wiggle` reads a `WiggleType` name (case-insensitive), `slowMo` a `true`/`false`
  yoyo flag, and `expoScale` parses the rest of its tokens as an ease string, so `expoScale(1, 10, outElastic(1.5, .5))`
  works. An unknown identifier, a bad flag or an `expoScale` with fewer than two scales throws `FormatException`.

**`Projectile`.**

- **Model.** `x' = v`, `v' = a − k·v`, solved in closed form when the tween is built, the same place `Inertia` is
  resolved. `Velocity` and `Acceleration` are in units per engine-time unit (squared). `Friction` is the fraction of
  velocity lost per **millisecond** in either time unit, below 1.
- **Duration.** The flight time is `Projectile.Duration`, or the tween's own duration if that is null. Unlike `Inertia`,
  the flight time is not derived from the physics.
- **End and ease.** The end value is the physical end point. Each tween's ease is the displacement normalised over its
  own span, so a seek at any time matches the ballistic formula.
- **Turning back.** A throw that turns back is split at its apex into two tweens through `Plan.Then`, so each tween is
  monotonic. A throw that lands on its start still has a span to normalise by, and ends exactly there. A projectile
  that does not move at all is a `None`-eased tween to its start.
- **Launch.** `Launch` splits a 2D throw, with gravity on Y only and positive Y pointing down. The angle runs
  clockwise from +X, so −90 throws straight up.
- **Limits.** Like `Inertia`, it needs a single number property and cannot be a keyframe or part of a from/to pair. It
  composes by replace like any other tween. There is no `Animatable` counterpart to `Fling`.
- **Reduced motion.** Under `ReduceAll`, the trajectory is still resolved from the declared duration, then collapsed
  to an instant set, so the property lands on the physical end, not on the start.

**`MotionPath`.**

- **Spline.** A uniform Catmull-Rom spline, stored as cubic Béziers whose control points are
  `p ± (next − previous) · curviness / 6`. `curviness` 0 is the straight polyline, 1 the standard Catmull-Rom curve
  (GSAP's default), and larger values loosen it.
- **Ends.** An open path uses its first and last points as their own missing neighbours. A `closed` path wraps, so its
  end tangent meets its start.
- **Constant speed.** An arc-length table of 64 samples per segment gives constant speed along the path. `Sample` is
  allocation-free. It takes the angle from the curve's derivative, and from the segment's chord where a zero-length
  control arm has none (curviness 0).
- **Tweens and rotation.** `Tweens` clamps or wraps by `offset` exactly as the `Shape` overload does.
  `rotateOffset`/`autoRotate` is a number of degrees added to the tangent angle (GSAP's `autoRotate: 90`), not a
  switch: the `Rotate` tween is always returned, and a caller that does not want rotation leaves it out.
- **Coordinates.** Points are in the container's coordinates, and the animated control sits at its origin.

**`FollowSystemReducedMotion`.**

- **Opt-in.** Presto still never sets `ReducedMotion` by itself. A call writes `Engine.Current.Defaults.ReducedMotion`
  (the engine current at the call) from the probe straight away, and again whenever a window becomes active, the app is
  activated (`IActivatableLifetime.Activated`), the browser reports a change, or the app calls
  `RefreshSystemReducedMotion()`.
- **Writes.** The flag is written only when the reading differs from it, and `changed` is called then. A `null`
  reading means unknown and leaves the flag alone. Disposing the handle stops following and leaves the flag as it is.
- **No Avalonia source.** The Avalonia 12.1.2 assemblies expose no reduced-motion or client-area-animation setting, so
  the bridge has its own probes, none of which needs a platform TFM:
  - **Windows:** `SystemParametersInfo(SPI_GETCLIENTAREAANIMATION)`, read as reduced when animation is off.
  - **macOS:** `NSWorkspace.sharedWorkspace.accessibilityDisplayShouldReduceMotion`, through `objc_msgSend`.
  - **iOS:** `UIAccessibilityIsReduceMotionEnabled`.
  - **Browser:** `matchMedia("(prefers-reduced-motion: reduce)")`, whose `onchange` triggers a refresh.
- **Fallback.** Android (the animator duration scale needs Java interop, which a plain `net10.0` bridge has not got),
  Linux and anything else read `null`. There the app passes its own `probe` and calls `RefreshSystemReducedMotion` when
  it learns of a change.

**`Recipes.RollDigits`.**

- **Alignment.** The old text (the number the block shows, parsed in the current culture, or 0) and the new value are
  formatted with `format` (default `"N0"`) and lined up from the right.
- **Columns.** Unchanged characters stay plain glyphs. Each changed column is a clipped `Panel` holding the outgoing
  glyph, which moves to −lineHeight and fades out, and the incoming one, which comes in from +lineHeight. The
  direction flips when counting down, and a column that appears or disappears has only one glyph.
- **Timing.** Columns start right to left, `Tokens.StaggerStep` apart, each over `Tokens.Medium` with `Tokens.Ease`
  unless given.
- **Not TextSplitter.** The roll builds its own row of columns inside the block as one `InlineUIContainer`, because a
  split piece holds a single glyph and cannot gain or lose columns. The row's top and bottom margins, taken from the
  first line's baseline, ascent and height, make its line box match the plain text.
- **Ending and re-rolling.** At the end the block's plain text is restored. Calling it again mid-roll completes the
  running roll (tracked per `TextBlock`) and rolls on from there.
- **Not a counter.** It goes from the old value to the new one directly, without counting through the values between.
  Reduced, it hands off to `CountUp`'s set-at-once form.

### 9.2 Tests and sample

- **Tests (core):** `CustomEaseTests` (a line is linear, a cubic matches `CubicBezier`, overshoot, exact endpoints,
  unit-square normalisation, relative equals absolute, H/V steps, backwards x and malformed data throw, no allocation).
  `EasePackTests` (wiggle rests at both ends and crosses zero twice per wiggle, decaying, uniform and anticipating
  envelopes, rough exact at the ends, same seed same curve, clamped, jagged, follows its template at strength 0, slow-mo's
  constant slope and yoyo, expo-scale's geometric midpoint, its ease and its argument checks, no allocation).
  `EaseParserTests` (every new string form matches its factory, identifier and flag errors throw, `fromPath` takes the
  raw path, a string caches one instance). `ProjectileTests` (linear without gravity, the ballistic formula, friction,
  a toss landing on its start ends exactly there, seeks on both sides of the apex, `Launch`, `Duration` overrides,
  keyframe, from/to and colour errors, seconds mode, `ReduceAll`). `MotionPathTests` (passes through every point,
  curviness 0 is the polyline, a closed path's tangent meets its start, equal distances give equal chords, tangent and
  `autoRotate`, offset clamping and wrapping, an animation lands on the last point).
- **Tests (bridge):** `ShapesTests.PointMotionPathDrivesTheShorthandsThroughThePoints`, `SystemReducedMotionTests`
  (following sets the flag, a refresh picks up a flip and reports it, dispose leaves the flag, unknown leaves it alone,
  the platform probe reads without throwing), and `RollDigitsTests` (only changed digits roll, old above and new below
  inside the clip mid-roll, the line box is kept, ends as plain text, an added digit rolls in, right-to-left start,
  counting down rolls the other way, rolling again mid-roll ends on the second value, reduced sets the end at once).
- **Sample:**
  - The Eases page plots `FromPath` (an overshoot path), `Wiggle(6)`, `Wiggle(6, Anticipate)`, `Rough(20, 1)`
    (seeded), `SlowMo(.5, .8)` and `ExpoScale(.5, 4)`. The dot's travel now follows the plotted min and max, and an
    invalid typed argument shows its message instead of crashing.
  - The Animatable page has a Toss card, `Projectile.Launch(.6, -60, .0015)`.
  - The Shapes page has a third demo: a rider on a six-point spline with `autoRotate` 90, a curviness slider (0–2) and
    the drawn track.
  - The sidebar has a Follow system switch, which disables the manual Reduced motion switch while it is on.
  - Counters has a "Roll to a random value" readout, and the Recipe gallery a `RollDigits` tile beside `CountUp`.
  - Pinned by `ReferencePagesTests` (`EveryCatalogueEaseAndTypedEaseFormPlots`,
    `TossArcsTheBallUpAndLandsItBackOnTheFloor`), `GalleryPageTests.FollowingTheSystemLocksTheSidebarSwitchUntilTurnedOff`
    and `BuilderRecipePagesTests.RollDigitsRollsTheReadoutAndEndsAsPlainText`.

### 9.3 Deviations and visual check

- **Deviations from the sketch:**
  - `Rough` takes GSAP's full option set, seeded through a `Random` rather than a `seeded` flag.
  - `Projectile` takes per-axis velocity and acceleration, and `Launch` supplies the angle form.
  - `FollowSystemReducedMotion` has no built-in Android probe (the sketch listed the animator duration scale), since
    the `net10.0` bridge has no Java interop. Android apps pass their own `probe` (§9.1 Fallback).
- **Win32:**
  - The Windows probe runs on the dev machine in `ThePlatformProbeReadsWithoutThrowing`.
  - The `RollDigits` line-box margins came from the desktop head, where Consolas drew 3px taller without them.
  - The Eases, Toss, spline and Follow system demos were verified headlessly only, by the sample tests above. The
    Shapes spline demo has no sample test; `ShapesTests` covers its API.

## 10. Open questions (confirm before the relevant phase)

- **41 — names (settled):** `{presto:Follow}` / `Follow.Bind` / `Follow.*` (§1.7).
- **41 — default timing (settled):** a critically damped spring, `Springs.Gentle` (§1.7).
- **41 — `Follow.*` layering (settled):** the followed value is the composer's base layer. An animation overrides
  it, holds until it is reverted, then hands back (decision 13, §1.7 Q6).
- **42 — XAML (settled):** no `{presto:Map}` extension. Derived values stay in code and view models, and reach XAML
  through `{presto:Follow ..., Duration=0}` (§2.2).
- **42 — name (settled):** `MotionValue`, as in Motion. The bridge's `Motion` class is about state triggers (§2.2).
- **43b — depth (settled):** direct children only (§3.2).
- **44 — scope:** dotLottie in the same phase if the spike shows it is cheap?
- **45 — smoothed wheel scrolling (settled):** opt-in per scroller as `Scroll.Smooth`, Win32 only (§5.2).
- **48 — form:** a separate window or an in-window overlay? A separate window survives page navigation and does not
  perturb layout.
