# Gallery and Tempo showcase the recent bridge features

## Why

The last round of changes added several bridge features. Four of them appear only in Tempo:
- composed states (`{presto:StateTokens}`, `Motion.LocalStates`);
- the adaptive `Sheet` (`EdgeSwipe`, the edge pseudo-classes, `Presentation="Anchored"`);
- `Animate.DoOnChanged` with `{presto:MorphGlyphs}`;
- `Modal.IsOrigin`.

Tempo is an app, not a reference. It has no code panels, and each feature is buried in a view that does other things. Two more features are in the gallery but do not show what changed:
- Lifecycle's "Clear rows" already staggers its exits, but nothing on the page shows the batch order, picks its origin, or compares `Wait` with `Sync` on the rows' host.
- Storefront hand-diffs its filtered products (`StorefrontViewModel.Pick`), which `FilteredView<T>` now does, and its cards leave all at once.

The snap fix for animated sizes is visible only on Micro-interactions, with nothing saying what to look for. Its one known gap, a half-pixel tie at 125% (BACKLOG, BRIDGE-TARGETS), is still undecided.

The visual checks also found two Tempo defects:
- Under Reduced motion, restored rows still fade in one after another, because `RowEnter`'s reduced form keeps the full form's `Delay(Presto.Stagger(40))`.
- During a tag-filter swap, the rows that stay slide up under the leaving rows' ghosts, so titles overlap for a moment. Under Reduced the overlap lasts the 160 ms fade.

## What

**States page: a "Composed state" card.** A new card on the States reference page shows one status chip driven by three page properties:
- a `Mode` picker (Draft, Review, Live);
- a `Selected` toggle;
- a `Busy` toggle.

The chip binds `presto:Motion.State="{presto:StateTokens {Binding Mode}, {Binding IsSelected}, {Binding IsBusy}}"`. Its `Motion.States` recolor it by mode, add an outline `When="IsSelected"`, and pulse it `When="IsBusy"`. A readout under the chip shows the live token string, bound to the chip's `Motion.State`, for example `Review IsSelected IsBusy`.

Beside it, a **layering** card. A style gives every `.layer-card` a `Motion.States` list that colors it by the same mode. One card also sets its own `Motion.LocalStates` with a `When="IsBusy"` entry that names the same `Background`, plus a `Scale` breathe. With Busy on, that card takes the local background and breathes, while its siblings keep the style's color. The caption gives the layering order (Hover, Focus, Press, `States`, `LocalStates`, code) and says that a later layer wins a shared property. Both cards print their XAML through snippet regions.

**A new Showcase page, "Adaptive sheet".** It is a small notes app inside a resizable stage. A Phone | Tablet | Desktop segmented control sets the stage's width to 390, 820, or the full card. The stage width is the breakpoint: an Avalonia `ContainerQuery` at 720 DIP on the stage, with no Presto width API. Two `Sheet`s each switch form by style alone:
- **Details:** a right-edge drawer with a 20 DIP `EdgeSwipe` at 720 DIP and wider, a bottom sheet with detents `0,0.5,1` and no edge swipe below. A grab handle shows only under `presto|Sheet:bottom`.
- **Settings:** anchored at the stage's gear as a flyout (`Presentation="Anchored"`, `Popover.Open`/`Close` on its `Flyout`) when wide, a bottom sheet below.

A readout shows each sheet's `IsOpen`, `DetentIndex`, `Edge` and `Presentation`. Change the width while a sheet is open: it stays open and lands on its new edge or anchor. The page prints the sheet XAML and the two container-query styles.

**Lifecycle: the exit batch made visible.** Beside the existing row buttons:
- an **Exit order** picker (First, Last, Center) sets the row exit's stagger `From` and rebuilds the printed builder;
- a **Host** picker (Sync, Wait) sets the `Presence.Mode` of the rows' host. That host is the panel holding the list and its "No rows" line, which today is fixed to `Wait`.
- **Remove three** removes three rows that are not next to each other in one call: a batch of three.

Each exit's log entry names its row, read from the ghost's `DataContext`, so the log shows the batch order. With Wait, "Clear rows" leaves in the picked order and then the empty line fades in. With Sync, the empty line fades in while the rows are still leaving.

**Storefront: a `FilteredView` and a staggered exit.**
- `StorefrontViewModel.Shown` becomes a `FilteredView<Product>` over the catalog, and `Pick` sets the picked categories and calls `Refresh()`. The hand-written diff goes away.
- The card exit becomes a page builder, `StorefrontPage.CardExit`, set with `{x:Static}`: a scale-and-fade with `Delay(Presto.Stagger(35, new() { From = StaggerFrom.Center }))`, whose reduced form is a fade with no delay. When you pick a category, the cards that leave go out from the middle of the batch outward, while the cards that stay reflow.

**Micro-interactions: say what the snap fixed.** A caption on the "Place order" card says that its width animates in whole device-pixel steps measured from where it started, so both edges move together and the label does not shake. The page description says the same. Submit flow is already on the plain library path and its description no longer mentions a workaround, so it is unchanged.

**Tempo: two fixes.**
- `RowEnter`'s reduced form gains `.Delay(0)`, as `RowExit`'s already has.
- `RowExit`'s full form fades over its first 120 ms with an out ease (`OutQuad`), while the slide still runs 220 ms `InCubic`. A leaving row is mostly transparent before a staying row reaches its slot. The reduced exit keeps its 160 ms fade but takes the same front-loaded ease.

## Decisions

These go to ARCHITECTURE §5 (Gallery and Tempo).

- **Composed states join the States reference page, not a new page.** Reason: they are part of the `Motion` states feature, and reference pages are one per feature. Rejected: a "State tokens" page (one card's worth of content).
- **The adaptive sheet is its own Showcase page.** Reason: it combines `Sheet`, `Popover` and a container query, so it is a multi-feature demo. The Gestures page's rows are full (see its decisions), and Popups is about popup owners. Rejected: a fifth Gestures row, and a section on Popups.
- **The breakpoint is a container query on a stage the page sizes.** Reason: it shows the "the breakpoint stays the app's" decision as an app would write it, and a desktop user can flip it without resizing the window. Rejected: following the gallery window's width (it would need a resize, and in compact the gallery shell takes over).
- **Lifecycle's Wait/Sync picker sits on the rows' host, beside the wizard's.** Reason: the wizard shows `Wait` between views, and the list shows the newer case, where a shown element waits for an exit batch. Rejected: a second page.
- **Storefront's stagger runs from the center over the batch order, without `Grid`.** Reason: the leaving cards are scattered across a `WrapPanel` whose column count changes with the width, so they form no grid. The batch's reading order is what an exit knows. Rejected: a `Grid` stagger computed from the panel's current columns.
- **Storefront's exit is a page builder, not a recipe extension.** Reason: `{presto:Scale}` takes a `Stagger` step but no origin. "Demos never quietly grow the library", so the missing `From` goes to the backlog as a finding. Rejected: adding `StaggerFrom` to `RecipeExtension` in this change.
- **Tempo's filter overlap is eased in the sample, and the cause goes to the backlog.** Ghosts draw in the top level's overlay layer, above the live rows (and above a `Modal`'s scrim, the clear's other finding). As long as that holds, a leaving row that fades early is the sample-side fix. BRIDGE-DECLLAYOUT-020 (the rows ease into the gap while the exit plays) stays as it is. Rejected: delaying `{presto:Reflow}` (every insert and reorder would lag), and a zero-duration reduced exit (it would lose the clear's fade and the `Wait` handoff).

## Spec changes

None to requirements; samples are not owned by specs.

Depending on the open question, a BRIDGE-TARGETS (`property-resolution.md`) decision records the 125% tie, and the backlog's "to decide" note on BRIDGE-TARGETS is struck.

## Tasks

1. **States: Composed state and layering cards.** In `Pages/StatesPage.axaml(.cs)`:
   - Add `Mode`, `IsSelected` and `IsBusy` page properties, the chip, the readout and the layering cards, with snippet regions `StatesComposed` and `StatesLayering` printed in code panels.
   - Update the States description in `MainViewModel.Catalog`.
   - Tests (gallery, headless):
     - toggling Busy and Selected and picking Review makes the readout `Review IsSelected IsBusy`, and turning both off makes it `Review`;
     - with Busy on, the local card's `Background` is the local state's while a sibling keeps the style's;
     - reduced motion stops the busy pulse and the breathe, and the colors still change.
2. **Adaptive sheet page.**
   - Add `Showcase/Pages/AdaptiveSheetPage.axaml(.cs)` on `ShowcasePage`, with its data from `Datasets`, and list it in the Showcase section after Popups with a description.
   - Snippet regions `AdaptiveSheetDetails`, `AdaptiveSheetSettings` and `AdaptiveSheetStyles`.
   - Tests (`AdaptiveSheetPageTests`):
     - at Desktop width the details sheet is on the right with a 20 DIP edge swipe, and at Phone width it is a bottom sheet with no edge swipe;
     - open details at Desktop, switch to Phone: still open, and `DetentIndex > 0` on the bottom edge;
     - settings open as a `FlyoutPresenter` at the gear when wide and as a bottom sheet at Phone;
     - a light dismiss closes either one and sets `IsOpen` false;
     - the grab handle is visible only on the bottom edge.

     `GalleryPageTests` already opens every page in both motion modes.
3. **Lifecycle: exit order, host mode, Remove three.**
   - Add the two pickers and the button in `Pages/LifecyclePage.axaml(.cs)`. The row template reads the current exit builder, and a change to the picker sets it on the realized rows.
   - The exit's `OnBegin` logs the ghost's item.
   - Update the caption text and the Lifecycle description.
   - Tests:
     - "Clear rows" on Inbox logs the exits top to bottom with First, bottom to top with Last, and from the middle out with Center;
     - Remove three logs three exits in position order;
     - with Wait, the empty line is held at opacity 0 until the last ghost ends;
     - with Sync, the empty line arrives while the ghosts are still leaving.
4. **Storefront.**
   - Replace `Shown` and the diff in `Pick` with a `FilteredView<Product>`.
   - Add `StorefrontPage.CardExit` and set it as the cards' `Presence.Exit`.
   - Update the description.
   - Tests (`StorefrontPageTests`):
     - picking a category raises one `Remove` per hidden card and never a `Reset`;
     - the leaving cards' exits start in center-out order of the batch;
     - reduced, every exit starts at once;
     - unpicking brings the cards back in catalog order;
     - the existing quick-view, add-to-cart and chip tests pass unchanged.
5. **Micro-interactions caption.** Add the caption and the description sentence. Visual check at 100% and 125%.
6. **Tempo fixes.** Edit `RowEnter` and `RowExit` in `Motion/TempoMotion.cs`.
   - Tests (`TempoMotionTests` or `TasksViewTests`):
     - under reduced motion, Restore sample data starts every row's entrance at delay 0;
     - at full motion, a leaving row's ghost reaches opacity ≤ 0.1 by 120 ms while its X is still moving;
     - the clear still leaves top row first and the empty line still waits.
   - Visual check: switch tag chips both ways at full and reduced motion with no titles overlapping at full opacity.
7. **Backlog.** Add the findings:
   - [BRIDGE-RECIPES] recipe extensions take a `Stagger` step but no `From`/`Grid`;
   - [BRIDGE-PRESENCE-EXIT] ghosts draw in the overlay layer, above live siblings and above a `Modal`'s scrim, so a leaving row overlaps the rows easing into its gap and stays sharp over a modal's blur.
8. **Visual checks** on the Desktop gallery, DevTools screenshots, in both motion modes:
   - States: the chip's tokens and the layering card;
   - Adaptive sheet: all three widths, flipping while each sheet is open;
   - Lifecycle: each exit order, and Sync against Wait on "Clear rows";
   - Storefront: picking and unpicking categories;
   - Micro-interactions: "Place order";
   - Tempo: tag chips and Restore sample data.

## Open questions

- **The 125% label tie on `SubmitButton`.** Recommended: accept it and record a BRIDGE-TARGETS decision. The label is fading during both moves, and the cause is a half-pixel centering tie that Avalonia breaks by floating-point noise, which no even step removes. Alternatives:
  - bias the snapped width so ties round one way (still a 1 px move at the start and end, and it relies on Avalonia's rounding);
  - restructure `SubmitButton` so the label sits over the button, not inside it (a control change).

  The caption's wording follows the answer.
