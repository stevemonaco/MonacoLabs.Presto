# One sheet across breakpoints

## Why

A responsive app usually shows the same panel differently by width: a side drawer or an anchored popover when wide, a bottom sheet when compact. Presto has no way to do that with one element. A `Sheet`'s `Edge` and `Detents` are styled properties, but the edge swipe is fixed by type (`Drawer` has a 20-DIP strip, `Sheet` none, BRIDGE-SHEET-025/027), the content has no hook to restyle its chrome per edge, and an anchored popover is a separate Avalonia `Flyout` with its own content and a read-only open state.

So Tempo builds its adaptive surfaces by hand (BACKLOG, Tempo findings, BRIDGE-SHEET):
- `ShellView.axaml` declares a `DetailDrawer` (`Edge="Right"`) and a `DetailSheet` (`Detents="0,0.5,1"`) for one `TaskDetail`, plus a `SettingsSheet` and a settings `Flyout` for one `SettingsFlyout`.
- `ShellView.PlaceSurfaces` runs on each breakpoint flip: it closes all four surfaces, moves `Detail` between `DetailDrawer.Content` and a `DetailSlot` decorator in the sheet, disposes and re-creates a `ReflectionBinding` so only the live surface binds `IsDetailOpen`, and moves `Settings` between the flyout's `Content` and a `SettingsSlot`.
- A style hides the drawer in compact (`v|ShellView.compact presto|Drawer`), or its edge strip would open it empty.
- `ShowSettings` (behind a `MainViewModel.SettingsRequested` event, because `FlyoutBase.IsOpen` is read-only) and `ShowConfirm` branch on `_compact` to open or close the right one. The gear's and the bottom bar's `open` classes are kept in sync by a `PropertyChanged` lambda and the `SettingsToggled` handler.
- Open settings close when the window crosses the breakpoint (`SurfaceTests.CrossingTheBreakpointMovesOpenDetailsAndClosesSettings`), because moving content out of a popup that is still closing is the hard part.

Any app with a phone and a desktop layout needs this pattern. It should be one element with one `IsOpen` and one `Content`, switched by a style.

## What

**One `Sheet` changes edge, detents and edge swipe from a style.** `Sheet` gains a public `EdgeSwipe` styled property (DIPs, default 0; `Drawer` overrides the default to 20), so a style can turn a plain sheet into a swipe-to-open drawer and back. `Sheet` sets an edge pseudo-class (`:left`, `:top`, `:right` or `:bottom`), so its content's chrome can follow the edge: `presto|Sheet:bottom Border.grab` shows a grab handle only on the bottom sheet. When `Edge` or `Detents` changes while the sheet is open, the sheet stays open. It lands at once on the coerced `DetentIndex`'s detent on the new edge, without replaying the content's entrances. A drag in progress is dropped.

**A sheet can present anchored, as a flyout.** `Sheet` gains:
- `Anchor` (`Control?`): the element to anchor to.
- `Flyout` (`Flyout?`): an optional flyout that carries placement, `FlyoutPresenterClasses` and `Popover.Open`/`Close`.
- `Presentation` (`SheetPresentation.Edge | Anchored`, styled, default `Edge`).

While `Presentation` is `Anchored` and `Anchor` is effectively visible, opening the sheet moves `Content` into the flyout and shows it at `Anchor`. The edge panel, the scrim and the edge strip stay hidden. When the flyout closes (light dismiss, Escape, `IsOpen = false`), `IsOpen` turns false and the content goes back to the panel once the flyout has closed, so a `Popover.Close` still plays on it. Anchored, the sheet is only open or closed: opening sets `DetentIndex` to 1, so `IsOpen == DetentIndex > 0` still holds. If `Presentation` flips while the sheet is open, it stays open and moves:
- Edge to anchored: the panel hides at once and the flyout opens.
- Anchored to edge: the flyout closes, and once it has closed the panel lands open at once.

**The breakpoint stays the app's.** Presto adds no width API. The switch is a style: a class the app sets (Tempo's `v|ShellView.compact`) or an Avalonia `ContainerQuery`. Tempo's detail becomes one sheet:

```xml
<presto:Sheet x:Name="DetailSheet" Grid.Row="1" Edge="Right" EdgeSwipe="20" TimeConstant="150" IsOpen="{Binding IsDetailOpen}">
    <v:TaskDetail x:Name="Detail" DataContext="{Binding SelectedTask}" />
</presto:Sheet>
<!-- styles -->
<Style Selector="v|ShellView.compact presto|Sheet#DetailSheet">
    <Setter Property="Edge" Value="Bottom" />
    <Setter Property="EdgeSwipe" Value="0" />
    <Setter Property="Detents" Value="0,0.5,1" />
    <Setter Property="Grid.RowSpan" Value="2" />
</Style>
```

The settings become one sheet with `Anchor="{Binding #SettingsButton}"`, the existing `Flyout` (placement, `glass` class, `TempoMotion.FlyoutOpen`/`FlyoutClose`) as `Sheet.Flyout`, `Presentation="Anchored"` by default, and a compact style that sets `Edge`. `IsOpen` binds a new `MainViewModel.IsSettingsOpen`.

What does not change: a `Sheet` whose `Presentation` is never set behaves as today; `Drawer` keeps its 20-DIP strip and `Left` default; the edge-presented sheet's drag, inertia, scrim and Escape/back handling (BRIDGE-SHEET-013–023) are untouched; `Popover` (BRIDGE-POPOVER) plays on the sheet's flyout as on any flyout.

## Decisions

- **Extend `Sheet` rather than add a new adaptive control.** Reason: `Edge` and `Detents` are already styled properties, so most of the feature is making the rest styleable and keeping the open state across a change; one control keeps one `IsOpen`, one `Content`, and the one Escape/back registration (`Dismissal`). Rejected: an `AdaptiveSurface` that hosts a `Drawer`, a `Sheet` and a flyout and moves content between them (Tempo's code, moved into the library, with three open states to reconcile); a `Breakpoints` collection of edge/detent sets on the sheet (duplicates styles).
- **No breakpoint API; the switch is a style.** Reason: Avalonia has container queries and classes, and an app's breakpoint drives more than its surfaces (Tempo's bottom bar, Tasks page and Stats layout all key off `.compact`). Rejected: a `Sheet.CompactWidth` threshold (a second, competing breakpoint).
- **`EdgeSwipe` becomes a public styled property; `Drawer` stays.** Reason: the edge strip was the one behavior bound to the type, and it must follow the edge (a bottom sheet with a right-edge strip makes no sense). `Drawer` keeps working as the preset with `Left` and 20. Rejected: making `Drawer` obsolete (breaks nothing to keep, and its default edge reads well in XAML).
- **Edge pseudo-classes on the sheet.** Reason: a side panel and a bottom sheet need different chrome (border side, corner radius, grab handle) on the same content, and a pseudo-class is how Avalonia content follows its host's state. Rejected: a templated sheet with per-edge parts (the sheet is deliberately untemplated, see its decision).
- **An edge or detent change while open keeps the sheet open and lands at once.** Reason: a breakpoint flip is a layout change, not a user gesture, so moving the open panel to its new edge without a glide matches what the rest of the layout does on resize; it reuses BRIDGE-SHEET-005's land-at-once. Rejected: closing on a flip (what Tempo does for settings today, and it loses the user's place); gliding to the new edge (the panel would slide across the content while the window is being dragged).
- **Anchored presentation reuses Avalonia's `Flyout`.** Reason: placement, light dismiss, popup windows and `Popover` motion already work there; the sheet only owns the content's move and the open state, which is the part apps get wrong (moving content out of a popup that is still playing its close). Rejected: arranging the sheet's own panel beside the anchor (reimplements popup placement and leaves the window's bounds); making `FlyoutBase.IsOpen` bindable in `Popover` (still two contents).
- **The content returns to the panel only after the flyout has closed.** Reason: `Popover.Close` plays on the live presenter (BRIDGE-POPOVER-010), so moving the content earlier empties the flyout mid-close. Rejected: moving it on `Closing` (blank close).
- **Anchored falls back to the edge when `Anchor` is null or not effectively visible.** Reason: a style may hide the anchor at the same breakpoint (Tempo hides the gear in compact), and opening a flyout on a hidden element fails or places it at the origin. Rejected: throwing or staying closed.
- **Anchored, the sheet does not register with `Dismissal`.** Reason: the flyout already closes on Escape and light dismiss, and a second handler would close the sheet ahead of a flyout opened above it. Rejected: handling Escape in both (double close).
- **An anchored sheet needs two detents, like an edge sheet.** A single-detent sheet stays closed when anchored too (BRIDGE-SHEET-038), and the docs say so; the default `"0,Auto"` already has two. Reason: it keeps BRIDGE-SHEET-010's coercion whole for every presentation. Rejected: ignoring the detent count when anchored and setting `DetentIndex` to 1 past the coercion, which breaks BRIDGE-SHEET-010 for one state.
- **`Sheet.Flyout` is a `Flyout`, not any `PopupFlyoutBase`.** Reason: the sheet moves its content into the flyout's `Content`, which only `Flyout` has. Rejected: `PopupFlyoutBase`, whose subtypes build their presenter differently.

## Spec changes

**BRIDGE-SHEET** (`sheet.md`)
- `types`: add `SheetPresentation`; `sources`: add `Controls/SheetPresentation.cs`.
- Purpose: add a sentence on the anchored presentation and on restyling across breakpoints.
- Change BRIDGE-SHEET-025: "While a sheet's panel is hidden, a strip `EdgeSwipe` DIPs deep along its edge shall take presses, so a swipe in from that edge opens it."
- Change BRIDGE-SHEET-027 (no longer inherited): "`EdgeSwipe` shall default to 0 on a `Sheet` and 20 on a `Drawer`, so a closed, hidden plain sheet takes no drag."
- Strike BRIDGE-SHEET-024's "untested" by citing a test; wording unchanged.
- Change BRIDGE-SHEET-006 (no longer inherited): "When `Edge` changes, the sheet shall revert its animatable, rebuild it on the new axis and land on `DetentIndex`'s detent at once, keeping `IsOpen` and playing no entrances; a drag in progress shall be dropped."
- Add: "The sheet shall carry exactly one of the pseudo-classes `:left`, `:top`, `:right`, `:bottom`, matching `Edge`."
- Add: "When `Detents` changes while the sheet is open, it shall stay open on the coerced `DetentIndex` and land on its detent at once."
- Add: "While `Presentation` is `Anchored` and `Anchor` is effectively visible, opening the sheet shall set `DetentIndex` to 1, move `Content` into `Flyout` (or a default `Flyout` when it is null) and show it at `Anchor`, leaving the panel, scrim and edge strip hidden."
- Add: "When the anchored flyout closes, the sheet shall set `IsOpen` false, and move `Content` back to its panel once the flyout has closed."
- Add: "Setting `IsOpen` false on an anchored open sheet shall hide its flyout, so a `Popover.Close` on it plays."
- Add: "When `Presentation` changes while the sheet is open, the sheet shall stay open: to `Anchored`, its panel shall hide at once and its flyout open; to `Edge`, its flyout shall close and, once it has closed, the panel land open at once."
- Add: "While `Presentation` is `Anchored` and `Anchor` is null or not effectively visible, the sheet shall open at its edge."
- Add: "While presented anchored, the sheet shall not take Escape or back through `Dismissal`; its flyout's own dismissal closes it."
- Add (Defaults): "When `Presentation` is not set, it shall be `Edge`."
- Invariants: "`IsOpen == (DetentIndex > 0)`" holds in both presentations; the edge strip is visible exactly while the panel is hidden *and* the sheet presents at its edge.
- Edge cases: an anchored sheet with a single detent stays closed, as at an edge (BRIDGE-SHEET-038); a style that hides `Anchor` while the anchored flyout is open (the flyout closes as BRIDGE-POPOVER-013, and the sheet reads closed).
- Decisions: the eight decisions above.
- Open items: drop "the `Edge` change rebuild" and "the `Drawer` default edge" from the untested list.

**BRIDGE-POPOVER** (`popovers.md`)
- No requirement changes. Add an edge case: a flyout owned by a `Sheet`'s anchored presentation is handled like any flyout; the sheet moves its content only after `Closed`.

## Tasks

1. **Styleable edge swipe and edge pseudo-classes.** Add `Sheet.EdgeSwipeProperty` (replacing the `private protected virtual EdgeSwipe`), override its default on `Drawer`, and set the edge pseudo-class in `OnPropertyChanged` and the constructor. Update BRIDGE-SHEET-024/025/027 and add the pseudo-class requirement.
   - Tests (bridge, headless): `DrawerTests`: a drawer's default edge is `Left` and its strip 20; a `Sheet` with `EdgeSwipe="20"` opens on an edge swipe and one with 0 does not; a style that sets `EdgeSwipe` to 0 on a `Drawer` stops the swipe. `SheetTests`: the pseudo-class follows `Edge`, and a style on `presto|Sheet:bottom` reaches the content.
2. **Keep the open state across an edge or detent change.** In `OnPropertyChanged` for `EdgeProperty` and `DetentsProperty`, drop a drag in progress, and land on the coerced detent at once on the next arrange without hiding the panel or replaying `Presence.EnterOnShow`. Update BRIDGE-SHEET-005/006 and add the detents-while-open requirement.
   - Tests: an open right-edge sheet flipped to `Bottom` by a style class stays open, rests at its detent on the new axis in the same layout pass, and its content's entrance plays only once; an open sheet at index 2 whose detents shrink to two lands open at index 1; a flip mid-drag ends the drag with no capture left.
3. **Anchored presentation.** Add `SheetPresentation`, `Sheet.Anchor`, `Sheet.Flyout` and `Sheet.Presentation`; move content into and out of the flyout, keep `DetentIndex`/`IsOpen` in sync, skip `Dismissal` while anchored, fall back to the edge without a visible anchor, and handle a presentation flip while open. Add the anchored requirements and the BRIDGE-POPOVER edge case.
   - Tests: opening an anchored sheet shows a `FlyoutPresenter` holding the content at the anchor and leaves the panel hidden; a light dismiss sets `IsOpen` false and the content returns to the panel after `Closed`; with `Popover.Close` on the flyout, the content stays in the presenter until the close has played; `IsOpen = false` hides the flyout; a hidden anchor opens at the edge; an open sheet flipped `Anchored` → `Edge` ends with the flyout closed and the panel open at its detent, and back; Escape on an anchored open sheet is handled once, by the flyout; `IsOpen == DetentIndex > 0` after each step.
4. **Tempo: one detail sheet.** Replace `DetailDrawer` and `DetailSheet` with one `Sheet` bound to `IsDetailOpen`, its compact edge, detents, edge swipe and row span set by a `v|ShellView.compact` style, and its chrome (grab handle, rounded top, side border) keyed off `:bottom`/`:right`. Delete `DetailSlot`, the detail half of `PlaceSurfaces`, `_detailOpen`, the `ReflectionBinding`, and the `v|ShellView.compact presto|Drawer` style.
   - Tests (`Tempo.Tests`): update `SurfaceTests.CompactOpensDetailsAndSettingsInBottomSheets` and `CrossingTheBreakpointMovesOpenDetailsAndClosesSettings` to find the one sheet and assert its edge on each side of the breakpoint, open throughout.
5. **Tempo: one settings sheet.** Replace `SettingsSheet` and the attached flyout with one anchored `Sheet` (`Anchor` = `SettingsButton`, the existing flyout as `Sheet.Flyout`), bound to a new `MainViewModel.IsSettingsOpen` that `OpenSettingsCommand` sets. Bind both buttons' `open` class to its `IsOpen`. Delete the rest of `PlaceSurfaces` (so `MeasureOverride` keeps only `IsCompact`, the class and the Tasks pane move), `SettingsSlot`, `ShowSettings`, `SettingsRequested`, `SettingsToggled` and the `PropertyChanged` lambda, and reduce `ShowConfirm`'s closing to `IsSettingsOpen = false`. Restyle the content's `Border.sheet` wrapper inside `FlyoutPresenter.glass`.
   - Tests: rename `CrossingTheBreakpointMovesOpenDetailsAndClosesSettings` to say open settings move across the breakpoint and assert they do, flyout to sheet and back; `SettingsTests` keep passing with `IsSettingsOpen`; `TheBottomBarsSettingsOpensTheSheetOverThePageWithoutMovingThePill` unchanged.
6. **Docs.** Update Tempo's README surface row and ARCHITECTURE §5 if it describes `PlaceSurfaces`; remove the BRIDGE-SHEET Tempo finding from the backlog.
7. **Visual check** on Desktop, both motion modes, DevTools screenshots:
   - Tempo wide: the details drawer swipes in from the right edge and closes with Escape; the settings popover opens from the gear with its `FlyoutOpen` and closes with `FlyoutClose`, content intact through the close.
   - Tempo at `--size 360x780`: details and settings open as bottom sheets with grab handles; no edge strip opens anything.
   - Drag the window across 720 DIP with details open, then with settings open: each stays open and lands on its new edge or popover with no flash of empty chrome.
   - Gallery Gestures page: its sheet and drawer unchanged.

## Open questions

None. The BRIDGE-PAGES finding (hosting a live element as a page, Tempo's `ShellView.TasksPage`) has the same content-move problem but a different owner (a `TransitioningContentControl`'s template) and is not solved here; it stays in the backlog.
