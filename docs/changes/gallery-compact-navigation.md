# Gallery compact navigation

## Why

The gallery shell (`samples/PrestoSample/Views/MainView.axaml`) is a fixed master-detail layout: a 240 px sidebar column (`MinWidth` 180) holding the nav `TreeView`, search and settings, beside a content column with a 24 px margin. On a phone in portrait (360–430 DIP) the sidebar takes 55–70% of the width and the page gets what is left; collapsing the pane (Ctrl+B or the chevron) is the only way out, and it is a desktop affordance. There is no back navigation anywhere: a Reference page's Builder twin link replaces the page with no way back but the nav. The Android head builds and launches but has never been driven (BACKLOG [PLATFORM]); the Browser head on a phone has the same problem.

Pages themselves were built on desktop and some will not fit 400 DIP; that is a separate audit. This change fixes how you get to a page and back.

## What

**Two layouts, chosen by width.** The shell measures its own width. At 720 DIP and wider it is the **wide** layout, today's sidebar and page, unchanged apart from history (below). Narrower, it is the **compact** layout, a two-level page stack:

- **Home** is the nav at full width: the same `TreeView` of sections, search box and settings block that the sidebar holds, so search, the accordion, the selection pill and the motion settings behave the same. Rows get touch-sized padding under a `compact` class.
- **Page** is the selected sample at full width under a top bar: a back button, the section eyebrow and the title. The description and the twin line sit behind an "About" toggle, collapsed by default, because the longer blurbs would fill a phone screen. The page margin drops from 24 to 12.

Tapping a row on Home opens the page; tapping the row of the page already open opens it too (the nav selects on tap, not only on selection change). Back from a page returns to the previous page if one was opened from it (a twin link), otherwise to Home, with its search text and scroll position as they were.

**Push transitions.** In compact mode, Home → Page slides the page in from the right over Home, and back plays it reversed, through `PrestoPageTransition` with the `PagePush` recipe, driven directly on the Home and Page panels (`IPageTransition.Start(from, to, forward, …)`), so both stay attached and Home keeps its state. Page → page (twin links and their back) uses the same transition in the page host. Under reduced motion both crossfade, as the page recipes already do. The wide layout keeps `PageFadeThrough`.

**History in both layouts.** The view model keeps a back stack of opened pages. Wide: every page change pushes the previous one; Alt+Left and the mouse back button go back, and the transition plays reversed (`IsBack` already exists). Compact: opening from Home starts a fresh stack; twin links push; back pops, and an empty stack goes Home. The platform back request (Android's back button, `TopLevel.BackRequested`) goes back in compact mode after any open overlay (`Sheet`, `Modal`, `Drawer`) has had it, and is left unhandled at Home so the OS can close or background the app.

**Switching layouts at run time** (a desktop window resized across 720, a tablet rotated) keeps the open page: compact → wide shows it beside the sidebar; wide → compact shows it as the Page level, with Home beneath it. Starting compact with no `--page` opens Home and builds no page until one is chosen; going wide from Home opens the first page, as the wide layout does at start.

**What does not change.** The wide layout's look, the nav's motion decorations, the catalog, search, `--page`/`--motion`/`--size` and every sample page. The sidebar's collapse (Ctrl+B, chevron, splitter) exists only in the wide layout.

## Decisions

- **Adaptive, not page-based everywhere.** Wide keeps master-detail; only compact becomes a page stack. Reason: desktop is the acceptance target (ARCHITECTURE §6), jumping between 79 pages from an always-visible list is the fastest way to work there, and the nav tests (`NavPaneTests`, `NavPillTests`, `NavSearchTests`, …) pin that layout. Rejected: page-based navigation at every width (a tap-back-tap loop per page on desktop).
- **Width decides, not platform.** The layout follows the shell's own width with one breakpoint, 720 DIP. Reason: the Browser head runs on phones and desktops, tablets rotate, and a width rule is testable on the Desktop head (`--size 400x850`) and headlessly. 720 is the 240 px sidebar plus the narrowest content column (~480) that still reads as a page. Portrait tablets (768–834) therefore keep the sidebar. Rejected: `OperatingSystem.IsAndroid()`-style checks; a higher breakpoint that would put portrait tablets in the compact layout; Avalonia container-query styles alone (they can restyle, but the layout swap moves the nav between levels and needs code).
- **One nav, moved between levels.** The nav, search and settings are extracted into one `GalleryNav` user control that the shell places in the sidebar (wide) or on Home (compact). Reason: two `TreeView`s bound to one selection fight, as the existing "one `TreeView`" decision records; one instance also keeps search and expansion state across a layout switch. It is reparented only when the layout changes.
- **The view model owns navigation; no `NavigationPage`.** `MainViewModel` holds the back stack and an `AtHome` flag, and the shell drives the transitions from them. Reason: one source of truth that both layouts and headless tests read, and the wide layout reuses the same history. Rejected: Avalonia 12's `NavigationPage`, which keeps its own stack and back button that would have to be kept in step with the view model and cannot host the wide layout.
- **Home and Page stay attached; the transition is called directly.** Compact mode stacks the Home and Page panels in one cell and calls `PrestoPageTransition.Start` on them. Reason: a `TransitioningContentControl` swapping Home out would detach the nav, losing its scroll position and replaying its entrance cascade on every back. This exercises the transition outside a page host, which BRIDGE-PAGES allows ("every Avalonia page host"), and is recorded as a finding if it needs anything the bridge does not offer.
- **Overlays get the back request first.** The shell's `BackRequested` handler is re-added each time a page attaches, so it runs after the handlers of overlays that page attached. Reason: `Dismissal` subscribes on the top level in registration order and the shell subscribes first; without this, back would leave a page with its sheet still open. The ordering dependence is a bridge finding (below), not a sample fix.
- **Back is not Escape.** Back keys are Alt+Left and the mouse back button; Escape keeps clearing the search box and closing overlays. Reason: Escape already has two owners.
- **Wide back is keys only.** The wide layout shows no back button; Alt+Left and the mouse back button go back. Reason: the desktop look stays unchanged, and the sidebar is always one click away. Rejected: a header back button shown while history exists.
- **Twin links push; nav choices replace in compact.** Opening from Home starts a new stack; a twin link pushes. Reason: Home is the root, so "back" from a page chosen there means Home, while a twin link is a detour you return from. The wide history is capped at 50 pages.

## Spec changes

None; the gallery has no spec. ARCHITECTURE §6 gains the decisions above (adaptive layout at 720, one nav moved between levels, view-model navigation, overlays first on back, back keys, wide back keys only), and the "one `TreeView`" decision names `GalleryNav`.

Backlog additions:
- [BRIDGE-SHEET] `Dismissal`'s back and Escape handlers run in top-level registration order, so an app handler registered earlier (the gallery shell) runs before an open overlay's; consider handling them so an open overlay always wins.
- [SAMPLE] Audit the pages at 400 DIP in the compact layout and list the ones that clip or overflow, with a fix or a minimum-width note per page.

## Tasks

1. **Extract `GalleryNav`.** Move the sidebar grid (nav `TreeView`, its styles and templates, the no-matches caption, search and settings) and their handlers into `Views/GalleryNav.axaml`; `MainView` hosts it in the sidebar. Behavior unchanged. Tests: the existing nav suites pass unmodified (`NavScene` finds the `TreeView` by name through logical descendants).
2. **History in the view model.** Add the back stack, `GoBack`/`CanGoBack`, `AtHome` and `IsCompact` to `MainViewModel`; `OnSelectedPageChanged` pushes, `GoBack` pops and sets `IsBack`; twin links (`OpenCommand`) push. Tests: `GalleryHistoryTests` — wide pushes on every change and caps at 50; back restores the previous page with `IsBack` set; compact open-from-Home clears the stack; twin link then back returns to the Reference page; back with an empty stack in compact goes Home and in wide does nothing.
3. **Wide back keys.** Alt+Left and the mouse back button call `GoBack` in both layouts. Tests: a `NavScene` test raising each and asserting the previous page and a reversed transition.
4. **Compact layout.** In `MainView`, switch layouts on width with the 720 breakpoint; stack Home (`GalleryNav`) and Page (top bar, About toggle, page host) in one cell; drive `PagePush` between them; `compact` class for touch padding and the 12 px margin; hide the splitter, chevron and Ctrl+B. Tests: `CompactShellTests` at 400×850 — Home shows the nav at full width and no page is built; tapping a row opens it, tapping the open page's row opens it again; back returns to Home with search text and scroll offset kept; the About toggle shows the description and twin link; reduced motion crossfades.
5. **Platform back.** Handle `TopLevel.BackRequested` in compact mode, re-added on each page attach. Tests: back pops a page; at Home it is left unhandled; on a page with an open `Sheet` the first back closes the sheet and the second leaves the page.
6. **Layout switching.** Resizing across 720 keeps the open page and the nav's state; going wide from Home opens the first page. Tests: `CompactShellTests` resizing the window both ways, with and without a page open.
7. **Visual check.** Desktop head with `--size 400x850`: Home, open, back, a twin link and back, search kept, reduced motion; then resize the window across 720 both ways. Desktop at 1400×1000: the wide layout looks unchanged and Alt+Left goes back.
8. **Android smoke run.** Install on a device or emulator: Home fits, pages open and the system back button returns, an open sheet closes first, back at Home leaves the app. Record the result in the README Platforms table and update BACKLOG [PLATFORM].
9. **Docs.** ARCHITECTURE §6 decisions and the backlog additions above; the README's gallery section mentions the compact layout and the back keys.

## Open questions

None.
