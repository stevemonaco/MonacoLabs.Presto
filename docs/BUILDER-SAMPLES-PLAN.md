# Monaco.Presto — Builder samples plan

A "Builder" section in the sample gallery that shows off the fluent builder ([FLUENT-BUILDER-PLAN.md](FLUENT-BUILDER-PLAN.md)),
every sample printing the builder code it runs, plus the smallest XAML surface that lets a builder be attached from
markup. It is PLAN.md Phase 15.

## 1. Findings

A spike (a headless Avalonia 12.1.2 app referencing the bridge, since deleted) checked each question before the plan
committed to an answer.

**Showing the code.** The Loaders page prints each call from hand-copied `const string`s that "must match those calls"
and can drift silently. `[CallerArgumentExpression]` removes the drift: a helper that takes the builder expression as an
argument receives its exact source text at compile time, line breaks and comments included. Continuation lines keep
their source indentation, so the helper strips the common leading whitespace of lines 2+. The code shown is then, by
construction, the code that ran.

**Attaching from XAML.** All three candidate spellings compile under compiled bindings and animate at run time when
the value lands in an attached property typed `AnimationBuilder`:

| Spelling | XAML | What it is good for |
|---|---|---|
| Static recipe | `presto:Animate.OnAttached="{x:Static local:Motions.FadeUp}"` | A fixed, named animation reused across views. Safe to share because every terminal snapshots the builder (FLUENT-BUILDER-PLAN decision 4). |
| Markup extension | `presto:Animate.OnAttached="{local:FadeUp Distance=48, Delay=100}"` | A recipe the markup parameterizes. `ProvideValue` returns a fresh builder per element. This is the "builder as a class" option, done without unsealing `AnimationBuilder`. |
| Binding | `presto:Animate.OnAttached="{Binding Emphasis}"` | View state picks the animation: the view model swaps the property and the element re-animates. The builder is core-only, so a view model needs no Avalonia reference for it (only the bridge's typed shorthands would pull one in). |

## 2. Decisions

1. **One attached property, typed `AnimationBuilder`, and all three spellings are shown.** They are not rival designs:
   the same property takes a static, a markup extension or a binding, so the sample demonstrates each with a line on
   when to pick it. No `IAnimationRecipe` interface or unsealed builder is needed.
2. **The attached property ships in the bridge, not the sample.** A XAML attach that only exists in a sample is copy-paste
   for every user, and its lifetime rules (below) need bridge tests. This settles the PLAN §7 open item "attached
   property triggers vs behaviors package" in favor of attached properties, deliberately minimal: one trigger.
3. **The trigger is attachment to the visual tree, named `Animate.OnAttached`.** Not `Loaded`, for the reason PLAN §7
   decision 8 records (`Unloaded` never fires for a root detached before its `Loaded` job runs). Other triggers
   (pointer enter, click, property change) stay open.
4. **Lifetime.**
   - Set while attached, or attached with a value set: play `target.Animate(builder)`.
   - Detached: revert the running animation, as a `ControlScope` does, so a re-attached element starts clean.
   - Value changed while attached: revert the previous animation, then play the new one. Predictable over seamless:
     a mid-flight swap restarts from the element's own values.
   - Set to `null`: revert and unsubscribe; no handlers are left on the visual.
   - `Design.IsDesignMode`: do nothing, so the Rider/VS previewer does not animate the design surface.
5. **Samples use the builder everywhere they animate.** The section exists to show the builder, so its pages do not mix
   in the indexer.

## 3. Library work (bridge)

`Monaco.Presto.AvaloniaBridge/Animate.cs`:

```csharp
public static class Animate
{
    public static readonly AttachedProperty<AnimationBuilder?> OnAttachedProperty;
    public static AnimationBuilder? GetOnAttached(Visual visual);
    public static void SetOnAttached(Visual visual, AnimationBuilder? value);
}
```

The running `Animation` and the attach/detach handlers live in one private per-visual state object, stored in a
non-public attached property, so the class handler subscribes once per visual and unsubscribes on `null`.

Tests, `tests/Monaco.Presto.AvaloniaBridge.Tests/AnimateAttachedTests.cs`:

- Setting it on an attached control starts one animation (`Presto.GetAnimations` sees it) that reaches its end value.
- Setting it before attachment starts nothing until the control is attached.
- Detaching reverts the animated property to its own value; re-attaching plays again from the start.
- Changing the value reverts the first animation (its property is back to its own value) and plays the second.
- Setting `null` reverts, and a later attach/detach starts nothing.
- One static builder on two controls gives two independent animations; canceling one leaves the other running.
- A XAML-loaded control using each of the three spellings (`x:Static`, a test markup extension, a binding) animates.
- Design mode is a no-op, if `Design.IsDesignMode` can be set from a test; otherwise note that it cannot and skip.

## 4. Sample work

### 4.1 Kit

- `samples/PrestoSample/Builder/Snippet.cs`: `Snippet.Of<T>(T value, [CallerArgumentExpression(nameof(value))] string code = "")`
  returning `(T Value, string Code)` with the continuation-line dedent. Used for builders, timelines and anything
  else a page wants to print.
- A code-block look in `Styles/Showcase.axaml`: a `Border.codeblock` (subtle panel background, padding, corner
  radius, both themes via `Palette`) around a `SelectableTextBlock.code` (the existing monospace `code` class, no wrap,
  horizontal scroll when wide). Reused by every Builder page.
- Pages go in `samples/PrestoSample/Builder/` (namespace `PrestoSample.Builder`), derive from `ShowcasePage` so their
  scope reverts them, take colors from `Palette`, and are registered in a new **Builder** section of
  `MainViewModel` after Showcase, each with a 2–3 sentence caption saying what to try.

### 4.2 Pages

Each demo is a stage, a Replay (or the page's own interaction), and the code block with the captured text beneath it.

1. **Builder basics.** The Animation basics like-button, spelled with the builder: `X`, `Y`, `Rotate(from: …)`,
   `Scale`, `Opacity`, `Background`, `Duration`, `Delay`, `Ease`, `Loop()`, `Alternate()`. It is still a real Button,
   so clicks count while it moves.
2. **Properties and keyframes.** Three stages:
   - per-property settings: `.X(from: -120, to: 120, ease: …)` beside `.Rotate(360, duration: …, delay: …)`;
   - per-property keyframes (`.Y(k => k.To(-40, duration: 300).To(0, …))`) and animation-level keyframes
     (`.Keyframes(k => k.At(0, …).At(30, …).At(100, …))`);
   - typed values and any property: `.Background(Palette color)`, `.CornerRadius(new CornerRadius(…))` and
     `.Property(Border.BorderThicknessProperty, …)` on a card.
3. **Recipes.** One `static readonly AnimationBuilder` "pop" played on whichever of a row of buttons is clicked, and
   the same recipe with `.Delay(Presto.Stagger(60))` over the whole row. A duration slider mutates the recipe: the next
   play uses the new duration while one already running keeps its own, which is the snapshot rule made visible.
4. **Timeline and compositor.** A timeline assembled from builders (`tl.Add(a, fadeUp).Add(b, fadeUp, "-=200")…`) with
   a scrub slider, and a control spun by `AnimateOnCompositor(builder)` beside a "Freeze UI thread for 2 s" button (the
   same one the Compositor page has), so it keeps turning while the UI thread is blocked.
5. **XAML.** Three panels, one per spelling in §1, each showing its XAML and the C# it points at:
   - `{x:Static}`: a `Motions` class of static recipes (captured with `Snippet.Of` in their initializers), used on a
     row of cards;
   - markup extension: a `FadeUpExtension` with `Distance` and `Delay` properties, used on a list with increasing
     `Delay`s so the items cascade in;
   - binding: a panel whose `DataContext` is a small `ObservableObject` with an `Emphasis` builder that a ToggleSwitch
     swaps between an idle float and an alert shake.

   A Replay button re-attaches the three panels, since `OnAttached` plays on attachment. The XAML snippets are
   `const string`s (XAML has no caller-argument capture), pinned by a test against the `.axaml` file.

### 4.3 Sample tests (`tests/PrestoSample.Tests`)

- `SnippetTests`: capture of a multi-line expression, dedent of continuation lines, comments preserved, single-line
  expressions unchanged.
- `BuilderPagesTests`:
  - every page's code blocks are non-empty and each shows `Presto.Build()` (or the timeline/recipe it prints);
  - Builder basics, Properties and Recipes start their animations on attach, and a Recipes play after the slider
    moves uses the new duration while the running one keeps the old;
  - the XAML page's elements each have a running animation after attach, the ToggleSwitch swaps the bound animation,
    and every XAML snippet appears in `XamlPage.axaml` (whitespace-normalized), found through `[CallerFilePath]`.
- `GalleryPageTests` already covers "runs a second and reverts on detach" for every registered page.

## 5. Docs

- README: in the Fluent builder section, a short "From XAML" subsection with the three spellings and when to use each.
- PLAN.md: tick Phase 15; move the "XAML surface" open item to Decided as a minimal `Animate.OnAttached` attached
  property, with other triggers still open.
- CLAUDE.md layout table: mention the `Builder/` folder in the samples row.

## 6. Out of scope

- Triggers beyond attachment, and a behaviors-style XAML layer.
- Converting existing reference or showcase pages to the builder.
- Builder types for timers, timelines or other params.
- Desktop visual verification is the owner's step; the tests above are the automated check.
