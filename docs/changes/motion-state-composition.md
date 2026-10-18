# Motion states compose from several sources

## Why

Two Tempo findings (BACKLOG, Tempo findings, BRIDGE-MOTION) show that `Motion` states have only one way in, both for tokens and for definitions:

- **One token source.** `Motion.State` takes one value (BRIDGE-MOTION-006), so tokens from two view-model properties need a combined string. Tempo's shell binds `FocusViewModel.MotionState => IsRunning ? $"{Mode} Running" : Mode.ToString()`, a property that exists only to feed the view's motion, with `NotifyPropertyChangedFor` on both inputs. The other option is a hand-written `IMultiValueConverter`. Any app whose states depend on a mode plus a flag (selected + busy, mode + running, error + focused) hits this.
- **One definition source.** `Motion.States` is one attached property, so a local `<presto:Motion.States>` replaces the list a style sets (local value beats style setter) instead of adding to it. Tempo's ring takes its mode `Fill` states from the `ProgressRing.accent` style, so its `Running` breathe sits on a wrapper `Panel` (`FocusView.axaml`, the `Panel` around `Ring`). Every themed control that also wants a local state needs a wrapper like this.
- **Name shadowing.** An app folder named `Motion` creates a `Tempo.Motion` namespace. In C#, that namespace shadows `MonacoLabs.Presto.Motion` anywhere under `Tempo.*`, so Tempo's tests alias it (`using PrestoMotion = MonacoLabs.Presto.Motion;`).

## What

**Token composition.** A new markup extension, `{presto:StateTokens}`, takes two to four bindings and provides a `MultiBinding` for `Motion.State`. Its value is the union of each binding's tokens:
- a `bool` that is true contributes the last member name of that binding's path (`{Binding Focus.IsRunning}` → `IsRunning`);
- a `bool` that is false, a `null` and an unset value contribute nothing;
- any other value contributes its tokens as BRIDGE-MOTION-006 reads them (an enum by name, a string split on separators).

Tempo's shell becomes:

```xml
presto:Motion.State="{presto:StateTokens {Binding Focus.Mode}, {Binding Focus.IsRunning}}"
```

and its states read `When="IsRunning"`. `FocusViewModel.MotionState` is deleted. The tracker itself does not change: it still reads one `Motion.State` value, so propagation, orchestration and the 006 tokenizer stay as they are.

**Additive states.** A new attached property, `Motion.LocalStates` (`MotionStates?`), layers after `Motion.States` instead of replacing it. The layering order becomes Hover, Focus, Press, `States`, `LocalStates`, then code-added states. The per-property rule is unchanged: the last active state in that order that names a property wins it (BRIDGE-MOTION-009). So a style's `States` and an element's own `LocalStates` share an element. Where both name the same property, the local list wins, as a local value beats a style. Tempo's ring takes `RingBreathe` as a `LocalStates` entry beside its style's `Fill` states, and the wrapper `Panel` goes away.

What does not change: `Motion.States` keeps its meaning and its replace-by-precedence behavior; `MotionState`, `When` parsing (one leading bare name per condition), rest, `Leave`, propagation (BRIDGE-MOTION-045–048) and orchestration are untouched. The `Motion` class keeps its name.

## Decisions

- **Compose tokens in a markup extension, not in the tracker.** Reason: `Motion.State` stays one value, so the propagation model (a follower reads its nearest source's one value) and orchestration need no change, and the extension uses only standard `MultiBinding` machinery. Rejected: a second token property such as `Motion.Flags`, since every propagation and orchestration rule would need a two-source version; a collection of `MotionToken { Name, When={Binding} }` items, since items in an attached list get no `DataContext` or name scope; a union of tokens from every ancestor that sets `Motion.State`, which breaks BRIDGE-MOTION-046 (an inner state scopes its subtree).
- **A true `bool` is named by its binding path's last member.** Reason: the flag needs a name, and the path already gives one, so `{Binding IsRunning}` reads as `When="IsRunning"` with nothing to repeat. Rejected: a positional `Names="_ Running"` parameter (two lists to keep in step); asking the app for `{Binding IsRunning, Converter=…}` per flag (the converter boilerplate this removes). A bool bound with an empty path (`{Binding}`) falls back to BRIDGE-MOTION-006 and contributes `True` or `False`.
- **Two to four bindings.** Reason: XAML positional arguments bind to constructors, and four covers every case seen; more is a sign the view model should expose an enum. Rejected: a `params` constructor, which the XAML compiler does not bind positionally.
- **`LocalStates` is a second list, ordered after `States`.** Reason: Avalonia gives a property one effective value, so a style's list and a local one can only both apply if they are separate properties, and putting the local list later matches local-over-style precedence. Rejected: reading `States` at both style and local priority (Avalonia has no public API for a value below the winning priority); `MotionStates.BasedOn` pointing at a resource list, which handles chains but makes the app move every style's list into a named resource first.
- **The name says intent, not a rule.** `LocalStates` may still be set from a style; then two styles setting it collide one level up, just as two styles setting `States` do today. Reason: the common case is one themed list plus one local list, and that is what the name describes. Rejected: throwing when `LocalStates` comes from a style, which would block a legitimate second style layer.
- **Keep `Motion`; document the shadowing.** In C#, an enclosing namespace's members beat types brought in by `using`. So any app namespace named `Motion`, `Presence` or `Connected` shadows the Presto type of that name, and no library-side alias or `global using` can win. The README's Motion section names the rule and the alias fix. Tempo keeps its `Motion/` folder (CLAUDE.md and ARCHITECTURE §5 cite it) and its test alias, as the documented consumer case. Rejected: renaming `Motion` (it breaks every XAML use, and the same rule would hit whatever name replaces it); renaming Tempo's folder (it hides the pitfall instead of documenting it).

## Spec changes

**BRIDGE-MOTION** (`motion-states.md`)
- `sources`: add `StateTokensExtension.cs`; `types`: add `StateTokensExtension`; `tests`: add `MotionStateTokensTests`.
- Add (Conditions): "`StateTokens` shall provide a `MultiBinding` whose value is the union of its bindings' tokens: a true `bool` contributes the last member name of its binding's path, a false `bool`, `null` or unset value contributes none, and any other value contributes its tokens as BRIDGE-MOTION-006 reads them."
- Add (Conditions): "When a `bool` binding of `StateTokens` has an empty path, its value shall contribute `True` or `False` as BRIDGE-MOTION-006 reads it."
- Add (Conditions): "When any one binding of `StateTokens` changes, `Motion.State` shall change once, so the tracker plays one transition."
- Change **BRIDGE-MOTION-008**: "… ordered Hover, Focus, Press, then `Motion.States` entries in declaration order, then `Motion.LocalStates` entries in declaration order, then code-added states in the order added."
- Add (Sugar, ordering and layering): "Setting `Motion.LocalStates` shall add its states to those of `Motion.States`, whatever the priority of either value."
- Change **BRIDGE-MOTION-025, -038, -039, -040**: each list of properties ("`Hover`, `Press`, `Focus` and `States`") gains `LocalStates`; 039 also covers a `LocalStates` collection change.
- Change **BRIDGE-MOTION-041**: "One `MotionStates` list shared by a style across controls, through `States` or `LocalStates`, …".
- Decisions: the first five decisions above; amend "One static class, `Motion`, named for its meaning" with the shadowing rule and its alias fix.
- Edge cases: add "A `StateTokens` flag and an enum value with the same name give one token."

## Tasks

1. **`Motion.LocalStates`.** Register the attached property and accessors in `Motion.cs` and add it to `HandleChanged`'s release check. In `MotionTracker.Rebuild`, read it after `States`, with its own `CollectionChanged` subscription (alongside `_states`), and unsubscribe in `Release`.
   - Tests (bridge, headless, `MotionStateTests`): a style setting `States` (`Fill` per token) plus a local `LocalStates` (`Scale` while `Running`) on one `Border` play both; when both name `Opacity`, the local entry wins and leaving it returns to the style entry's value; a code-added `While` still wins over both (BRIDGE-MOTION-043); clearing `LocalStates` while `States` remains rebuilds and returns its properties to rest; clearing both releases the tracker (038, now tested); a `LocalStates` collection change rebuilds (039, now tested); design mode creates no tracker.
   - Update BRIDGE-MOTION.
2. **`StateTokensExtension`.** Use 2-, 3- and 4-binding constructors. Read each binding's path name at `ProvideValue`, for both `Binding` and compiled bindings. Return a one-way `MultiBinding` with an internal converter that joins the tokens with spaces. Throw `InvalidOperationException` when it is set on anything but `Motion.StateProperty`, with the message naming that property.
   - Tests (`MotionStateTokensTests`): an enum plus a true flag gives both tokens, so a `When="Focus"` and a `When="IsRunning"` state both hold; the flag going false leaves only its state; a null binding contributes nothing; `{Binding}` on a bool contributes `True`; a compiled binding (`x:DataType`) and a reflection binding name the flag the same; one source change plays one transition (an animation count); followers of a `StateTokens` source stagger as they do from a plain value (`MotionPropagationTests`).
   - Update BRIDGE-MOTION.
3. **README.** In the Motion section, show `StateTokens` and `LocalStates`, and add the note on namespace shadowing and the `using X = MonacoLabs.Presto.Motion;` alias.
4. **Tempo.** In `ShellView.axaml`, bind `Motion.State` with `{presto:StateTokens {Binding Focus.Mode}, {Binding Focus.IsRunning}}`. Delete `FocusViewModel.MotionState` and its two `NotifyPropertyChangedFor` entries. Rename `When="Running"` to `When="IsRunning"` in `FocusView.axaml` (ring, halo, play icon) and in `TempoMotionTests`, and change `FocusViewTests`' `Running` assertion to `IsRunning`. Move `RingBreathe` from the wrapper `Panel` onto the `ProgressRing` as `presto:Motion.LocalStates`, delete the wrapper, and point `TempoMotionTests.EveryStateValidatesInBothMotionModes` at `ProgressRing`. Update ARCHITECTURE §5's "The mode is the root's `Motion.State`" decision.
   - Tests: `Tempo.Tests` green; a `FocusViewTests` case that starting the clock breathes the ring while the mode's `Fill` holds.
5. **Visual check** on `dotnet run --project samples/Tempo/Tempo`, DevTools screenshots: start, pause and skip across modes, so the ring recolors and breathes together, the halo pings, the play icon turns, and the accents still stagger across the window. The intro's ring flight lands with no pop now that the breathe's `Scale` sits on the flying ring itself. Repeat under Reduced motion.
   - **Outstanding:** the Reduced-motion pass and the intro ring landing were not checked. Run Tempo with System/Reduced motion and capture the intro landing (launch with the intro enabled and attach DevTools before the flight). Keep this change open until done.

## Open questions

- Can the `{presto:StateTokens …}` path name be read uniformly from a compiled binding (`CompiledBindingExtension.Path`) as well as a reflection `Binding`? If not, the fallback is to require reflection bindings inside the extension and say so in BRIDGE-MOTION. Settle this in task 2 before writing the requirement's final wording.
