# Monaco.Presto

A .NET port of the [anime.js](https://animejs.com/documentation) v4.5.0 animation model: a framework-agnostic core that
animates any property of any object, plus an Avalonia adapter that makes controls first-class targets.

The core is a faithful port of anime.js's timing model — the same clock, the same `tick()` maths for
delay/loop/alternate/reverse/seek/stretch, the same composition rules — so the upstream test suites port across
one-to-one. The API is C#-idiomatic: `init`-only parameter records, a string indexer for the properties you animate, and
the anime.js names in PascalCase.

| Package | What it is |
|---|---|
| `Monaco.Presto` | The engine: clock, timers, animations, timelines, tweens, values, eases, stagger, utils, animatable, scope. No UI dependency. |
| `Monaco.Presto.AvaloniaBridge` | Avalonia adapter: frame pump, `AvaloniaProperty` targets, transform composition, colour/brush/struct codecs, draggable, scroll observer, shapes, text, render-thread scenes. |

## Install

```powershell
dotnet add package Monaco.Presto
dotnet add package Monaco.Presto.AvaloniaBridge
```

The bridge is built against **Avalonia 12.1.2** and both packages target **net10.0**. The core has no package
dependencies at all; take it alone if you are driving a game loop, a view model or your own renderer.

## Quick start

### A plain object

```csharp
using Monaco.Presto;

var ship = new Ship();

var animation = Presto.Animate(ship, new()
{
    ["X"] = 320,                                // 0 → 320
    ["Opacity"] = new double[] { 1, 0.2 },      // explicit from → to
    Duration = 1250,
    Ease = Eases.InOutQuint,
    Loop = 1,
    Alternate = true,
    OnUpdate = _ => Console.WriteLine($"{ship.X:0} {ship.Opacity:0.00}"),
});

await animation;                                // anime.js .then()

sealed class Ship
{
    public double X { get; set; }
    public double Opacity { get; set; } = 1;
}
```

Properties are found by reflection and cached as delegates. Frames come from the built-in `StopwatchFrameSource`, which
posts to the `SynchronizationContext` of the thread that started it; tests swap in `ManualFrameSource` and call
`Advance(16)`.

### An Avalonia control

```csharp
using Avalonia;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

Square.Animate(new()
{
    ["X"] = 320,                                // transform shorthand → RenderTransform
    ["Y"] = -40,
    ["Rotate"] = new TweenParams { From = -180 },
    ["Scale"] = 1.4,
    [Visual.OpacityProperty] = 0.4,             // typed AvaloniaProperty
    ["Background"] = "#ff6b6b",                 // brush, from a CSS colour string
    Duration = 1250,
    Delay = 250,
    Ease = Eases.InOutQuint,
    Loop = true,
    Alternate = true,
});
```

Named properties resolve through `AvaloniaPropertyRegistry`, so `"Opacity"`, `"Width"`, `"Margin"` and attached
properties like `"Canvas.Left"` all work. Values are written at `BindingPriority.Animation`, so `Revert()` puts the
control's own values back exactly.

### Fluent builder

`Presto.Build()` is an optional, typed spelling of the same parameters. It only builds an `AnimationParams` and hands it
to `Presto.Animate`, so it can express nothing the indexer cannot; the indexer stays the primary spelling in these docs.

```csharp
Square.Animate(Presto.Build()                   // or Presto.Build()...Animate(Square)
    .X(320)
    .Y(-40)
    .Rotate(from: -180)
    .Scale(1.4)
    .Property(Visual.OpacityProperty, 0.4)
    .Background("#ff6b6b")
    .Duration(1250)
    .Delay(250)
    .Ease(Eases.InOutQuint)
    .Loop()
    .Alternate());

Presto.Build().Property("X", 320).Duration(1250).Animate(ship);   // core: any property by name
```

The transform and [effect](#effects) shorthands, `Opacity`, `Width`, `Height`, `Background`, `Foreground`, `CornerRadius`, `BorderThickness`,
`Margin`, `Padding` and the `Draw` / `Text` / `Data` pseudo-properties are bridge extension methods that write the same
keys the indexer does; anything else is `Property(SomeControl.SomeProperty, …)` or `Property("Name", …)`. A property
given only `to` is a bare value, and any other argument (`from`, `duration`, `ease`, …) makes it a `TweenParams`.
Naming a property again replaces its value but keeps its position, and a second callback replaces the first.

A builder is a reusable recipe. `Animate`, `Set` and `ToParams` each take a fresh snapshot, so changing the builder
afterwards never touches an animation already running:

```csharp
var pop = Presto.Build().Scale(from: 0.6, to: 1).Opacity(from: 0, to: 1).Duration(400).Ease(Eases.OutBack());
pop.Animate(first);
pop.Delay(200).Animate(second);                 // first keeps its zero delay
```

`With` varies a copy of a shared builder and leaves the original untouched (`Clone` is the bare copy). Anywhere a
builder is taken, a lambda also works; it configures a fresh `Presto.Build()`:

```csharp
pop.With(b => b.Delay(Presto.Stagger(60))).Animate(row.Children);   // pop itself is unchanged
control.Animate(b => b.X(100).Duration(400));
```

Keyframes, per property and animation-wide (`Frame` for duration-based frames, `At` for percentages; not both). Each
`To` is a frame ending on that value, so the first frame starts from the property's current value:

```csharp
Presto.Build()
    .X(k => k.To(200, duration: 500, ease: Eases.OutQuad).To(80, duration: 300, delay: 200).To(0, duration: 600))
    .Animate(box);

Presto.Build()
    .Keyframes(k => k
        .At(0, f => f.X(0).Scale(1))
        .At(60, f => f.Scale(1.6))
        .At(100, f => f.X(0).Scale(1)))
    .Duration(2500)
    .Ease("linear")
    .Animate(box);
```

Timelines and the render thread take a builder wherever they take parameters:

```csharp
var slide = Presto.Build().X(300).Duration(600).Ease(Eases.OutQuad);
Presto.CreateTimeline()
    .Add(a, slide)
    .Add(b, slide, "-=400")
    .Add(d, x => x.Y(-40).Duration(400), "<")
    .Add(strip.Children, Presto.Build().Y(-14), Presto.Stagger(60))
    .Set(c, Presto.Build().Opacity(0), "<");

var spin = icon.AnimateOnCompositor(Presto.Build().Rotate(360).Duration(1200).Ease(Eases.None).Loop());
```

`AnimateOnCompositor` throws `NotSupportedException`, naming the setting, for anything the builder set that the
[render-thread model](#render-thread) has no counterpart for (`Keyframes`, `OnUpdate`, `Composition`, …).

#### From XAML

The attached properties (`xmlns:presto="using:Monaco.Presto.AvaloniaBridge"`) take a builder three ways:

```xml
<Border presto:Presence.Arrive="{x:Static local:Motions.FadeUp}" />
<TextBlock presto:Presence.Arrive="{presto:Fade Direction=Up, Distance=48, Delay=100}" />
<PathIcon presto:Animate.OnAttached="{Binding Emphasis}" />
```

Use `x:Static` for a fixed recipe shared as-is, a markup extension when the markup parameterises it per element, and a
binding when the view model decides which animation runs. `Presence.Arrive`, `Initial`, `Enter`, `Reveal` and `Exit` play only
for their own moment of an element's lifecycle ([Lifecycle animations](#lifecycle-animations)), so entrances belong
there. `Animate.OnAttached` plays each time the visual attaches to the visual tree, including a virtualizing list's
recycling, and is reverted on detach, so it is the trigger for loops and timers tied to being on screen, such as a
skeleton shimmer or a countdown; a new value reverts the old animation and plays the new one, and `null` reverts and
stops listening. Nothing runs in design mode on either.

## Avalonia setup

Install the bridge once at startup:

```csharp
public static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .UsePresto()
        .WithInterFont();
```

`UsePresto()` is optional. Everything in the bridge calls `PrestoAvalonia.EnsureInitialized()` first, which points the
engine at the Avalonia frame source and registers the adapter and codecs. Call it yourself if you animate a control
through the core `Presto.Animate` overload rather than the `Animate` extension.

Frames come from `TopLevel.RequestAnimationFrame`, re-requested every frame while the engine has children, with a
render-priority `DispatcherTimer` fallback until a `TopLevel` exists. The engine stops the pump when nothing is running.

## Feature tour

### Timers

```csharp
var timer = Presto.CreateTimer(new()
{
    Duration = 1000,
    Loop = true,
    FrameRate = 30,
    OnUpdate = t => CurrentTimeText.Text = t.CurrentTime.ToString("0"),
    OnLoop = _ => loops++,
});
```

### Playback control

Every timer, animation and timeline has the same controls:

```csharp
animation.Pause().Resume();
animation.Restart();
animation.Reverse();
animation.Alternate();
animation.Seek(250);
animation.Progress = 0.5;
animation.Stretch(2000);
animation.Complete();
animation.Cancel();
animation.Revert();                     // undo the writes, restore the target
await animation.Then(a => Log(a.Id));
```

### Values

```csharp
["X"] = 220,                                        // number
["Rotate"] = "1turn",                               // deg / rad / turn
["X"] = "+=140", ["Scale"] = "*=1.5",               // relative
["Value"] = "75%",                                  // percentage of the current value
["Opacity"] = new double[] { 0.2, 1 },              // from → to
["Background"] = "hsl(280, 70%, 60%)",              // #rgb, #rrggbbaa, rgb(), rgba(), hsl(), hsla()
["BorderThickness"] = TweenValue.Of(new Thickness(10)),   // typed structs, via codecs
["Background"] = TweenValue.Of(new LinearGradientBrush { … }),   // linear/radial/conic, solid ↔ gradient
["BoxShadow"] = "0 14 28 2 #804c8dff",               // shadow lists, padded as CSS pads them
["Y"] = new FunctionValue((target, i, total) => -10 - i * 6),
```

Gradients with different stop counts are resampled onto the union of their offsets, a solid brush takes the gradient's
shape, and a gradient eased to a colour ends on a `SolidColorBrush`. Two kinds of gradient interpolate their stops, while
the geometry snaps to the end value's.

### Keyframes

Per-property, each frame with its own duration and ease:

```csharp
Presto.Animate(box, new()
{
    ["X"] = TweenValue.Keyframes(
        new TweenParams { To = 200, Duration = 500, Ease = Eases.OutQuad },
        new TweenParams { To = 80, Duration = 300, Delay = 200 },
        new TweenParams { To = 0, Duration = 600, Ease = Eases.InOutQuad }),
});
```

Or a shared timeline of frames, indexed by order or by percentage:

```csharp
Presto.Animate(box, new()
{
    Keyframes = new Keyframe[]
    {
        new() { ["Y"] = -40, ["Rotate"] = -20 },
        new() { ["X"] = 220, Ease = Eases.OutQuad },
        new() { ["X"] = 0, ["Rotate"] = 0, Duration = 600 },
    },
    Duration = 2000,
});

Presto.Animate(box, new()
{
    Keyframes = new Dictionary<string, Keyframe>
    {
        ["0%"] = new() { ["X"] = 0, ["Scale"] = 1 },
        ["60%"] = new() { ["Scale"] = 1.6 },
        ["100%"] = new() { ["X"] = 0, ["Scale"] = 1 },
    },
    Duration = 2500,
    Ease = "linear",
});
```

### Timelines

```csharp
var tl = Presto.CreateTimeline(new() { Defaults = new() { Duration = 600, Ease = Eases.OutQuad } })
    .Add(a, new() { ["X"] = 300 }, 0)               // absolute position
    .Add(b, new() { ["X"] = 300 })                  // appended
    .Add(c, new() { ["X"] = 300 }, "-=400")         // relative to the end
    .Add(d, new() { ["X"] = 300 }, "<")             // the previous child's end ("<<" for its start)
    .Label("flash", "+=200")
    .Add(e, new() { ["Rotate"] = 360 }, "flash")    // at a label
    .Add(new TimerParams { Duration = 400, OnUpdate = t => Show(t) }, 20)
    .Call(_ => Log("half way"), "flash")
    .Sync(existingAnimation, 0);

tl.Seek(tl.Labels["flash"]);
```

### Stagger

```csharp
Presto.Animate(strip.Children, new()
{
    ["Y"] = -14,
    Delay = Presto.Stagger(60),                                          // 60 ms apart
    Duration = 450,
});

Presto.Stagger(new double[] { 0, 700 }, new() { Ease = "inOutQuad" });   // a range, eased
Presto.Stagger(50, new() { From = StaggerFrom.Center, Total = 24 });
Presto.Stagger(40, new() { Grid = [7, 4], Axis = StaggerAxis.X, Reversed = true });
PrestoAvalonia.Stagger(40, new() { From = StaggerFrom.Center });         // grid measured from the layout
```

`Presto.Stagger` also produces the value of a property (`["X"] = Presto.Stagger(6)`) and the position of a timeline
child (`.Add(targets, parameters, Presto.Stagger(60))`).

### Eases

```csharp
Eases.OutQuad;  Eases.InOutQuint;  Eases.OutInBounce;  Eases.None;
Eases.Out(3);   Eases.InOutBack(1.7);   Eases.OutElastic(1, .3);
Eases.CubicBezier(.17, .67, .83, .67);
Eases.Steps(5);
Eases.Linear(0, "0.25 75%", 1);
Eases.Irregular(10, 1);
Eases.Spring(mass: 1, stiffness: 100, damping: 10, velocity: 0);
Eases.FromPath("M0,0 C0.3,0 0.4,1.2 0.7,1.1 C0.85,1.05 0.9,1 1,1");   // GSAP CustomEase, y up
Eases.Wiggle(6, WiggleType.Anticipate);   Eases.Rough(20, 1);           // GSAP CustomWiggle, RoughEase
Eases.SlowMo(.5, .8);   Eases.ExpoScale(.5, 4);                          // GSAP SlowMo, ExpoScaleEase
EaseParser.Parse("outElastic(1.2, .4)");
EaseParser.Parse("wiggle(6, anticipate)");
```

An `Ease` is just `double → double`, and anywhere one is accepted a string works too (`Ease = "inOutQuint"`). Avalonia's
own `Easing` types convert both ways through `PrestoEasing`.

The GSAP-style eases have no anime.js counterpart. `FromPath` takes M/L/H/V/C path data, normalised to run from (0, 0)
to (1, 1), and y may overshoot in between. `Wiggle` is 0 at both ends, so the to-value is the swing amplitude and the
property comes back to its start. `Rough` and a `WiggleType.Random` wiggle use `Random.Shared` unless given a `Random`.
In strings (`fromPath(…)`, `wiggle(n, type)`, `rough(points, strength)`, `slowMo(ratio, power, yoyo)`,
`expoScale(start, end, ease)`) they are seeded with 1, so a string always gives the same curve.

### Animatable

A set of paused per-property animations you push values into — for cursor followers and other continuously changing
targets, where a new animation per frame would be wasteful:

```csharp
var follower = Cursor.CreateAnimatable(new() { ["X"] = 500, ["Y"] = 500, Ease = Eases.Out(3) });

follower.Set("X", point.X).Set("Y", point.Y);    // eases toward the value over 500 ms
follower.Set("X", -140, 1200, "inOutQuad");      // one-off duration and ease
var x = follower.Get("X");
follower.Revert();
```

With a spring ease, `PreserveVelocity = true` keeps a property's momentum across retargets: each new spring starts
with the velocity the value already has instead of from rest, so a flicked follower swings through the turn. This is
not in anime.js, so it is opt-in:

```csharp
var dot = Dot.CreateAnimatable(new() { ["X"] = new(), Ease = new Spring(stiffness: 90, damping: 9), PreserveVelocity = true });
```

### Scope

A lifetime: everything built inside reverts together.

```csharp
var scope = Presto.CreateScope(new ScopeParams { Defaults = d => d.Ease = Eases.OutQuad });
scope.Add(_ => Presto.Animate(model, new() { ["Progress"] = 100 }));
scope.Revert();
```

The bridge roots a scope on a control, reverting when it leaves the visual tree:

```csharp
PrestoAvalonia.CreateScope(this).Execute(_ =>
{
    PrestoAvalonia.CreateDraggable(Card, new() { Container = Stage });
});
```

### Draggable

```csharp
PrestoAvalonia.CreateDraggable(SpringCard, new()
{
    Container = Stage,
    Snap = Utils.Snap(40),
    Y = new DraggableAxisParams { Enabled = false },
    ReleaseStiffness = 40,
    ReleaseDamping = 8,
    OnGrab = d => Log("grab"),
    OnUpdate = d => Show(d.X, d.Y, d.ProgressX, d.ProgressY),
});
```

`CancelOnEscape = true` adds what anime.js does not have: Escape mid-drag ends the drag and springs the target back to
where it was grabbed, then `OnRelease` and `OnSettle` run as for a release.

#### Drag to reorder

`Reorder` makes an `ItemsControl` or `ListBox` drag-to-reorder, as Motion's `Reorder` and Flutter's
`ReorderableListView` do. Grabbing a container (or, when its template has one, a `Reorder.Handle`) lifts it and it follows
the pointer. Each neighbour midpoint it crosses moves its item one step, and on release it springs into its slot:

```xml
<ListBox ItemsSource="{Binding Tasks}" presto:Reorder.Enabled="True" presto:Reorder.Lift="{presto:Reorder}"
         presto:Animate.ItemLayout="{presto:Reflow}">
    <ListBox.ItemTemplate>
        <DataTemplate>
            <StackPanel Orientation="Horizontal">
                <PathIcon Data="{StaticResource GripIcon}" presto:Reorder.Handle="True" />
                <TextBlock Text="{Binding Title}" />
            </StackPanel>
        </DataTemplate>
    </ListBox.ItemTemplate>
</ListBox>
```

`Animate.ItemLayout` slides the neighbours aside; the dragged container is left out of it. `Reorder.Lift` defaults to
`Recipes.ReorderLift()` (scale 1.03 and a raised shadow), `{presto:Reorder Intensity=2}` doubles it, and `Reorder.Axis`
is `Vertical` unless set. An `ObservableCollection<T>` is moved in place with `Move(old, new)`. Each step first raises
the bubbling `Reorder.Moving` (`Item`, `OldIndex`, `NewIndex`, settable `Cancel`), and when `Reorder.Command` is set it
receives the same args and the view model makes the move, which any other `IList` needs. Escape cancels a drag back to
its start index one step at a time, Alt+Up/Down (Alt+Left/Right horizontally) moves the focused item, and holding the
pointer near an edge scrolls the list. The dragged container is focused to keep it realized in a virtualizing panel, so
a non-focusable one can be recycled when a drag scrolls it far past the viewport.

### Inertia

Framer Motion's `inertia` and GSAP's InertiaPlugin, which are not in anime.js. An `Inertia` goes where a `To` goes: the
value coasts from a velocity and decays to rest. `ModifyTarget` snaps where it lands, and crossing `Min` or `Max` hands
over to a bounce spring that settles on the bound. The end value, ease and duration are worked out from the start
value, so a `Duration` beside it is ignored.

```csharp
Presto.Animate(card, new AnimationParams
{
    ["X"] = new Inertia(velocity) { ModifyTarget = Utils.Snap(240), Min = -960, Max = 0 },
});

follower.Fling("X", new Inertia(0) { Min = -300, Max = 300 });    // coasts from its current velocity
follower.Fling("Y", tracker.Velocity, new Inertia(0) { Max = 0 }); // or from a given one

PrestoAvalonia.CreateDraggable(Sheet, new()
{
    Container = Stage,
    X = new DraggableAxisParams { Enabled = false },
    ReleaseInertia = new Inertia(0) { TimeConstant = 250 },
});
```

`Animatable.Fling` replaces the property's ease and duration until the next `Set`. `DraggableParams.ReleaseInertia` is
opt-in: the default release stays the ported anime.js throw. When it is set, each axis flings at the pointer's velocity,
with the container as `Min`/`Max` and `Snap` as `ModifyTarget` unless the template sets its own. For a gesture of your
own, feed a `VelocityTracker` and pass its `Velocity` on:

```csharp
var tracker = new VelocityTracker();
Sheet.PointerMoved += (_, e) => tracker.Add(e.GetPosition(Stage).Y, Presto.Engine.FrameSource.Now);
Sheet.PointerReleased += (_, _) =>
{
    Sheet.Animate(new() { ["Y"] = new Inertia(tracker.Velocity) { Min = 0, Max = 400 } });
    tracker.Reset();
};
```

Velocity is in units per engine-time unit, so per **millisecond** by default, not per second as in Framer. The ideal
travel is `Power · Velocity · TimeConstant` (defaults 1 and 350). Framer's defaults, power 0.8 and timeConstant 700
with velocity per second, are `new Inertia(v) { Power = 1.14, TimeConstant = 700 }` here.

A `Projectile` (GSAP Physics2D, Flutter's `GravitySimulation`) also goes where a `To` goes. It flies from a launch
velocity under constant acceleration and friction for the tween's `Duration`, or its own:

```csharp
var (x, y) = Projectile.Launch(speed: .6, angleDegrees: -60, gravity: .0015);   // -90 is straight up, Y points down
Ball.Animate(new() { ["X"] = x, ["Y"] = y, Duration = 900 });

Presto.Animate(chip, new AnimationParams { ["Y"] = new Projectile(-1) { Acceleration = .002, Friction = .001 } });
```

Velocity and acceleration are per engine-time unit, and `Friction` is the fraction of velocity lost per millisecond. The
end value is where the physics puts it, so a toss that comes down on its start ends exactly there. It is split at the
apex into two tweens to get there.

### Transformable

Pan, pinch-zoom and rotate on one control, which anime.js has no counterpart for. Two fingers zoom and rotate about
their midpoint while one pans, and a scale pushed past `MinScale` or `MaxScale` rubber-bands. On release the pan and
rotation coast with an `Inertia` and the scale springs back inside its limits. A double-tap toggles zoom about the tap
point and Ctrl+wheel zooms about the cursor. `ZoomTo`, `PanTo`, `RotateTo` and `Reset` animate through the same
`Settle` animation.

```csharp
using Monaco.Presto.AvaloniaBridge.Gestures;

// Photo sits in a Border with ClipToBounds, its parent, which bounds the pan.
var viewer = Photo.CreateTransformable(new()
{
    Rotate = true,
    RotationSnap = 90,
    MaxScale = 5,
    OnSettle = t => Readout.Text = $"{t.Scale:0.##}× at {t.Rotation:0}°",
});
ResetButton.Click += (_, _) => viewer.Reset();
```

- **`PanAtRest`** is off by default. At `MinScale` a one-finger drag is left alone, so a `ScrollViewer` or `Carousel`
  around the photo still gets the swipe. Two fingers always engage, and once zoomed in one finger pans.
- **`WheelZoom`** is `WithControl` by default, which leaves a plain wheel to an enclosing scroller. `Always` zooms on
  every wheel and `None` never does.
- **One gesture owner per control.** Creating a `Transformable` on a control that a live `Draggable` drives throws, and
  so does the reverse. `Revert()` frees the control.
- **The transform is the `X`, `Y`, `Scale` and `Rotate` shorthands**, about `RenderTransformOrigin`. A `RenderTransform`
  of your own stays underneath them and is not part of the maths. A `Motion` hover on the same control would animate
  the same shorthands, so put hover motion on an inner element.

### onScroll

Watches a control cross a `ScrollViewer`'s thresholds, and drives whatever is linked to it:

```csharp
PrestoAvalonia.OnScroll(new() { Target = RevealCard }).Link(reveal);        // play on enter

PrestoAvalonia.OnScroll(new()
{
    Target = ThresholdCard,
    Enter = "bottom-=120 top",
    Leave = "top center",
    OnEnter = _ => Highlight(true),
    OnLeave = _ => Highlight(false),
});

PrestoAvalonia.OnScroll(new() { Target = SyncCard, Sync = true }).Link(timeline);   // scrub with the scroll
```

anime.js has no pinning, snapping or parallax. Presto adds them in the shape of GSAP ScrollTrigger and ScrollSmoother:

```csharp
PrestoAvalonia.OnScroll(new()
{
    Target = Stage,
    Enter = "center center",
    Leave = "center center+=960",
    Sync = true,
    Pin = true,                       // held in place for 960px of scroll
    Snap = ScrollSnap.Labels,         // or .25, or Utils.Snap([0, .3, 1]); tune with `with { Duration = 600 }`
}).Link(storyTimeline);
```

```xml
<ScrollViewer presto:Scroll.Smooth="True">
    <StackPanel>
        <Border presto:Scroll.Speed="0.6" />   <!-- parallax: moves at 60% of the scroll once it enters -->
        ...
    </StackPanel>
</ScrollViewer>
```

- **`Pin`** holds the target with its own `"Y"` (or `"X"`) shorthand, moved in the scroller's offset notification, so it
  never lags by a frame. Animate the target's children, not its `X`/`Y`. `PinSpacing` (on by default) adds the pinned
  distance to its trailing margin, so what follows arrives after the pin ends. Reverting the observer releases both.
- **`Snap`** eases the scroller's `Offset` to the snapped progress once scrolling has settled inside the thresholds
  with no pointer down, and any user scroll interrupts it. `ScrollSnap.Labels` lands exactly on a label under a linear
  `Sync`.
- **`Scroll.Speed`** moves an element by `(1 − speed) ×` the scroll since it entered the viewport: below 1 it lags,
  above 1 it leads.
- **`Scroll.Smooth`** eases mouse-wheel notches instead of jumping. It works on Windows only, where a notch can be
  told apart from touchpad input. Elsewhere it does nothing.
- **Scroll velocity as a value** is `PrestoAvalonia.ScrollValue(scroller).VelocityOf()`.

### Shapes

```csharp
PrestoAvalonia.CreateDrawable(SignaturePath).Animate(new()
{
    ["Draw"] = TweenValue.FromTo("0 0", "0 1"),     // stroke dash reveal
    Duration = 2200,
});

var motion = PrestoAvalonia.CreateMotionPath(TrackPath);
Rider.Animate(new() { ["X"] = motion.X, ["Y"] = motion.Y, ["Rotate"] = motion.Rotate, Duration = 3000 });

Point[] stops = [new(20, 120), new(120, 30), new(240, 150), new(360, 40)];
var spline = PrestoAvalonia.CreateMotionPath(stops, curviness: 1.2, autoRotate: 90);   // GSAP MotionPath curviness
Track.Data = PrestoAvalonia.MotionPathGeometry(stops, curviness: 1.2);

MorphPath.Animate(new() { ["Data"] = PrestoAvalonia.MorphTo(starGeometry), Duration = 900 });
```

The point-list overload runs a Catmull-Rom spline through the points at constant speed. `curviness` 0 is the straight
polyline, 1 the standard curve, and higher values loosen it. `closed: true` joins the last point back to the first.
`autoRotate` adds degrees to the tangent angle on either overload. The rider sits at the origin of the container the
points are in, and the core `MotionPath.Through` gives the same spline without Avalonia.

### Split and scramble text

```csharp
PrestoAvalonia.SplitText(Headline, new SplitTextParams
{
    Lines = new SplitPieceParams { Wrap = true },
    Chars = new SplitPieceParams { Class = "split-char" },
})
.AddEffect(split => Presto.Animate(split.Chars, new()
{
    ["Y"] = TweenValue.FromTo(44, 0),
    ["Opacity"] = TweenValue.FromTo(0, 1),
    Delay = Presto.Stagger(18),
}));

Transmission.Animate(new()
{
    ["Text"] = PrestoAvalonia.ScrambleText(new ScrambleTextParams { Text = "Signal acquired", From = ScrambleFrom.Center }),
});
```

#### Text reveals

`TextReveal.Pieces` (`None`, `Lines`, `Words`, `Chars`) on a `TextBlock` turns any builder into a text reveal:
`Presence.Initial`/`Enter`/`Arrive`/`Reveal`, `Animate.OnAttached` and `Animate.OnTapped` play it on the block's split
pieces instead of the block, each piece taking its own index and the piece count for function values and staggers.
`Motion` states and `Animate.OnChanged` still animate the block. `TextReveal.Mask="True"` wraps each piece in its own
clipping border.

```xml
<TextBlock Classes="headline" TextWrapping="Wrap" Text="{Binding Headline}"
           presto:Presence.Initial="{presto:LineMask}" />
<TextBlock Text="{Binding Body}" presto:TextReveal.Pieces="Words"
           presto:Presence.Reveal="{presto:FadeUp Stagger=True}" />
```

The block is held at `Opacity` 0 from the trigger until it has been laid out and split, a resize re-splits it and the
reveal carries on at the same time, and a reveal that completes puts the text back together (a looping one stays
split). Reduced-motion forms play on the block unsplit. `TextSplitter.StaggerByLine(lineStep, pieceStep, start)` is a
delay function of line index × `lineStep` + index on the line × `pieceStep`, and `TextSplitter.LineHeightOf(piece)` is
the owning split's line height (0 for a control that is not a piece). The ready-made reveals are
[recipes](#recipes).

### Render thread

Everything above runs on the UI thread, which means everything above stops when the UI thread does. A `CompositorScene`
does not: it is a world of its own — its own `Engine`, its own `Scope`, its own drawing — animated and drawn by
Avalonia's compositor.

```csharp
using Avalonia;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge.Compositor;

public sealed class SpinnerScene : CompositorScene
{
    private readonly Dot[] _dots = [.. Enumerable.Range(0, 12).Select(_ => new Dot())];

    protected override void OnAttached() =>
        Presto.Animate(_dots, new()                             // registers with this scene's engine
        {
            ["Opacity"] = new double[] { 1, 0.15 },
            Duration = 900,
            Delay = Presto.Stagger(75, new() { From = StaggerFrom.Center }),
            Ease = Eases.InOutSine,
            Loop = true,
        });

    protected override void OnRender(ImmediateDrawingContext context, Size size)
    {
        for (var i = 0; i < _dots.Length; i++)
        {
            var angle = i * Math.Tau / _dots.Length - Math.PI / 2;
            var center = new Point(
                size.Width / 2 + Math.Cos(angle) * 22,
                size.Height / 2 + Math.Sin(angle) * 22);
            context.DrawEllipse(Brush(Colors.SteelBlue, _dots[i].Opacity), null, center, 4, 4);
        }
    }

    private sealed class Dot { public double Opacity { get; set; } = 1; }
}
```

A `CompositorCanvas` hosts it. The scene goes to the render thread when the canvas enters the visual tree, and its scope
is reverted when the canvas leaves:

```csharp
var spinner = new CompositorCanvas { Width = 64, Height = 64, Scene = new SpinnerScene() };

spinner.Post("pulse");                                  // → OnMessage("pulse"), on the render thread
spinner.Invoke<SpinnerScene>(s => s.Engine.Speed = 2);  // ditto, against the live scene
```

The rules that come with the thread:

- **From attach on, the scene belongs to the render thread.** `OnAttached`, `OnMessage`, `OnRender` and the callbacks of
  everything they create run there. In Debug the engine's thread pin catches UI-thread code that reaches into a live
  scene.
- **The UI thread talks to it through `Post` and `Invoke<TScene>` only.** Both go through the compositor's batch queue
  and arrive in order; messages posted before the canvas is shown are queued behind the attach.
- **Scene callbacks must not touch controls.** `OnUpdate`, `OnComplete` and the rest fire off the UI thread;
  `PostToUI(Action)` is the way back. `Then()` and `await` on a scene timer complete on the thread pool, because the
  render thread has no synchronization context.
- **Drawing is `ImmediateDrawingContext`**: bitmaps, lines, rectangles, ellipses and glyph runs, with immutable brushes
  and pens. `Brush(color, opacity)` caches them per scene, so drawing from a tween does not allocate a brush per frame. There is no path or geometry drawing — reach for the Skia lease feature if you
  need one.
- **The scene's `Engine` is its own.** It starts from a copy of `Presto.Defaults`, and after that `Presto.Engine`'s
  speed, fps and pause state do not apply to it.

`ParticleScene` is a ready-made scene of emitted particles: `canvas.Post(new ParticleScene.Emit(emitterParams, origin))`
starts a core `ParticleSystem` drawn as a `ParticleShape` (rect, rounded rect, circle, streak, glyph or bitmap), capped
at `MaxLive` particles; the [particle recipes](#confetti-sparkles-and-bursts) use it.

A scene only draws itself. To move an *existing* control on the render thread — through the composition visual Avalonia
already gives it — there is `AnimateOnCompositor`, the anime.js `waapi.animate` analogue:

```csharp
var spin = icon.AnimateOnCompositor(new() { ["Rotate"] = 360, Duration = 1200, Ease = Eases.None, Loop = true });
await busyWork;                 // the icon keeps turning even while this blocks the UI thread
spin.Revert();
```

It is a reduced model, not a second `Animate`. It animates `X`, `Y`, `Rotate`, `Scale`, `ScaleX`, `ScaleY` and
`Opacity` and throws for anything else; the animation is compiled up front into composition key frames, so there are no
per-frame callbacks, no `Blend` composition and no timelines. `Pause`, `Resume`, `Play`, `Reverse`, `Alternate`,
`Seek`, `Restart`, `Complete`, `Progress` and `Speed` are there, but a composition key frame animation has none of
them of its own: each one stops what is running and starts a freshly compiled animation from where the UI-side model
stands, so the two are a frame apart until the next call. `Cancel`, `Revert` and `Then` finish the surface. Everything
else about a control is an `AvaloniaProperty` write, which is UI-thread work and stops with the UI thread. WebAssembly
has a caveat of its own, in [Platforms and status](#platforms-and-status).

The same composition transform eases a control into its new place whenever layout moves it, as FLIP:

```csharp
PrestoAvalonia.AnimateLayout(card, duration: 300, ease: Eases.OutQuint);   // or card.AnimateLayout(300, Eases.OutQuint)
PrestoAvalonia.AnimateSharedElement(thumbnail, heroImage, duration: 400, ease: Eases.OutQuint);
```

Where the control was and where layout has just put it are measured on the UI thread, the move is inverted onto the
composition `Translation`, and the render thread eases that back to identity, so the move survives a blocked UI thread.
`LayoutAnimateMode.PositionAndSize` inverts a resize onto the composition scale as well, about the control's top left
corner, which scales its content with it. `LayoutAnimateMode.PositionAndSizeCorrected` counter-scales the control's
direct children on the render thread so they keep their size and alignment; what the control draws itself, corner
radius and shadow included, still scales, so a rounded surface wants [`Animate.ContentSize`](#content-size) instead. Layout itself is unchanged: `Bounds` is right in the same pass and only the
pixels lag behind, so a control is hit where layout put it while it is still drawn on the way there. `Revert()` takes it
off again.

A shared element is measured where it is seen, scroll included, so a card in a scrolled list flies from the right place.
When the two views never exist at once, as with navigation driven by a view model, prepare the source under a key
before its view goes and start the target from that key when the next view is built, as the Community Toolkit's
connected animations do:

```csharp
PrestoAvalonia.PrepareSharedElement("photo", thumbnail);                        // leaving view
PrestoAvalonia.AnimateSharedElement("photo", heroImage, 500, Eases.OutQuint);   // arriving view; null if nothing prepared
```

A prepared entry keeps only the rectangle and lapses after three seconds. Every layout and shared-element call also
takes an `AnimationBuilder` for its motion: duration, delay, ease, and per-axis eases given as `.X(ease: …)`,
`.Y(ease: …)` or `.Scale(ease: …)`; easing X and Y differently curves the flight into an arc. Layout owns the values,
so giving X, Y or Scale a value, or setting anything else, throws.

```csharp
PrestoAvalonia.AnimateSharedElement("photo", heroImage, Presto.Build().Duration(600).Ease(Eases.OutQuint).Y(ease: Eases.InOutCubic));
```

During the flight a control is still drawn inside its own parents, so one flying into a clipped container, such as a
`ScrollViewer`, is clipped to it until it lands.

#### Layout animation from XAML

`Animate.Layout` keeps the same transition armed while the control is attached, and `Animate.ItemLayout` arms one on
every item container of a list. `{presto:Reflow}` is the [recipe](#recipes-from-xaml) made for them:

```xml
<Border presto:Animate.Layout="{presto:Reflow}" presto:Animate.LayoutMode="PositionAndSize" />

<ItemsControl ItemsSource="{Binding Rows}" presto:Animate.ItemLayout="{presto:Reflow 320}">
    <ItemsControl.ItemTemplate>
        <DataTemplate><Border presto:Presence.Enter="{presto:Fade}" presto:Presence.Exit="{presto:Blur}" /></DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>

<Style Selector="StackPanel.accordion > Border">
    <Setter Property="presto:Animate.Layout" Value="{presto:Reflow}" />
    <Setter Property="presto:Animate.LayoutMode" Value="PositionAndSize" />
</Style>
```

Attaching records where the control is without easing its arrival, detaching reverts, and changing the builder or the
mode re-arms; `null` stops. `Animate.LayoutMode` set on the list applies to its containers. A container that is realized
or recycled lands in its slot at once instead of flying there from its old one, and a pass that only scrolls the list
eases nothing. A `Connected` flight or a code `AnimateLayout` call on the same control wins its move; the property takes
over again after a flight lands. The builder takes the same timing and eases as the code API and throws for anything
else.

Layout and `Presence` recipes drive the same composition `Translation` and `Scale`, so the container moves and the content
arrives: put the layout property on the container (or a wrapper) and `Presence` on its content, as the list above does.
An opacity-only or blur entrance can share the element.

#### Content size

`Animate.ContentSize` (or `PrestoAvalonia.AnimateContentSize(host, duration, ease)` / `host.AnimateContentSize(…)`)
eases a host's real layout size on the UI thread whenever its content changes size, as Compose's `animateContentSize`
does, and clips the content meanwhile. Nothing is scaled, so corner radius, shadow and text stay true, and the content is
laid out at its final size from the first frame, so text never rewraps. The host is laid out every frame, which suits a
panel rather than hundreds of rows.

```csharp
panel.AnimateContentSize(300, Eases.OutQuint);   // or presto:Animate.ContentSize="{presto:Reflow}" in XAML
panel.Child = nextView;                          // a view with presto:Presence.Enter and Exit swaps through a ghost
```

Swapping the host's `Content` or `Child` resizes it from the size drawn to the new content's while `Presence` ghosts the
old content out, clipped to the old box, and enters the new one (≈ `AnimatedContent` with `SizeTransform`). The host owns
its size: `Animate.Layout` on it eases only its moves, and its neighbours' layout moves land with the resize rather than
trailing it. The builder takes timing and an ease only.

#### Connected animations from XAML

With view-model navigation (a `ContentControl` or `TransitioningContentControl` swapping views through DataTemplates),
the prepare and start calls can move into markup, and neither view needs code:

```xml
<!-- Gallery item template -->
<Border Classes="photo" presto:Connected.Key="{Binding Id, StringFormat=photo-{0}}" />

<!-- Detail view -->
<Border Classes="photo hero"
        presto:Connected.Key="{Binding Id, StringFormat=photo-{0}}"
        presto:Connected.Motion="{x:Static local:Motions.Flight}" />
<StackPanel presto:Connected.Anchor="{Binding Id, StringFormat=photo-{0}}">…</StackPanel>
```

| Property | Type | Question | Where |
|---|---|---|---|
| `Key` | `string?` | Which element is the same thing in both views? | The matching element in both views |
| `Motion` | `AnimationBuilder?` | How does this element arrive? | A keyed element (its flight) or an anchored one (its entrance) |
| `Anchor` | `string?` | Which keyed flight does this element arrive with, without flying itself? | Elements around the destination |

Arrival is attachment to the visual tree: a keyed element flies from another attached element with its key, or else
from where the last one to leave was drawn, and otherwise shows normally. The arriving element's `Motion` wins; the
leaving element's is never used. Without one, flights use `Connected.DefaultMotion` (500 ms, `OutQuint`) and anchored
entrances `Connected.DefaultAnchorMotion` (rise 24 px and fade in over 400 ms, `OutQuart`, 150 ms delay); both are
settable.

### Lifecycle animations

`Presence` classifies every arrival and departure of an element once, by one rule, and plays the builder for that moment:

| Moment | When | Property |
|---|---|---|
| **Initial** | the element appears with its view: the window's first show, a page navigated in, the rows a list starts with, a view shown again | `Presence.Initial` |
| **Enter** | it arrives on its own into a view that has loaded: an item added, content swapped in, a child added to a panel | `Presence.Enter` |
| **Reveal** | a virtualizing list realizes its row for an item it already had, such as a row scrolled into view | `Presence.Reveal` |
| **Exit** | it leaves on its own while its loaded parent stays: an item removed, content swapped out | `Presence.Exit` |
| *Unload* | it leaves with its view, such as a page navigated away | nothing plays |

`Presence.Arrive` plays on every classified arrival, Initial and Enter alike, and a set `Initial` or `Enter` wins for its
own moment. So "rise on page load, slide when added" is `Arrive` plus one override:

```xml
<DataTemplate x:DataType="local:Task">
    <Border Classes="task"
            presto:Presence.Arrive="{x:Static local:Motions.RiseIn}"
            presto:Presence.Enter="{x:Static local:Motions.SlideIn}"
            presto:Presence.Exit="{x:Static local:Motions.SlideOut}">…</Border>
</DataTemplate>
```

The rule: an arriving element's ancestors are walked up past the new wrappers around it, such as its item container,
and stop at the first panel or other host. A loaded host (`Control.IsLoaded`) means the element entered on its own; a host
still in its first layout means it came with its view. A departure exits only when every visual between the element and
the root of what was removed wraps only it, and the parent it leaves has loaded. Inside a list the list itself decides: a row
enters only when its item was just added and exits only when it was just removed, so a virtualizing `ListBox` can scroll
without rows animating as it realizes and recycles them.

Elements arriving in one pass under one panel form a batch, and each one's position in it is the index its function
values and staggers see, so `RiseIn = Presto.Build()….Delay(Presto.Stagger(60))` ripples a list in with no code.
Staggers run in arrival order, since the batch size is not known while it is still arriving.

`Presence.Reveal` plays when a virtualizing list realizes a row for an item it already had, which is a row scrolled into
view. It never plays on Initial, Enter or a recycle, `Arrive` does not cover it, and outside a list's item container it
does nothing. A list that does not virtualize realizes nothing on scroll, so scroll reveals there use
[`OnScroll`](#onscroll). By default a row reveals once per item (`Presence.RevealOnce`, default `true`): the list
remembers every item whose row has been shown, whether by Initial, Enter or Reveal, so scrolling back up plays nothing.
A realized row counts as shown only once a layout pass ends with it in the viewport, so rows inserted above a scrolled
list reveal a scroll step at a time as the reader scrolls back up to them. The list forgets an item that leaves `Items`, and a reset clears the memory; with `RevealOnce` false every realization reveals.
Rows realized in one layout pass form a batch like any other, so a stagger ripples them in the scroll direction. A jump or
fling that moves the list more than one viewport in a pass snaps instead: its rows are marked seen and land at rest.

```xml
<DataTemplate x:DataType="local:Event">
    <Border Classes="event"
            presto:Presence.Initial="{presto:Fade Direction=Up, Stagger=60}"
            presto:Presence.Reveal="{presto:Fade Direction=Up, Distance=12, Stagger=30}">…</Border>
</DataTemplate>
```

A removed control is gone in the same frame, since an items container may be recycled at once and a panel child
re-added. So an exit plays on a *ghost*: a snapshot of the element, drawn in the window's overlay where the element was
and clipped as it was. The ghost is an `Image`, so an exit animates the transform shorthands, the [effect](#effects) shorthands and `Opacity`. It keeps the
element's styles and resources, and a transform the shorthands held moves onto it; content that was animating inside the
element holds still. The snapshot is taken before the element's own entrance is reverted, so a row removed mid-entrance
leaves from where it was drawn. An element added back while its ghost is still exiting dismisses the ghost. Code that
removes a control itself can call `PrestoAvalonia.AnimateExit(control, builder)` first, in the same frame.

Because a ghost leaves layout at once, an enter normally plays alongside the exit (`Presence.Mode`'s default `Sync`, which
is why there is no Motion-style `popLayout`). `Presence.Mode="Wait"` on a content host or list plays the exit first, like Motion's
`AnimatePresence mode="wait"`: what arrives under it stays at opacity 0, its entrance held back, until every exit in
flight there has ended. Content swapped again while held leaves unseen, with no ghost.

```xml
<ContentControl Content="{Binding Step}" presto:Presence.Mode="Wait" />

<DataTemplate x:DataType="local:Step">
    <Border presto:Presence.Enter="{presto:Slide Edge=Right, Distance=48}"
            presto:Presence.Exit="{presto:Slide Edge=Left, Distance=48}">…</Border>
</DataTemplate>
```

`Animate.OnAttached` is the unclassified "while attached" trigger for loops and timers; see [From XAML](#from-xaml).

### State motion

`Motion` gives an element states. While a state's condition holds, the properties its builder names go to its values;
when the condition stops holding, they go back. This is Framer Motion's `whileHover`/`whileTap`/`whileFocus` and variants,
keyed on Avalonia's own pseudo-classes and classes.

```xml
<Button Content="Save"
        presto:Motion.Hover="{x:Static local:Motions.Lift}"
        presto:Motion.Press="{x:Static local:Motions.Squish}"
        presto:Motion.Focus="{x:Static local:Motions.Ring}" />

<Border Classes="status" presto:Motion.State="{Binding Status}">
    <presto:Motion.States>
        <presto:MotionStates>
            <presto:MotionState When="Syncing" Motion="{x:Static local:Motions.Pulse}" />
            <presto:MotionState When="Failed" Motion="{x:Static local:Motions.Alarm}" Leave="{x:Static local:Motions.Calm}" />
            <presto:MotionState When=".selected:pointerover" Motion="{x:Static local:Motions.Raise}" />
        </presto:MotionStates>
    </presto:Motion.States>
</Border>
```

```csharp
public static readonly AnimationBuilder Lift = Presto.Build().Y(-4).Duration(220).Ease(Eases.OutQuart);
public static readonly AnimationBuilder Squish = Presto.Build().Scale(.95).Duration(120).Ease(Eases.OutQuad);
public static readonly AnimationBuilder Pulse = Presto.Build().Opacity(k => k.To(.35).To(1)).Duration(900).Loop();
public static readonly AnimationBuilder Calm = Presto.Build().Duration(900).Ease(Eases.OutQuad);   // timing only
```

| Property | Holds while |
|---|---|
| `Hover` | `:pointerover` |
| `Focus` | `:focus-visible`: keyboard focus only, so a click does not play it (`When=":focus"` plays on both) |
| `Press` | `:pressed`, which Presto synthesises for every element, not only buttons: a left button, touch or pen press until release or capture loss, so a touch that turns into a scroll ends it |
| `States` | each `MotionState`'s `When` |
| `State` | *(not a state)* an `object?` whose `ToString()`, split on whitespace and commas, names the bare-name conditions that hold; an enum matches by name |

A `When` is ANDed tokens on the element itself: `:pseudo-class`, `.class`, and a leading bare name matched against
`Motion.State`, as in `:checked`, `.selected:pointerover` or `Failed:focus`. It is not a selector: descendant, child and
template parts throw `ArgumentException`. For anything a selector can express, set `Motion.State` from a style setter and
name that token in the state. Here hovering a card nudges its arrow; when the selector stops matching, the setter's value
goes away and the arrow returns:

```xml
<Style Selector="Path.arrow">
    <Setter Property="presto:Motion.States">
        <presto:MotionStates>
            <presto:MotionState When="hot" Motion="{x:Static local:Motions.Nudge}" />
        </presto:MotionStates>
    </Setter>
</Style>
<Style Selector="Border.card:pointerover Path.arrow">
    <Setter Property="presto:Motion.State" Value="hot" />
</Style>
```

States layer by declaration order: `Hover`, then `Focus`, then `Press`, then the `States` entries, then states added
from code, and a later state wins the properties it shares with an earlier one. So press wins a shared `Scale` over
hover, and letting go returns it to hover's value. A property no holding state names returns to rest: an
`AvaloniaProperty` to its base value (the style, binding or local value beneath the animation, read as it returns, so
one changed mid-hover is honoured), a transform shorthand to its identity. When that return finishes, the animated
value is reverted, uncovering the equal value beneath. The return uses the leaving state's `Leave` builder if it has
one, otherwise the state's own timing; `Leave` takes only duration, delay and eases (`.Scale(ease: …)`), and a value
there throws. State values are absolute: a relative `"+=4"` or a `From` without a `To` throws, since a state re-enters.

Each transition plays one animation per state that gains or loses properties, with replace composition, so it takes
them over from where they stand: a hover during a `Presence.Enter` goes straight to the lift, and the entrance keeps
the rest. States that already hold when the element attaches are applied instantly, so a recycled row or a view shown
again arrives in its state instead of replaying it; detaching cancels and reverts every state animation. A
`MotionState` is immutable, so one `MotionStates` list can be shared through a style while each element keeps its own
runtime state. Nothing runs in design mode.

From code, `WhileHover`, `WhilePressed`, `WhileFocused` and `While(when, …)` add a state after the declared ones, taking
a builder or a lambda; disposing the handle removes it, and its properties return like those of any state that stops
holding:

```csharp
using var hover = card.WhileHover(b => b.Y(-4).Scale(1.02).Duration(180).Ease(Eases.OutQuad));
Motion.SetState(chip, SyncStatus.Failed);
```

`Motion.State` also reaches every visual descendant with states that does not set its own, so one value drives a whole
subtree, as Motion's variants do. The parent times those children: `Motion.StaggerChildren` starts each one that many
milliseconds after the previous, in on-screen order (top to bottom, then left to right) from `Motion.StaggerFrom`;
`Motion.DelayChildren` shifts them all; and `Motion.Order` plays the parent's own animations `Together` with them,
`BeforeChildren` or `AfterChildren`. Clearing the state mid-stagger returns the started children from where they stand
and never starts the rest.

```xml
<Style Selector="Border.item">
    <Setter Property="Opacity" Value="0" />
    <Setter Property="presto:Motion.States">
        <presto:MotionStates>
            <presto:MotionState When="Open" Motion="{x:Static local:Motions.SlideIn}" />
        </presto:MotionStates>
    </Setter>
</Style>

<Border Classes="menu" Opacity="0" presto:Motion.State="{Binding MenuState}"
        presto:Motion.StaggerChildren="40" presto:Motion.DelayChildren="60" presto:Motion.Order="BeforeChildren">
    <presto:Motion.States>
        <presto:MotionStates>
            <presto:MotionState When="Open" Motion="{x:Static local:Motions.Unfold}" />
        </presto:MotionStates>
    </presto:Motion.States>
    <StackPanel>
        <Border Classes="item"><TextBlock Text="New file" /></Border>
        <Border Classes="item"><TextBlock Text="Share" /></Border>
    </StackPanel>
</Border>
```

One-shot triggers are not states and stay on `Animate`. `Animate.OnTapped` plays its builder on each tap, restarting
it; `Animate.OnChanged` plays each time `Animate.Watch` changes, but not for its first value. Both revert when the
animation completes or the element detaches:

```xml
<Button presto:Motion.Press="{x:Static local:Motions.PressIn}"
        presto:Animate.OnTapped="{x:Static local:Motions.Pop}" />
<TextBlock Text="{Binding Count}"
           presto:Animate.Watch="{Binding Count}"
           presto:Animate.OnChanged="{x:Static local:Motions.Bump}" />
```

### Effects

Blur, drop shadow, 3D rotation and clip or mask reveals are shorthands, like the transform ones. They are written at
`Animation` priority and changed in place every frame, so nothing is allocated per frame, and `Revert()` uncovers the
control's own `Effect`, `Clip`, `OpacityMask` or `RenderTransform`.

```csharp
card.Animate(b => b.Shadow(0, 12, 24, Color.Parse("#66000000")).Duration(300).Ease(Eases.OutQuad));
title.Animate(b => b.Blur(from: 12, to: 0).Opacity(from: 0, to: 1).Duration(500));
tile.Animate(b => b.RotateY(180).Perspective(600).Duration(700).Ease(Eases.InOutQuart));
photo.Animate(b => b.ClipCircle(from: 0, to: 1).ClipCircleCenter(new RelativePoint(0.2, 0.3, RelativeUnit.Relative)).Duration(600));

photo.Animate(new() { ["ClipInset"] = new[] { "50%", "0%" }, Duration = 900, Ease = Eases.OutQuart });
photo.Animate(new() { ["MaskSweep"] = new double[] { 0, 1 }, ["MaskSweepAngle"] = 90, Duration = 1100 });
```

| Shorthand | Drives | Values |
|---|---|---|
| `Blur` | `BlurEffect.Radius` | DIPs |
| `ShadowX`, `ShadowY`, `ShadowBlur` | `DropShadowEffect.OffsetX` / `OffsetY` / `BlurRadius` | DIPs |
| `ShadowColor` | `DropShadowEffect.Color` | colour string, `Color` |
| `ShadowOpacity` | `DropShadowEffect.Opacity` | 0–1 |
| `RotateX`, `RotateY` | `Rotate3DTransform.AngleX` / `AngleY` | degrees (`turn` / `rad` accepted, as `Rotate`) |
| `Perspective` | `Rotate3DTransform.Depth` | DIPs; 0 is no perspective |
| `ClipInset` | `Visual.Clip` rectangle | left, top, right, bottom in `Thickness` order (`"10%"`, `"0 20"`, `"0 0 100% 0"`, a `Thickness`), each in DIPs or `%` of the bounds |
| `ClipCircle` | `Visual.Clip` ellipse | radius fraction, 1 reaching the corner farthest from the centre |
| `ClipCircleCenter` | the circle's centre | `RelativePoint`, default 50%, 50% |
| `MaskSweep` | `Visual.OpacityMask` gradient | progress, 0 hidden to 1 shown |
| `MaskSweepAngle` | the sweep's direction | degrees, 0 sweeping left to right (`turn` / `rad` accepted) |
| `MaskSweepFeather` | the soft edge's width | fraction of the sweep, default 0.2 |

Each has the usual four builder overloads (bare, tween settings, `FunctionValue`, keyframes), plus `ShadowColor(Color)`,
`ClipInset(Thickness)`, `ClipCircleCenter(RelativePoint)` and `Shadow(x, y, blur, color)`, which sets the four shadow
shorthands at once. A user effect of the same kind supplies a shadow's or blur's starting values; without one a shadow
starts black, opaque, unblurred and at no offset, so animating `ShadowBlur` alone shows it.

- **One effect per visual.** Avalonia has no effect group, so animating a blur and a shadow on one visual at once throws
  `InvalidOperationException` from the `Animate` or `Set` call; one after the other switches the kind. Put the blur on
  an inner element of a shadowed card, and on a `Border` prefer `BoxShadow`, which is cheaper and not exclusive. A
  visual likewise holds one clip, so `ClipInset` and `ClipCircle` together throw too.
- **Removed at rest.** Any effect costs an offscreen pass, so a blur of 0, or a shadow whose `ShadowOpacity` ended at
  0, is removed rather than left installed. A reveal that ends fully visible (every inset 0, a radius of 1, a sweep at
  1) removes its clip or mask the same way.
- **UI thread only.** The composition layer has no counterpart for any of them, so `AnimateOnCompositor` throws
  `NotSupportedException` for these shorthands.

#### Image filters

CSS colour filters are properties of `PrestoImageBase`, an `Image`-like control (`Source`, `Stretch`,
`StretchDirection`) whose `Render` subclasses can override. `FilteredImage` is its plain form, and `SpriteSheet` and
`ImageSequence` derive from it too, so frames are filtered the same way. A subclass that overrides `Render` can draw
any image, or several cells of one, through the protected `DrawImage(context, image, sourceRect)`, which applies the
filters and places the cell by `Stretch` within the control. The filters are ordinary styled properties, so
they animate, bind, style and revert like any other; each has a typed builder method with the usual four overloads, and
the colours also take a `Color`.

```xml
<presto:FilteredImage Source="/Assets/Photos/port-blanc.jpg" Grayscale="1" Stretch="UniformToFill" />
```

```csharp
Motion.SetHover(photo, Recipes.Colorize());       // to full colour while hovered, back to grayscale after
photo.Animate(b => b.Sepia(from: 0.8).Brightness(from: 1.6).Duration(900));
photo.Animate(b => b.HueRotate(360).Duration(4000).Loop());
photo.Animate(b => b.Duotone(1).DuotoneShadow(Colors.Navy).DuotoneHighlight(Colors.Gold));
```

| Property | Identity | Range | CSS |
|---|---|---|---|
| `Brightness` | 1 | 0 (black) and up | `brightness()` |
| `Contrast` | 1 | 0 (flat gray) and up | `contrast()` |
| `Saturate` | 1 | 0 (gray) and up; above 1 oversaturates | `saturate()` |
| `Grayscale` | 0 | 0–1 | `grayscale()` |
| `Sepia` | 0 | 0–1 | `sepia()` |
| `HueRotate` | 0 | degrees | `hue-rotate()` |
| `Invert` | 0 | 0–1 | `invert()` |
| `Tint`, with `TintColor` (white) | 0 | 0–1: maps luminance onto `TintColor`, black staying black | none |
| `Duotone`, with `DuotoneShadow` (black) and `DuotoneHighlight` (white) | 0 | 0–1: maps luminance from the shadow to the highlight colour | none |

The filters compose into one colour matrix in a fixed order, Brightness → Contrast → Saturate → Grayscale → Sepia →
HueRotate → Tint → Duotone → Invert, rather than CSS's per-declaration order, so each can animate on its own. With every
filter at its identity the image draws plain, with no offscreen layer. A source that is not a bitmap (a `DrawingImage`)
is rasterized once per size to be filtered, and a renderer without Skia draws the image unfiltered. Filters run on the
UI thread, not the compositor: `AnimateOnCompositor` throws `NotSupportedException` for them.

#### Pixel transitions

Three pixel effects sit beside the filters on `PrestoImageBase`, built from Skia's own noise, displacement and matrix
filters rather than shaders. They are styled properties with typed builder methods like the filters, so each can reveal
or hide a single image on its own:

| Property | Identity | Tuning | Effect |
|---|---|---|---|
| `Dissolve` | 0 | `DissolveScale` (noise size in DIPs, 24), `DissolveSoftness` (edge width, 0.1) | 0 to 1: the image breaks up into noise until it is gone |
| `Pixelate` | 1 | | block size in DIPs from the image's top-left, each block one sampled pixel |
| `Ripple` | 0 | `RippleScale` (noise size in DIPs, 32) | displacement in DIPs over noise; the image never draws past its own rect |

`Seed` (an `int`) picks the noise for both `Dissolve` and `Ripple`, so the same seed draws the same pattern every frame
and every run. The effects apply after the colour filters, in the order Ripple → Pixelate → Dissolve, so the dissolve
mask is never rippled or pixelated; at identity the image still draws plain.

```xml
<presto:FilteredImage Source="/Assets/Photos/mongolia.jpg" Seed="7"
                      presto:Presence.Enter="{x:Static local:Motions.DissolveIn}" />
```

```csharp
public static readonly AnimationBuilder DissolveIn = Presto.Build().Dissolve(from: 1, to: 0).Duration(900).Ease(Eases.OutQuad);
photo.Animate(b => b.Pixelate(to: 32, ease: Eases.InQuad).Opacity(to: 0, delay: 300, duration: 400).Duration(700));
photo.Animate(b => b.Ripple(12).RippleScale(48).Duration(1200).Alternate().Loop());
```

`FilteredImage` also swaps its `Source` through a transition. Set `SourceTransition` and every `Source` change plays one
from the old image, including changes through a binding; without it a change snaps, as `Image` does. The XAML string
form is a kind and an optional duration in ms (`"Dissolve"`, `"Wipe 800"`); the element form takes the tuning. In code,
`TransitionTo` sets `Source` at once and returns the `Animation`, so it can be awaited, put in a timeline or reverted,
which snaps to the new source.

```xml
<presto:FilteredImage Source="{Binding Photo}" SourceTransition="Dissolve 600" />
<presto:FilteredImage Source="{Binding Photo}">
    <presto:FilteredImage.SourceTransition>
        <presto:ImageTransition Kind="Wipe" Angle="45" Feather="0.3" />
    </presto:FilteredImage.SourceTransition>
</presto:FilteredImage>
```

```csharp
await hero.TransitionTo(next, ImageTransitionKind.Dissolve);
hero.TransitionTo(next, ImageTransitionKind.Pixelate, b => b.Duration(900).Ease(Eases.InOutCubic));
hero.SourceTransition = new ImageTransition { Kind = ImageTransitionKind.Ripple, PeakRipple = 30, Builder = Presto.Build().Duration(1000) };
```

| `ImageTransitionKind` | What plays | Tuning on `ImageTransition` |
|---|---|---|
| `Crossfade` (default) | the old image fades out as the new one fades in | |
| `Dissolve` | the new image appears through seeded noise over the old one | the image's `Seed`, `DissolveScale`, `DissolveSoftness` |
| `Pixelate` | the old image breaks into blocks that grow to a peak mid-way, then shrink away on the new image | `PeakPixelate` (24) |
| `Ripple` | a crossfade under a displacement that peaks mid-way | `PeakRipple` (20), the image's `RippleScale` |
| `Wipe` | the new image is revealed behind a soft edge sweeping across the old one, as `MaskSweep` | `Angle` (0, left to right), `Feather` (0.2) |

`Builder` gives the timing; without one a transition runs 600 ms on `Eases.InOutQuad`. The colour filters apply to both
images, and each keeps its own `Stretch` placement. Changing `Source` while a transition is running starts the next one
from what is on screen: the current composite is captured and becomes the outgoing image, so nothing jumps. The
outgoing image is released when the transition completes or is reverted, and detaching mid-transition completes it.

A `Presence` exit cannot dissolve or pixelate an image: the exit ghost is a plain `Image` snapshot with none of these
properties. Play the effect on the live element, await it, then remove the element, as the Image effects page does.
`Presence.Enter` has no such limit, since the entrance plays on the element itself.

### Recipes

`Recipes` is a catalogue of everyday motion. Each recipe is a factory that returns a new `AnimationBuilder`, so chaining
on one never touches another caller's copy, and it goes wherever a builder goes: `Animate`, `Presence.*`, `Motion.*`,
`Animate.OnAttached`, and `AnimateOnCompositor` where the table allows.

```csharp
card.Animate(Recipes.FadeUp());
Recipes.SlideIn(Edge.Left, stagger: true).Animate(panel.Children);
using var hover = card.WhileHover(Recipes.Lift());
badge.Animate(Recipes.Shake(intensity: 1.5));
var spinner = icon.Animate(Recipes.Spinner());     // loops until cancelled
```

Every recipe also takes `duration`, `delay` and `ease`, defaulting to the [tokens](#motion-tokens), and all but the
interactions and `Dots` take `stagger: true`, which starts each element of a batch `Tokens.StaggerStep` ms after the one
before, from `delay`. The families are classes in `Monaco.Presto.AvaloniaBridge.Prebuilt`; `Recipes` forwards to them,
so `using Monaco.Presto.AvaloniaBridge;` is enough.

Entrances state only where the element starts (`.Opacity(from: 0)`), so they land on its own resting values: a card at
`Opacity` 0.6 fades up to 0.6. Every entrance and exit fades as well as moving. Exits, attention, loops and interactions
use to-values; attention recipes end back at identity, and loops and loaders run until cancelled.

| Family | Recipe | Own arguments | Compositor | Reduced form |
|---|---|---|---|---|
| `Entrances` | `Fade` | | yes | fade in over `Short` |
| | `FadeUp`, `FadeDown`, `FadeLeft`, `FadeRight` | `distance` | yes | 〃 |
| | `ScaleIn`, `PopIn` (overshoots) | `scale` | yes | 〃 |
| | `BlurIn` | `radius` | no | 〃 |
| | `SlideIn` | `edge`, `distance` (default 4 × `Distance`) | yes | 〃 |
| | `FlipIn` | `angle` (90°) | no | 〃 |
| | `Develop`, for an [image](#image-filters): settles from `Sepia` 0.8, `Brightness` 1.6 and a `BlurRadius` blur over `Long` | | no | 〃 |
| `Exits` | `FadeOut`, `FadeOutUp`, `FadeOutDown`, `FadeOutLeft`, `FadeOutRight`, `ScaleOut`, `PopOut`, `BlurOut`, `SlideOut`, `FlipOut` | as the entrance; `FlipOut`'s `angle` is −90° | all but `BlurOut` and `FlipOut` | fade out over `Short` |
| `Attention` | `Shake`, `Pulse`, `Wobble`, `Jelly`, `Heartbeat`, `Tada`, `Bounce` | `intensity` | yes | an Opacity blink, 1 → 0.6 → 1 over `Short` |
| | `FlashTint`, for an [image](#image-filters): `Tint` up to `intensity` and back | `color` (`DodgerBlue`), `intensity` | no (filter) | 〃 |
| `Loops` | `Float` | `distance` | yes | static: nothing moves |
| | `Spin` | | yes | 〃 |
| | `Breathe`, `Blink` | `intensity` | yes | 〃 |
| `Interactions` | `Lift` | `distance`, `intensity` | no (shadow) | the shadow alone |
| | `Grow` | `intensity` | yes | nothing |
| | `PressIn` | `intensity` | yes | Opacity to 0.8 |
| | `Glow` | `color` (`DodgerBlue`), `intensity` | no (shadow) | unchanged |
| | `Dim` | `intensity` | yes | unchanged |
| | `Colorize`, for an [image](#image-filters) at rest in grayscale: `Grayscale` 0, `Saturate` 1 | | no (filter) | unchanged |
| `Loading` | `Shimmer` (a `MaskSweep` skeleton) | | no (mask) | a slow Opacity pulse |
| | `Spinner` | | yes | 〃 |
| | `Dots` (staggers a panel's children itself) | `distance` | yes | 〃, still staggered |
| | `Indeterminate` | `distance`, the track width (required) | yes | 〃 |
| `Sprites` | `PlayFrames`, for a [`SpriteSheet` or `ImageSequence`](#sprite-sheets-and-image-sequences) | `fps` (24), `loop`, `reverse` | no | frame 0, still |
| `TextReveals` | `LineRise`, `LineMask`, `LineWipe`, `WordFade`, `WordBlur`, `CharCascade`, for a [text reveal](#text-reveals) | `lineStep` (3 × `StaggerStep`), `pieceStep`; no `stagger` | no | whole block fades, no split |
| `Layouts` | `Reflow`, for [`Animate.Layout`](#layout-animation-from-xaml), `AnimateLayout` and [`Animate.ContentSize`](#content-size) | | layout only | an instant move |
| `Pages` ([`IPageTransition`](#page-transitions)) | `PageFade`, `PageFadeThrough` | `duration` only | yes | a crossfade over `Short` |
| | `PageSharedAxis` | `axis` (`X`, `Y`, `Z`), `distance` | yes | 〃 |
| | `PageSlide`, `PagePush` | `edge` only | yes | 〃 |

The interactions are meant for [`Motion` states](#state-motion); they take no `stagger`, since a state belongs to one
element.

#### Motion tokens

`Recipes.Tokens` is a `MotionTokens` record read on every call, so replacing it restyles later calls but not builders
already made. Entrances last `Medium` (`Develop` `Long`), exits and interactions `Short`, attention `Long`, and loops and loaders twice `Long`;
pops and flips use `EmphasizedEase`.

```csharp
Recipes.Tokens = MotionTokens.Expressive;
Recipes.Tokens = MotionTokens.Standard with { Distance = 32, StaggerStep = 40 };
```

| Preset | `Short` / `Medium` / `Long` | `Ease` / `EmphasizedEase` | `Distance` | `Scale` | `BlurRadius` | `StaggerStep` |
|---|---|---|---|---|---|---|
| `Standard` (default) | 200 / 450 / 650 | `OutCubic` / `OutBack()` | 24 | 0.9 | 12 | 50 |
| `Expressive` | 450 / 900 / 1200 | `OutBack(2.2)` / `OutElastic(1, .5)` | 64 | 0.6 | 24 | 90 |
| `Reduced` | 120 / 150 / 200 | `None` / `None` | 0 | 1 | 0 | 0 |

The `Reduced` preset tones motion down by its numbers alone; it is not the [reduced-motion](#reduced-motion) flag.

#### Recipes from XAML

Each markup extension provides a new builder per element from its recipe:

```xml
<Border presto:Presence.Initial="{presto:Fade Direction=Up, Stagger=60}"
        presto:Presence.Exit="{presto:Fade Direction=Up}"
        presto:Motion.Hover="{presto:Lift}"
        presto:Motion.Press="{presto:PressIn}" />

<Style Selector="Border.toast">
    <Setter Property="presto:Presence.Exit" Value="{presto:Slide Edge=Right, Out=True}" />
</Style>
```

| Extension | Recipe | Own properties | Exit form |
|---|---|---|---|
| `{presto:Fade}` | `Fade`, `FadeUp`… | `Direction` (`None`, `Up`, `Down`, `Left`, `Right`), `Distance` | `FadeOut`, `FadeOutUp`… |
| `{presto:Scale}` | `ScaleIn` | `From` | `ScaleOut` |
| `{presto:Pop}` | `PopIn` | `Scale` | `PopOut` |
| `{presto:Blur}` | `BlurIn` | `Radius` | `BlurOut` |
| `{presto:Slide}` | `SlideIn` | `Edge`, `Distance` | `SlideOut` |
| `{presto:Shake}` | `Shake` | `Intensity` | |
| `{presto:Lift}`, `{presto:PressIn}`, `{presto:Glow}` | the same | `Intensity` | |
| `{presto:Shimmer}` | `Shimmer` | | |
| `{presto:PlayFrames}` | `PlayFrames` | `Fps`, `Loop`, `Reverse` | |
| `{presto:LineRise}`, `{presto:LineMask}`, `{presto:LineWipe}`, `{presto:WordFade}`, `{presto:WordBlur}`, `{presto:CharCascade}` | the same | `LineStep`, `PieceStep` | |
| `{presto:Reflow}` | `Reflow` | `Duration` also positional: `{presto:Reflow 320}`; no `Stagger` | |
| `{presto:Reorder}` | `ReorderLift` | `Intensity` | |

All of them take `Duration`, `Delay`, `Ease` (a string such as `outBack`), `Stagger` (the step in ms) and `Out`. Set
on `Presence.Exit`, an extension with an exit form provides it. A style `Setter` is not the property it sets, so there
`Out=True` asks for the exit form; `Out=False` forces the entrance form anywhere. Arguments are separated by commas.

Set on a `TextBlock`, the text reveal extensions also set the `TextReveal.Pieces` (and for `LineMask`, `Mask`) their
recipe needs, unless the block sets them itself. From a style `Setter` they cannot reach the block, so set
`TextReveal.Pieces` there too.

#### Page transitions

`PrestoPageTransition` is an Avalonia `IPageTransition` (and `IProgressPageTransition`) for any host that takes one:
`TransitioningContentControl`, `Carousel`, `NavigationPage`, `TabControl`. It plays `Enter` on the incoming page and
`Exit` on the outgoing one, going back plays the pair reversed unless `BackEnter`/`BackExit` are set, and it runs on the
render thread whenever every property allows (`UseCompositor`). The page recipes return one ready-made:

```xml
<TransitioningContentControl Content="{Binding CurrentPage}" ClipToBounds="True"
                             IsTransitionReversed="{Binding IsBack}"
                             PageTransition="{presto:PagePush Edge=Right}" />

<TransitioningContentControl.PageTransition>
    <presto:PrestoPageTransition Enter="{presto:Fade Direction=Up, Delay=90}" Exit="{presto:Fade Duration=90}" />
</TransitioningContentControl.PageTransition>
```

```csharp
host.PageTransition = Recipes.PageSharedAxis(PageAxis.X);
host.PageTransition = new PrestoPageTransition { Enter = Recipes.SlideIn(Edge.Right), Exit = Recipes.FadeOut() };
```

The extensions are `{presto:PageFade}` and `{presto:PageFadeThrough}` (`Duration`), `{presto:PageSharedAxis}` (`Axis`,
`Distance`), and `{presto:PageSlide}` and `{presto:PagePush}` (`Edge`). A recipe extension set on `Exit` or `BackExit`
provides its exit form without `Out=True`. The slides travel the full size of the host's panel and would otherwise show
outside it, so give the host `ClipToBounds="True"`.

#### Tilt, typewriter and count-up

These need the pointer or the text, so they are not builders. `Tilt` turns an element towards the pointer, up to
`Strength` degrees (default 10) at its edges, through the `RotateX`, `RotateY` and `Perspective` shorthands, and springs
flat when the pointer leaves; detaching or disabling it reverts them. It runs on the UI thread, and nothing tilts under
reduced motion.

```xml
<Image Source="{Binding Photo}" presto:Tilt.Enabled="True" presto:Tilt.Strength="8" />
```

```csharp
Recipes.Typewriter(headline);                    // each character fades in StaggerStep ms after the last
Recipes.CountUp(total, 1280, format: "C0");      // from the number shown, or 0, over Long
Recipes.RollDigits(score, 1290);                 // odometer: only the changed digits roll, right to left
```

Both play at once and return the `Animation`. `Typewriter(textBlock, charDelay, …)` splits a laid-out `TextBlock` into
characters and puts it back together at the end; reduced, the whole block fades in. `CharCascade` is its trigger form,
which waits for layout. `CountUp(textBlock, to, format, …)`
formats each value in the current culture (default `"N0"`); reduced, the text is set to the end value on the first frame.
`RollDigits(textBlock, to, format, …)`, after SwiftUI's `numericText`, formats the same way and lines the old and new
text up from the right. Each changed character rolls out upwards under a clip as its replacement rolls in from below,
or the other way when counting down, staggered right to left by `StaggerStep`. The plain text comes back at the end,
and calling it again mid-roll finishes the running roll first. Reduced, it sets the text as `CountUp` does.

#### Confetti, sparkles and bursts

Particle recipes emit into one render-thread overlay per window, laid over the anchor in the window's `OverlayLayer`,
ignoring hit tests, and removed once it has drawn nothing for a second:

```csharp
await Recipes.Confetti(submit);                           // 120 rects and streaks from the button's centre
Recipes.Confetti(submit, from: Edge.Bottom);              // or launched along a window edge
var sparkles = Recipes.Sparkles(badge, rate: 30);         // twinkles over its bounds until stopped
sparkles.Stop();                                          // stop emitting; live particles finish
Recipes.Burst(like, count: 24, color: Colors.HotPink);    // a ring of circles for a press or a like
```

Each returns a `ParticleEffect`: `Stop`, `Cancel` (clears at once; also `Revert`), `Pause`/`Resume` for its own
emitters, and `Then()`/`await`, which completes on the UI thread when the last particle dies. Emitted particles cannot be
sought. Under reduced motion nothing is emitted, `reduced` plays on the anchor instead (default `Recipes.Pulse()`, an
opacity blink when reduced) and the effect is complete at once.

#### Sprite sheets and image sequences

`SpriteSheet` shows one cell of a grid `Source` (`Columns`, `Rows`, and `FrameCount` when the last row is partial), and
`ImageSequence` one image of `Frames`. Both show the frame their `double` `Frame` selects, and `Play` tweens it linearly
from 0 to the frame count, so every frame gets 1000 / fps ms. Both derive from `PrestoImageBase`, so they inherit the
[image filters](#image-filters):

```csharp
var run = sheet.Play(fps: 12);                            // loops; Play(fps, loop: false, reverse: true) plays once backwards
```

```xml
<presto:SpriteSheet Source="/Assets/Sprites/run.png" Columns="8" Rows="2"
                    presto:Animate.OnAttached="{presto:PlayFrames Fps=12}" />
```

`Recipes.PlayFrames(fps, loop, reverse)` is the same as a builder, reading each target's frame count, and
`{presto:PlayFrames}` adds `Fps`, `Loop` and `Reverse`; an explicit `Duration` or `Ease` replaces the frame timing.
Reduced, it holds frame 0.

### Reduced motion

Reduced motion is a flag the app sets, and each builder says what it becomes under it:

```csharp
Presto.Defaults.ReducedMotion = settings.ReduceMotion;

var intro = Presto.Build().Y(from: 80).Scale(from: .8).Opacity(from: 0).Duration(900).Ease(Eases.OutBack())
    .WhenReduced(b => b.Opacity(from: 0).Duration(150));
```

While `PrestoDefaults.ReducedMotion` is set, `ToParams()` builds the `WhenReduced` alternative instead: a new builder
with this one's settings (duration, delay, ease, loop, callbacks), including those chained after `WhenReduced`, but none
of its properties or keyframes. So `Recipes.FadeUp().Delay(Presto.Stagger(60))` keeps its stagger when reduced. The
flag is read when the builder becomes params, so a running animation is left alone; a `Motion` state picks up a change at
its next transition. Every recipe supplies an alternative (the table above).

`PrestoDefaults.ReduceAll` handles everything without one: each other animation, from a builder or from params, becomes
an instant, one-iteration set of its end values, keyframes and springs included, and so do timeline children. Plain
timers keep their durations, and animations built from a `WhenReduced` alternative play as written. Set both to reduce
the whole app.

Both flags are ordinary defaults, so each engine has its own and a `Scope` copies them. Presto never sets them on its
own. To follow the OS "reduce motion" setting (Motion's `reducedMotion="user"`), opt in:

```csharp
var following = PrestoAvalonia.FollowSystemReducedMotion(changed: reduced => Log($"reduce motion: {reduced}"));
// ...
following.Dispose();   // stops following and leaves the flag as it is
```

It sets `ReducedMotion` on the current engine straight away, and again whenever a window or the app is activated, the
browser reports a change, or you call `PrestoAvalonia.RefreshSystemReducedMotion()`. The built-in probe reads
Windows (client-area animation), macOS and iOS (reduce motion) and the browser (`prefers-reduced-motion`). On Android,
Linux and elsewhere it reads unknown, which leaves the flag alone. There, pass your own `probe: () => …` and call
`RefreshSystemReducedMotion` when it changes.

To show the OS setting without following it, as a settings screen offering "full", "reduced" and "OS setting" does,
`PrestoAvalonia.WatchSystemReducedMotion(reduced => …)` reports it now and on each change at the same moments, with
`null` where it cannot be read, and never touches the flag.

## anime.js → Presto

Every entry point of anime.js 4.5.0's `src/index.js`:

| anime.js | Presto |
|---|---|
| `animate(targets, params)` | `Presto.Animate(targets, new AnimationParams { … })`, or `control.Animate(new() { … })`<br>Builder: `Presto.Build().X(…).Duration(…).Animate(targets)`, or `control.Animate(b => b.X(…))` |
| `createTimer(params)` | `Presto.CreateTimer(new TimerParams { … })` |
| `createTimeline(params)` | `Presto.CreateTimeline(new TimelineParams { … })`, then `.Add` / `.Set` / `.Sync` / `.Label` / `.Call` / `.Remove` / `.Refresh`<br>Builder: `.Add(targets, Presto.Build()…, position)` / `.Set(targets, Presto.Build()…)`, or `.Add(targets, b => …, position)` |
| `createAnimatable(targets, params)` | `Presto.CreateAnimatable(targets, new AnimatableParams { … })`, or `control.CreateAnimatable(new() { … })` |
| `createScope(params)` | `Presto.CreateScope(new ScopeParams { … })`; `PrestoAvalonia.CreateScope(control)` reverts when the control detaches |
| `createDraggable(target, params)` | `PrestoAvalonia.CreateDraggable(control, new DraggableParams { … })` |
| `onScroll(params)` | `PrestoAvalonia.OnScroll(new ScrollObserverParams { … })`, over a `ScrollViewer` instead of the window; `Pin`, `Snap` and `Scroll.Speed` / `Scroll.Smooth` are Presto's own (GSAP-shaped), upstream has none |
| `svg.createDrawable(shape)` | `PrestoAvalonia.CreateDrawable(shape, start, end)`, animated through `["Draw"] = "0 1"` |
| `svg.createMotionPath(path)` | `PrestoAvalonia.CreateMotionPath(path, offset, autoRotate)` → the `X` / `Y` / `Rotate` tweens; `CreateMotionPath(points, curviness, …)` through a point list is GSAP-shaped, upstream has none |
| `svg.morphTo(path, precision)` | `PrestoAvalonia.MorphTo(geometry, precision)`, assigned to a `Path`'s `["Data"]` |
| `text.split(target, params)` / `splitText` | `PrestoAvalonia.SplitText(textBlock, new SplitTextParams { … })` → `TextSplitter` with `Lines` / `Words` / `Chars` |
| `text.scramble(params)` / `scrambleText` | `PrestoAvalonia.ScrambleText(new ScrambleTextParams { … })`, assigned to a `["Text"]` property |
| `stagger(value, params)` | `Presto.Stagger(value, new StaggerParams { … })`; `PrestoAvalonia.Stagger(…)` measures the auto grid from the layout |
| `utils.*` | `Utils.*` in PascalCase; the value-less overloads return a chainable `ChainedUtil` (`Utils.MapRange(0, 100, 0, 1000).Clamp(0, 100)`), usable wherever a `Func<double, double>` modifier is; `utils.get` / `set` / `remove` are `Presto.Get` / `Set` / `Remove`, with `PrestoAvalonia` overloads that take an `AvaloniaProperty`; `padStart` / `padEnd` are not ported (`string.PadLeft` / `PadRight` already do this) |
| `eases` / `easings` | `Eases.*`, the `Ease` delegate (`double → double`), `Spring`, `EaseParser.Parse("inOutQuint")` |
| `engine` | `Presto.Engine` (`Speed`, `Fps`, `Precision`, `TimeUnit`, `Pause()`, `Resume()`, `FrameSource`) |
| `globals` / per-call defaults | `Presto.Defaults`, or `ScopeParams.Defaults` / `TimelineParams.Defaults` for a subtree |
| `registerAdapter` (`adapters/`) | `Presto.Adapters` (an `ITargetAdapter` list) and `Presto.Codecs` (per property type). `adapters/three.js` itself is **not ported** — the interfaces are there if you want to write one. |
| `waapi.*` | Partly, and not as an API port — there is no Web Animations API here. `waapi.animate` is `PrestoAvalonia.AnimateOnCompositor(targets, new CompositorAnimationParams { … })`, or `control.AnimateOnCompositor(new() { … })` / `(Presto.Build()…)`: the reduced model upstream has too, compiled into composition key frames on the render thread; see [Render thread](#render-thread) for the properties it takes and what it leaves out. `CompositorCanvas` / `CompositorScene`, which run a whole engine on the render thread, are Presto's own — upstream has no counterpart. |
| `createLayout` / `AutoLayout` (`layout/`) | Partly. `PrestoAvalonia.AnimateLayout(control, duration, ease, mode)`, or `control.AnimateLayout(…)`, eases a control into its new place whenever layout moves it, as FLIP inverted onto the composition transform and played on the render thread; `AnimateSharedElement(source, target, …)` moves one between containers. From XAML, `Animate.Layout` arms it on a control and `Animate.ItemLayout` on every container of a list, for as long as each is attached; `LayoutAnimateMode.PositionAndSize` scales a resize and `PositionAndSizeCorrected` counter-scales the direct children. Upstream's real width/height animation is `Animate.ContentSize` / `AnimateContentSize(host, …)`, which eases the layout size on the UI thread. See [Render thread](#render-thread), [Layout animation from XAML](#layout-animation-from-xaml) and [Content size](#content-size). Upstream's enter and leave states (`enterFrom` / `leaveTo` / `swapAt`) are not layout moves here: entering is `Presence.Arrive`, `Initial` or `Enter`, and leaving is `Presence.Exit` (see [Lifecycle animations](#lifecycle-animations)). |
| `utils.$` (`registerTargets`) | **Not ported.** There are no selector strings: pass an object, an `IEnumerable`, or an Avalonia `Controls` collection straight to `Animate`. |
| `utils.cleanInlineStyles` | **Not ported.** The bridge writes at `BindingPriority.Animation`, so `Revert()` clears the animated values and the control's own values reappear; there is nothing inline to clean. |
| `core/units.js`, `core/styles.js` | Partly. Numbers, `deg` / `rad` / `turn`, and `%` against the current value are ported; `px` / `em` / `rem`, CSS variables and DOM attribute animation are **not** — Avalonia has no CSS box model. |
| `core/*`, `types/*` | Internal (`Clock`, `Renderer`, `Composer`, value parsing) or TypeScript declarations with no C# counterpart. |
| *Framer Motion, not anime.js:* `whileHover` / `whileTap` / `whileFocus` | `Motion.Hover` / `Motion.Press` / `Motion.Focus`, or `control.WhileHover` / `WhilePressed` / `WhileFocused` in code; see [State motion](#state-motion) |
| *Framer Motion, not anime.js:* `variants` with `animate="name"` | `Motion.States` entries whose `When` names a state, chosen by binding `Motion.State`; `control.While(when, …)` in code |
| *Framer Motion, not anime.js:* `inertia` transition; GSAP `InertiaPlugin` | `Inertia` as a to-value, `Animatable.Fling`, `DraggableParams.ReleaseInertia`; see [Inertia](#inertia) |
| *Framer Motion, not anime.js:* `useVelocity`, velocity tracking | `VelocityTracker` (`Add(value, time)`, `Velocity`, `Reset()`) |
| *Not anime.js:* pan, pinch and rotate, the `usePinch` / `useGesture` hooks Framer Motion apps pair with | `control.CreateTransformable(new TransformableParams { … })`; see [Transformable](#transformable) |
| *CSS, not anime.js:* `filter: blur()` | `Blur`; see [Effects](#effects) |
| *CSS, not anime.js:* `filter: 'grayscale(1)'` (and `brightness`, `contrast`, `saturate`, `sepia`, `hue-rotate`, `invert`) | `Grayscale` and the others on a `FilteredImage`: `b.Grayscale(1)`; see [Image filters](#image-filters) |
| *CSS/GSAP, not anime.js:* crossfading or dissolving an `<img>` into the next (two stacked images, opacity or a noise mask) | `SourceTransition="Dissolve"` on a `FilteredImage`, or `image.TransitionTo(next, ImageTransitionKind.Dissolve)`; see [Pixel transitions](#pixel-transitions) |
| *CSS, not anime.js:* `image-rendering: pixelated` on a scaled-down copy; an SVG `feTurbulence` + `feDisplacementMap` ripple | `Pixelate`, `Ripple` on a `FilteredImage`, or the `Pixelate` and `Ripple` transition kinds |
| *CSS, not anime.js:* `filter: drop-shadow()` | `ShadowX` / `ShadowY` / `ShadowBlur` / `ShadowColor` / `ShadowOpacity`, or `.Shadow(x, y, blur, color)`; `box-shadow` is `BoxShadow` on a `Border` |
| *CSS, not anime.js:* `rotateX()` / `rotateY()`, `perspective()` | `RotateX` / `RotateY`, `Perspective` |
| *CSS, not anime.js:* `clip-path: inset()` / `circle()` | `ClipInset`, `ClipCircle` with `ClipCircleCenter` |
| *CSS, not anime.js:* a `mask-image` linear-gradient sweep | `MaskSweep` with `MaskSweepAngle` and `MaskSweepFeather` |
| *Framer Motion, not anime.js:* `useReducedMotion()`, `<MotionConfig reducedMotion="always">` | `Presto.Defaults.ReducedMotion`, with `WhenReduced` for each builder's alternative and `ReduceAll` for the rest; for `reducedMotion="user"`, `PrestoAvalonia.FollowSystemReducedMotion()`. See [Reduced motion](#reduced-motion) |

The XAML surface is the `Presence` lifecycle properties for entrances, scroll reveals and exits ([Lifecycle animations](#lifecycle-animations)), `Animate.OnAttached` for loops and timers while a visual is attached ([From XAML](#from-xaml)), `Connected`'s keys,
the `Motion` states and the `Animate.OnTapped` / `OnChanged` one-shots ([State motion](#state-motion)), and the recipe markup extensions and `Tilt` ([Recipes](#recipes)); other triggers belong in code-behind or a view model.

### Parameter names

anime.js parameter objects become `init`-only records with the same names in PascalCase.

| anime.js | Presto |
|---|---|
| `duration` | `Duration` |
| `delay` | `Delay` |
| `loop` | `Loop` (`LoopCount`: `true` or a count) |
| `loopDelay` | `LoopDelay` |
| `alternate` | `Alternate` |
| `reversed` | `Reversed` |
| `autoplay` | `Autoplay` |
| `frameRate` | `FrameRate` |
| `playbackRate` | `PlaybackRate` |
| `ease` | `Ease` |
| `playbackEase` | `PlaybackEase` |
| `composition` | `Composition` (`Replace` / `None` / `Blend`) |
| `modifier` | `Modifier` |
| `keyframes` | `Keyframes` |
| `id` | `Id` |
| `from`, `to` | `TweenParams.From`, `TweenParams.To` |
| `onBegin`, `onBeforeUpdate`, `onUpdate`, `onLoop`, `onPause`, `onComplete`, `onRender` | `OnBegin`, `OnBeforeUpdate`, `OnUpdate`, `OnLoop`, `OnPause`, `OnComplete`, `OnRender` |
| stagger `from`, `reversed`, `ease`, `grid`, `axis`, `start`, `total`, `use`, `modifier`, `jitter` | `StaggerParams.From`, `.Reversed`, `.Ease`, `.Grid`, `.Axis`, `.Start`, `.Total`, `.Use`, `.Modifier`, `.Jitter` |
| spring `mass`, `stiffness`, `damping`, `velocity`, `bounce`, `duration` | `Eases.Spring(mass:, stiffness:, damping:, velocity:, bounce:, duration:)` |
| `persist` | Not ported — it only exists on anime.js's WAAPI path. |

## C# differences

A handful of places where the JS shape does not survive the type system:

- **Properties go through an indexer**, which is the only C# syntax that mixes named settings and arbitrary keys in one
  initializer: `new() { ["X"] = 320, Duration = 1250 }`. The bridge adds a typed `AvaloniaProperty` indexer, so
  `[Visual.OpacityProperty] = 0.4` sits alongside `["Opacity"] = 0.4`.
- **A lambda literal needs its delegate type named.** C# does not apply user-defined implicit conversions to lambdas, so
  write `Ease = (Ease)(t => t)` and `["X"] = new FunctionValue((target, i, total) => i * 10)`. Values that already carry
  their type (`Eases.OutQuad`, `Presto.Stagger(…)`, `Utils.Snap(40)`) convert on their own.
- **A positioned timer child names its type.** `Add(targets, parameters, position)` and `Add(parameters, position)` are
  ambiguous when the first argument is a target-typed `new() { … }`, so write
  `tl.Add(new TimerParams { Duration = 400 }, 20)`.
- **`Loop` is a `LoopCount`**, with implicit conversions from `bool` and `int`: `Loop = true` is infinite, `Loop = 2` runs
  three iterations, and `LoopCount.Infinite` is the explicit form.
- **`.then()` is `Then()` or `await`.** `await animation` works directly and yields the animation itself; `Then(callback)`
  returns the `Task` for parity with upstream.

## Platforms and status

Version 0.1.0, pre-release: the API is settled enough to build against but not frozen, and a pass over it is planned
before 1.0. Behavioural parity with anime.js 4.5.0 is covered by the upstream suites ported into
`tests/Monaco.Presto.Tests` and `tests/Monaco.Presto.AvaloniaBridge.Tests`.

The core is pure .NET and runs anywhere net10.0 does, and the bridge uses no desktop-only API. All four sample heads
build; what has actually been *run* differs, and the table says so rather than implying more.

| Head | Verified |
|------|----------|
| **Desktop** — `net10.0`, Windows/macOS/Linux | Runs. The development target: every gallery page is driven here, and it is what the bridge is written against. |
| **Browser** — `net10.0-browser`, WebAssembly | Runs, with one caveat (below): the 15 gallery pages that predate the compositor one were stepped through in Chrome — the timer tracks wall-clock time, the draggable cards take pointer capture, the scroll observer follows the `ScrollViewer` offsets, the animatable grid follows the cursor, and split/scramble text, motion paths and stroke drawing all render. The `Compositor` page has not been driven there. |
| **Android** — `net10.0-android` | Builds, installs and launches on a device (arm64, Android 16): the activity comes up with no startup exception. The gallery has not been driven on a device, so touch input and per-page rendering are unverified. |
| **iOS** — `net10.0-ios` | Compiles for `iossimulator-x64` only. Signing, packaging and running need a macOS build host, so nothing has been run. |

Five platform notes:

- **On WebAssembly the render loop is not a thread.** `BrowserRenderTimer` drives the compositor from the single WASM
  thread that also runs the UI, so a `CompositorScene` runs and draws there but does **not** survive a blocked UI
  thread — the busy-indicator guarantee is a desktop and mobile one. The same goes for any platform that opts into
  `ShouldRenderOnUIThread` (a Win32 and X11 option). Everywhere else the render loop has a thread of its own: Windows,
  macOS, Linux X11, Android (`ChoreographerTimer`) and iOS (`DisplayLinkTimer`).
- **Hot reload does not work on WebAssembly.** The file watcher cannot start there, and the browser head logs that at
  startup; `.axaml` edits need a rebuild.
- **On Android the frame pump starts on a dispatcher timer.** Avalonia's Android lifetime exposes only a factory for its
  main view, so the bridge cannot ask it for a `TopLevel`; it uses the first visual it animates instead and moves onto
  that top level's frame callback. Until then a 60 Hz render-priority timer pumps, so animations run either way.
- **Effects render through Skia on every head.** `Blur` and the shadow shorthands cost an offscreen pass wherever they
  run. What a blur costs in the Browser head has not been measured yet; it is to be measured in the smoke run, not
  assumed.
- **A trackpad pinch reaches `Transformable` differently on each platform.** On Windows and in the Browser it arrives
  as Ctrl+wheel, which the default `WheelZoom` handles. On macOS, `Avalonia.Native` raises magnify and rotate gestures,
  which zoom and (with `Rotate` on) turn about the pointer. X11 raises no touchpad gestures, so a trackpad pinch on Linux
  is unsupported; Ctrl+wheel still zooms there.

## Running the sample and the tests

The sample gallery has 17 pages — one per anime.js documentation area, plus the render-thread and layout ones — and doubles as the
manual acceptance surface.

```powershell
dotnet build Monaco.Presto.slnx
dotnet run --project samples/PrestoSample.Desktop
dotnet watch --project samples/PrestoSample.Desktop        # hot reload

dotnet test --project tests/Monaco.Presto.Tests            # note --project, not a bare path
dotnet test --project tests/Monaco.Presto.AvaloniaBridge.Tests
dotnet test --solution Monaco.Presto.slnx
```

Tests use xunit v3 on Microsoft.Testing.Platform, which the .NET 10 SDK requires — hence `--project`.

The implementation plan, architecture notes and the phase checklist live in [docs/PLAN.md](docs/PLAN.md).

## Credits

Presto is a port of **[anime.js](https://github.com/juliangarnier/anime)** by Julian Garnier, used under the MIT
licence. The timing model, the parameter vocabulary and most of the test suites come from anime.js 4.5.0; the bugs are
mine. If you write JavaScript, use the original — its
[documentation](https://animejs.com/documentation) is also the best reference for this library.

## Licence

Presto is MIT licensed — see [LICENSE](LICENSE), which also carries anime.js's own MIT notice.
