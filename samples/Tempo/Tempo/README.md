# Tempo

A focus timer for desktop, browser and phone, with a task list (Pomodoro style), built on Presto the way an outside app would be: one `using MonacoLabs.Presto;`, the controls in XAML, and every animation defined once as a builder and assigned from XAML. It runs on [Semi.Avalonia](https://github.com/irihitech/Semi.Avalonia) (Dark) rather than Fluent, so features that lean on Fluent's templates show up. It references only the library, never the gallery.

- **Tasks** (left): add with Enter, drag a row to reorder (on touch or pen, press and hold, then drag; let go without moving to open its details), swipe fully from the leading edge to complete or the trailing edge to delete (Del also deletes, with an Undo toast), double-click (or tap ⋯ in the compact layout) for a details drawer with notes, an estimate and a tag. Each row shows its tag as a colored chip, and the chips above the list (All, Writing, Code, Design, Admin, Personal) filter it; a task added under a filter takes its tag. Completed tasks fly into a collapsible Done group, and back to the end of the list when unchecked.
- **Focus** (right): a countdown ring whose one block of tabular digits morphs into each second, only the changed digits moving (the ring's glow pulses through the last ten), the mode (Focus, Short break, Long break), the active task, and start/pause (a pill reading Start focus, Start break or Resume that shrinks to a circle while running), skip and hold-to-abandon (let go early and it shakes). Breaks show a breathing guide. A finished session adds a dot to its task.
- **Stats**: today's minutes and sessions, the streak, a 7-day bar chart, a 12-week heatmap of focused minutes, a time-by-tag donut for Today, Week or All, and your best day, from the Focus | Stats switch in the title bar.
- **Settings** (title-bar button or Ctrl+,): a glass popover in four sections whose rows arrive in turn: Timer (− / + steppers for the durations and sessions before a long break), Behavior (auto-start breaks, the intro), Motion (System / Full / Reduced, with a dot that bounces under Full and fades under Reduced) and Data (restore sample data, clear all data). Both data buttons open a confirm dialog that grows in a circle from the button over a blurred app; the red button inside must be held for about 600 ms, and Escape cancels until you have pressed it, after which it shakes the dialog instead.

**Compact** (a window narrower than 720 DIP, down to 320): the title bar keeps only the wordmark, and a bottom bar switches between Tasks, Focus and Stats pages, with a pill gliding to the chosen item; its last button, Settings, opens the settings sheet over the current page. The task list fills its page with the tag chips scrolling sideways, the ring sizes to the width, and Stats stacks its cards in one scrolling column. Task details and settings open as bottom sheets (details at half or full height), and either one stays open while the window crosses the breakpoint, landing on its new edge or popover; the Undo toast sits above the bar. Widening the window from the Tasks page lands on Focus.

The window asks Windows 11 for Mica (then Acrylic) behind translucent glass cards over a slowly drifting backdrop; elsewhere the painted backdrop looks the same.

Space starts and pauses, N focuses the add box.

## Running

```powershell
dotnet run --project samples/Tempo/Tempo.Desktop
dotnet run --project samples/Tempo/Tempo.Desktop -- --skip-intro     # start on the main view
dotnet run --project samples/Tempo/Tempo.Desktop -- --size 360x780   # the compact layout
dotnet run --project samples/Tempo/Tempo.Browser
dotnet build -t:Run -f net10.0-android samples/Tempo/Tempo.Android
dotnet test --project tests/Tempo.Tests
```

`samples/Tempo/Tempo/` is the shared project; each head beside it (`Tempo.Desktop`, `Tempo.Browser`, `Tempo.Android`, `Tempo.iOS`) only starts it. The Browser and Android heads need their workloads (`wasm-tools`, `android`), and `Tempo.iOS` only compiles here: it runs on a macOS host. The launch options below are Desktop's; the other heads start with the defaults.

`--size WxH` sets the window size in DIPs for that run only: it wins over the saved size and the maximized state, and the saved placement is left as it was. An unknown argument or a malformed size exits with code 2. In Debug, F12 opens the Avalonia developer tools and the Presto DevTools overlay is attached.

## Data

Tasks, settings, the session log and the window placement are saved after every change: on Windows to one file, `%LocalAppData%/Tempo/tempo.json`; on Android and iOS to `Tempo/tempo.json` in the app sandbox's local application data; in the browser to the `tempo` key of `localStorage`. The first run, with no file yet, is populated with sample data: sixteen tagged tasks and eight weeks of session history, the same every time and dated back from today. A corrupt file starts empty and is kept as `tempo.json.bak` (in the browser, the `tempo.bak` key). Settings has **Clear all data** and **Restore sample data**, each behind a hold-to-confirm dialog: clearing empties everything but the settings: the task rows leave in turn from the top, the Stats bars drop to zero after them if Stats is showing, and the empty list's line fades in last; restoring brings the sample back with the rows arriving in turn. Deleting the file also starts over from the sample.

## Where the motion is

Every animation is a `public static readonly AnimationBuilder` in [Motion/TempoMotion.cs](Motion/TempoMotion.cs), each with its reduced-motion form, and XAML assigns them with `{x:Static m:TempoMotion.…}` (or a `{presto:…}` recipe where one says it exactly). In brief:

| Moment | Where |
|---|---|
| Rows enter, arrive, exit and reflow (also when a tag chip filters the list, through a `FilteredView`); a completed row flying into Done (`Animate.ItemGroup`); reorder lift (on touch or pen after a hold, which opens the row's details when let go in place: `Reorder.TouchHold`, `Reorder.HoldCommand`); swipe actions that commit or spring back (`Swipe.Reveal` off); selection glide; tag-chip indicator glide; checkbox and dot pops; strike line; the add box's plus turning into a check while it has text; an added task scrolling into view | `Views/TasksView.axaml`, `Views/TasksView.axaml.cs` |
| Ring sweep (`Follow`), last-ten-seconds glow pulse (`TempoMotion.FinalPulse`), mode label, play-to-pause morph with a half-turn (`TempoMotion.IconToPause`), a halo pinging from the start pill (`TempoMotion.StartPing`), start/pause pill resizing to a circle (`Animate.ContentSize`), skip's and stop's glass discs lighting on hover, skip's glyph opening into a fast-forward on hover (`TempoMotion.SkipToForward`), running breathe, breathing guide | `Views/FocusView.axaml`, `Styles/Tempo.axaml` |
| Changed digits of the one countdown block and the start/pause label morphing glyph by glyph (`Animate.DoOnChanged` with `{presto:MorphGlyphs}`), set at once under reduced motion (`ReducedMorph.Instant`); a burst from the ring when a focus session completes (`Animate.Cue` with `{presto:Burst}`) | `Views/FocusView.axaml` |
| Skip fast-forwarding with a sweep round its ring (`TempoMotion.SkipForward`, `SkipSweep`); hold-to-abandon shaking when let go early (`TempoMotion.AbandonShake`), and on confirm crossing its square out as the ring collapses (`AbandonCross`, `AbandonCollapse`) | `Views/FocusView.axaml.cs` |
| Mode accents, the backdrop gradient and blobs (OKLCH, following the shell's `Motion.State`), glass panels and cards, list dimming in breaks | `Styles/Tempo.axaml` |
| Backdrop blobs drifting on the compositor (`TempoMotion.BlobDrift`) and leaning towards the pointer (`Magnetic` with a slow `Magnetic.Spring`), swelling and brightening as it nears (`ShellView.Swell`), still under reduced motion; the settings gear and the add box's plus reaching for the pointer | `Views/ShellView.axaml`, `Views/ShellView.axaml.cs` |
| Totals flipping (`SplitFlap`), bars growing, tilting cards, the heatmap popping in outward from today, the donut sweeping in and easing between ranges (`Follow.Draw`), the best-day line fading in word by word | `Views/StatsView.axaml` |
| Page transitions (`TempoMotion.PageSwap`), switch pill, the details sheet as a right-edge drawer opened by a swipe from the edge (`Sheet.EdgeSwipe`), Undo toast, the settings sheet as a popover at the gear (`Presentation="Anchored"`) opening and closing, settings button's quarter turn on hover and while open; compact: the bottom bar's pill and icon pop (`TempoMotion.CheckPop`), its Settings button's quarter turn while the sheet is open, and both sheets turning into bottom sheets through `v|ShellView.compact` styles (chrome follows `:right`/`:bottom`) | `Views/ShellView.axaml`, `Styles/Tempo.axaml` |
| Edge shadows on the task list, the Done list and Stats as content scrolls under them, and the compact chip row's faded ends (`presto:Scroll.CueFor`, `CueEdge`, `FadeEdges`); every scroller easing the mouse wheel (`Scroll.Smooth`); every tooltip opening and closing (`TempoMotion.TipOpen`, `TipClose`) | `Styles/Tempo.axaml`, `Views/TasksView.axaml`, `Views/StatsView.axaml` |
| Confirm dialog: circle reveal from the pressed Data button over a blurred backdrop (`presto:Modal`; reduced, it shows at once and only the scrim fades) and its hold-to-confirm ring | `Views/ShellView.axaml`, `Views/ConfirmDialog.axaml` |
| The dialog shaking on Escape after a press (`TempoMotion.DialogShake`); the clear's rows leaving in turn (`TempoMotion.RowExit`, staggered), then the bars collapsing on their ghosts (`TempoMotion.BarCollapse` as their `Presence.Exit`), before the empty list's line fades in under a `Presence.Mode="Wait"` panel | `Views/ConfirmDialog.axaml.cs`, `Motion/TempoMotion.cs`, `Views/StatsView.axaml`, `Views/TasksView.axaml` |
| Settings rows arriving in turn (`TempoMotion.SettingsRowArrive`), stepper buttons pressing in, the motion preview dot (`TempoMotion.MotionPreview`) | `Views/SettingsFlyout.axaml`, `Styles/Tempo.axaml` |
| Stepper values morphing (`Animate.DoOnChanged` with `{presto:MorphGlyphs}`), set at once under reduced motion (`ReducedMorph.Instant`) | `Views/Stepper.axaml` |
| The intro and its handoff (the ring and wordmark fly into the app) | `Motion/TempoIntro.cs`, `Views/IntroView.axaml` |

A few calls have no XAML spelling and are made from code-behind: the skip's fast-forward and sweep, and the abandon ring's shake on an early release and its cross on confirm (`FocusView`), the intro's timeline and particle wordmark (`TempoIntro`), the backdrop drift on the compositor and the blobs' swell, the compact arrangement from the window width and moving the task list between the two arrangements (`ShellView`), the dialog's Escape shake (`ConfirmDialog`), and Space as the timer's key wherever focus is, with the other shortcuts installed on the top level (`MainView`). Wall-clock time drives the timer (`Model/FocusClock.cs` over a `TimeProvider`); Presto only presents it, so the countdown keeps going while the window is minimized.

The design decisions are in [ARCHITECTURE §5](../../../docs/ARCHITECTURE.md), and the friction found while building it under "Tempo findings" in [BACKLOG.md](../../../docs/BACKLOG.md).
