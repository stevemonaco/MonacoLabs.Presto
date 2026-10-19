# Tempo runs on Desktop, Browser, Android and iOS

## Why

Tempo is one WinExe project, so it only runs on Windows, macOS and Linux desktops. ARCHITECTURE §5 put this off on purpose ("One Desktop head, responsive by width … the split can come later if one is wanted"). The compact arrangement already works down to a 320 DIP width, with a bottom bar, bottom sheets, touch reorder and swipes. Most of a phone build is already done, but nothing runs it.

A phone and a browser tab are where the dogfooding has been weakest. The bridge's touch, gesture, hidden-engine and render-thread behavior is checked by hand on the gallery's Browser and Android heads, if at all (BACKLOG: Manual and platform checks). The gallery shows one feature per page. Tempo would show them together in one app, the same app the Desktop checks already use. A focus timer is also an app people use on a phone.

## What

Tempo gets the gallery's layout: the existing `samples/Tempo/Tempo/` project becomes a shared class library, with four heads beside it:

| Project | Target | Holds |
|---|---|---|
| `samples/Tempo/Tempo/` | `net10.0` library | Everything there today except `Program.cs` and `app.manifest`. This includes `App`, `LaunchOptions`, the views, and both `MainWindow` and the new `MainView`. |
| `samples/Tempo/Tempo.Desktop/` | `net10.0` WinExe | `Program.cs` (launch-option parsing, `StartWithClassicDesktopLifetime`) and `app.manifest`. |
| `samples/Tempo/Tempo.Browser/` | `net10.0-browser` | `Program.cs` (`StartBrowserAppAsync("out")`), `wwwroot/`, and the `localStorage` store. |
| `samples/Tempo/Tempo.Android/` | `net10.0-android` | `Application`, `MainActivity` and the resources (application id `com.monacolabs.tempo`). |
| `samples/Tempo/Tempo.iOS/` | `net10.0-ios` | `AppDelegate`, `Main`, `Info.plist` and the launch screen. |

Each head calls `UsePresto()` and `WithInterFont()`, as the gallery's heads do. Desktop also calls `UsePlatformDetect()` and sets up the logging it has today. `Tempo.Tests` keeps referencing the shared project, so the suite needs no new workload.

**One view for every lifetime.** A new `Views/MainView` user control holds the parts of today's `MainWindow` that are not about a window:
- the intro swap (`Start`);
- the N, Delete and Ctrl+, shortcuts and turning them off while a text box has focus;
- Space as the timer's key;
- the `Motion.Preference` binding;
- Android's back button.

`MainWindow` keeps only what needs a window: it hosts a `MainView`, extends into the title bar, hints Mica, restores and saves its placement, applies `--size`, and flushes on close. `App` creates the view model the same way under all three lifetimes (classic desktop, activity, single view). It awaits the load before showing anything, as today: a `MainWindow` on desktop, a bare `MainView` otherwise.

**Off a window, Tempo paints its own base and pads for the system bars.** Without a window there is no window material and no caption buttons. The Mica-friendly transparency and the 138 DIP caption clearance apply only to a `MainView` with the `windowed` class, which `MainWindow` sets. A `MainView` without it paints an opaque base. It reads the top level's safe-area insets and follows changes to them. The backdrop and blobs run edge to edge. The title bar pads by the top inset, the bottom bar by the bottom inset, and the panels by the side insets. Each head's own background (the page `body`, the Android splash and window background, the iOS launch screen) is Tempo's base color, so no white frame shows before the first render.

**Saving survives an app that is never closed.** On Desktop the flush on close stays. Under the other lifetimes, `App` flushes when the activatable lifetime reports that the app went to the background. On Android and iOS the OS can then kill the app without warning. In the browser, the head flushes when the page is hidden.

**The browser keeps its data in `localStorage`.** .NET's file system in WebAssembly is in memory, so `tempo.json` would be lost on every reload. `TempoStore` keeps the JSON format, the first-run check (`Exists`), the corrupt-file backup and the sample seeding. It does its reads and writes through a small `ITempoStorage`:
- `FileTempoStorage`, the default, keeps today's file, temp-file move and `.bak` under `LocalApplicationData/Tempo`. Android and iOS map that folder into the app's sandbox.
- The Browser head's `BrowserTempoStorage` keeps the data in a `tempo` key and the corrupt-data backup in a `tempo.bak` key. It reaches `localStorage` through `[JSImport]`.
- A head passes its storage to `App` before start, as Desktop does today with `App.Launch`.

**What does not change.** The layouts, the 720 DIP breakpoint (width only), the motion, the view models, the timer model and the sample data stay as they are. Launch options stay on Desktop; the other heads start with the defaults. A session that ends while the app is in the background completes when the app comes back (the wall clock is the truth, ARCHITECTURE §5). Nothing in the library changes. If a head shows a library bug, the bug goes to the backlog under its spec.

## Decisions

These go to ARCHITECTURE §5 (Tempo). No spec owns sample code.

- **Shared project plus four heads, as in the gallery.** This replaces "One Desktop head, responsive by width". Reason: the compact arrangement already covers phone widths, and the heads are where Browser, Android and iOS get exercised by an app rather than by single-feature pages. Following the gallery's layout means nobody has to learn a second one. Rejected:
  - multi-targeting the one project (the head-specific code would sit behind `#if`, and every build would need the mobile workloads);
  - a Browser head only (it leaves touch hardware and system insets unexercised).
- **The shared project keeps the `samples/Tempo/Tempo/` path and the `Tempo` name.** Reason: the tests, the ARCHITECTURE and BACKLOG paths, and the open change proposals all point into it; only the run commands move to `Tempo.Desktop`. Rejected: renaming it to a `.Shared` project, which would rewrite every path for no behavior.
- **`MainView` owns the shortcuts, but installs them on its top level.** On attach it adds its key bindings and its tunnel Space handler to the `TopLevel`, and removes them on detach. Reason: they must work while nothing has focus. That was why they sat on the window, and a single-view top level is not a `Window`. Rejected: bindings on `MainView` itself (dead until something inside has focus).
- **The off-window look keys on a `windowed` class, not on the top level's type.** `MainWindow` writes `<v:MainView Classes="windowed" />`, and the styles test `v|MainView.windowed`. Reason: headless tests can host a `MainView` without the class in a plain `Window` and so cover the single-view styles. A `Window` selector could be tested only on a device. Rejected: selecting on `Window v|MainView`, and setting a flag from each head.
- **Safe-area insets pad the bars, not the whole view.** Reason: the accent backdrop belongs under the status bar and the home indicator, as it fills the window today. Padding the root would leave bands of plain base color at both edges. Rejected: one `Padding` on `MainView`.
- **The breakpoint stays on width.** A landscape phone wider than 720 DIP gets the wide arrangement, with the Focus dial sized from the page's height (it already sizes from both dimensions). Reason: one rule that tests and `--size` both reach. A landscape phone's height fits the wide layout, and Stats scrolls. Rejected: a height or aspect rule, which nothing here has needed yet; if the visual check finds the wide layout cramped, it goes to the backlog.
- **Back closes a surface, then goes back to Focus, then leaves the app.** Back is handled only if nothing handled it first, so an open modal or sheet is closed by the bridge's own `Dismissal` handler. Otherwise, back on the Tasks or Stats page goes to Focus, and on Focus it goes unhandled, so the platform puts the app in the background. Reason: it matches the bottom bar's model, where Focus is home. Rejected: back walking a page history (three pages do not need one).
- **The browser store is `localStorage` through `[JSImport]`.** Reason: it survives reloads, needs no package, and is trim- and AOT-clean, while the JSON stays well under the usual 5 MB quota. Rejected: IndexedDB (an async JS layer for one small document), and leaving the in-memory file system (data lost on reload).
- **Flush on backgrounding, not on every change.** Reason: the store already saves after every change through a short debounce, so the only data at risk is a save still pending when the OS kills a backgrounded app. Rejected: dropping the debounce (it would write once per keystroke in the notes field).
- **iOS is compile-only here.** It builds for `iossimulator-x64` and runs on a macOS host, like the gallery's head (BACKLOG [PLATFORM]). Reason: there is no macOS build host in the loop. CI stays Windows-only and keeps building by project name. The Browser and Android heads join the gallery's in the backlog's scheduled-workload item. Rejected: adding the mobile workloads to `build-test-pack` (ARCHITECTURE §4 already rejects that).

## Spec changes

None. No spec owns sample code, so ARCHITECTURE §5 (Tempo) takes the decisions above:
- "One Desktop head, responsive by width" is replaced by the head layout.
- The decisions on reduced motion ("`MainWindow` binds `presto:Motion.Preference`"), Space and the plain-key shortcuts name `MainView` instead of `MainWindow`.
- The window-material decision says it applies to the `windowed` view only.
- The `tests/Tempo.Tests` decision adds the single-view tests.

## Tasks

Start after the open Tempo-touching proposals (`motion-state-composition`, `filtered-motion-lists`, `exit-batches`) have landed, since they edit `MainWindow`, `ShellView` and the run commands.

1. **Split out `Tempo.Desktop`.**
   - Turn `Tempo.csproj` into a library: drop `OutputType`, `ApplicationManifest` and `Avalonia.Desktop`.
   - Move `Program.cs` and `app.manifest` into `samples/Tempo/Tempo.Desktop/`. Its csproj mirrors `PrestoGallery.Desktop`.
   - Update `MonacoLabs.Presto.slnx` (a `/samples/Tempo/` folder with all five projects), the CLAUDE.md layout row and commands, the README's Running section, and the NativeAOT publish command (`samples/Tempo/Tempo.Desktop`).
   - Tests: `Tempo.Tests` builds and passes unchanged. A NativeAOT publish of `Tempo.Desktop` starts with and without `--skip-intro`.
2. **`MainView`.**
   - Create `Views/MainView` and move the intro swap, the shortcuts, the Space handler and the `Motion.Preference` binding into it, installed on its `TopLevel` at attach.
   - `MainWindow` hosts it with the `windowed` class and keeps placement, `--size`, the window hints and the close flush.
   - `App` builds the view model once and shows it under each lifetime.
   - Move `Tempo.axaml`'s `Window` font and background setters to `v|MainView`, and the caption clearance to an element shown only under `v|MainView.windowed`.
   - Tests: the existing window tests pass through `MainWindow`, where tests that read `window.Content` read the `MainView`'s instead.
   - New tests on a `MainView` without the class, hosted in a plain headless `Window`:
     - N, Delete, Ctrl+, and Space work while nothing has focus, and N and Space do not fire while typing in the add box;
     - the intro hands off to the shell;
     - the root paints the opaque base;
     - the caption clearance is hidden;
     - detaching the view removes its key bindings from the top level.
3. **Safe-area insets.**
   - `MainView` (off-window only) reads the top level's insets manager when it is attached, and follows its safe-area change event.
   - It passes the insets to `ShellView` as a `Thickness`, which pads the title bar, the bottom bar and the panels. The toasts and the sheets' bottom detents sit above the bottom inset.
   - Tests: setting the `Thickness` on a compact shell moves the bottom bar up and the title bar's content down by the insets, and the backdrop still fills the view.
4. **Back.**
   - `MainViewModel.GoBack()` returns false on Focus and otherwise navigates to Focus with `IsBack` set.
   - `MainView` calls it from the top level's back request when the request is not already handled.
   - Tests: `GoBack` from Tasks and from Stats lands on Focus and plays the back transition; from Focus it returns false; with the settings sheet open, a back request that the bridge's dismissal handles leaves the page unchanged.
5. **`ITempoStorage`.**
   - Extract the file I/O from `TempoStore` into `FileTempoStorage`; behavior is unchanged.
   - `TempoStore` takes an optional `ITempoStorage`, and `App` takes one from its head (`App.Storage`, file storage by default).
   - Tests: `TempoStoreTests` pass unchanged over the file storage. Over an in-memory `ITempoStorage`, a missing document seeds, a corrupt document is backed up and starts empty, and a save round-trips.
6. **Flush on backgrounding.**
   - `App` subscribes to the activatable lifetime's deactivation, when it has one, and calls `FlushAsync` when the app goes to the background.
   - Tests: an `App`-free test raises the handler `App` uses on a view model with a pending debounced save and finds the store written.
7. **Browser head.**
   - Add `Tempo.Browser`, mirroring `PrestoGallery.Browser`: `runtimeconfig.template.json`, `AssemblyInfo`, a `wwwroot` whose `body` and splash use Tempo's base color and title, and `main.js`.
   - Add `BrowserTempoStorage` over `localStorage`.
   - If hiding the tab does not raise the lifetime's deactivation, `main.js` calls an exported `Flush` on `visibilitychange` to hidden.
   - Check: a trimmed Release publish loads, and Tempo's data survives a reload.
8. **Android head.**
   - Add `Tempo.Android`, mirroring `PrestoGallery.Android`: `Application` with `UsePresto` and `WithInterFont`, a `MainActivity` that handles orientation, screen size and UI-mode changes itself, the splash, an icon, and Tempo's base color for the splash and window background.
   - Show it edge to edge, so the insets from task 3 apply.
9. **iOS head.**
   - Add `Tempo.iOS`, mirroring `PrestoGallery.iOS`: `AppDelegate` with `UsePresto` and `WithInterFont`, `Main`, `Info.plist` (display name Tempo, a `com.monacolabs.tempo` bundle id, all orientations) and the launch screen in Tempo's base color.
   - Check: it compiles for `iossimulator-x64`.
10. **Docs.**
    - README: lead with "a focus timer for desktop, browser and phone"; add run commands per head; say where data lives on each platform (`localStorage` in the browser).
    - ARCHITECTURE §5: apply the decisions above.
    - BACKLOG: add the Tempo heads to the scheduled-workload [PROJECT] item and the iOS-simulator [PLATFORM] item.
11. **Visual checks.**
    - **Desktop:** `dotnet run --project samples/Tempo/Tempo.Desktop` looks and behaves as before, wide and at `--size 360x780`. Check Mica, the title bar, the caption buttons, fullscreen exit, and that placement is restored. DevTools screenshots.
    - **Browser (Chrome):**
      - the intro, a session, a tag filter and the clear;
      - data survives a reload;
      - a narrow window gets the compact layout;
      - the `⋯` row button and the other icon glyphs render with the bundled Inter;
      - hiding the tab pauses the engine and the countdown keeps time (BRIDGE-INIT).
    - **Android (emulator, or a device if one is at hand):**
      - the intro, the bottom bar above the gesture bar, and the title bar below the status bar;
      - the touch reorder hold, swipes, the bottom sheets and the confirm hold;
      - back from Stats goes to Focus, back from Focus leaves the app;
      - backgrounding mid-session, then coming back after the session's end time, shows it completed;
      - the soft keyboard doesn't cover the add box;
      - rotation to landscape gets the wide layout;
      - the blobs' magnetism and the hover motions do nothing odd under touch.
    - Each finding goes to the backlog under its spec or `[SAMPLE]`.

## Open questions

None.
