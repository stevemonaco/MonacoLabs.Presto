# Tempo sample app

## Why

The gallery shows each feature on its own page, built by the people who wrote the library. Nothing shows Presto inside a real application: one design language, one view model, animations that carry meaning (state, progress, cause and effect) rather than demo for their own sake. Nothing exercises the library the way an outside consumer would: one `using MonacoLabs.Presto;`, the control tree in XAML, and the motion defined once as builders and assigned from XAML.

Tempo is that app: a desktop focus timer with a task list (Pomodoro-style). It is small enough to finish and useful every day. It touches nearly every Presto category: continuous motion (the countdown), discrete events (finishing a task or a session), list choreography, a global mode change, and celebration. As a second dogfooding surface it will surface friction the gallery hides, and those findings go to the backlog (ARCHITECTURE §6, "Demos never quietly grow the library").

## What

A new standalone Desktop app, `samples/Tempo/`. It is one WinExe project (`Tempo.csproj`) referencing `MonacoLabs.Presto.Avalonia`, themed with [Semi.Avalonia](https://github.com/irihitech/Semi.Avalonia) (dark variant) instead of Fluent. It does not reference the gallery or its kit (`ShowcasePage`, `Palette`, `Snippet`).

### The app

The app opens on a short intro (see "The intro" below), which hands off into a single window with a custom title bar and three regions:

- **Tasks** (left column). Today's tasks, each with a title, an estimate in sessions (a row of dots), and a done state. An inline "Add a task" box sits at the top. Completed tasks drop into a collapsible "Done" group at the bottom.
- **Focus** (right, the main area). A large ring counting down the current session, split-flap digits (`25:00`), the mode label (Focus, Short break, Long break), the active task's title, and the controls: start/pause, skip, and hold-to-abandon. During breaks the area shows a breathing guide.
- **Stats.** A second view, reached from a segmented switch in the title bar (Focus | Stats). It shows today's focused minutes and session count, a 7-day bar chart and the current streak.

Behavior:

- **Sessions.** Work is 25 minutes, a short break 5, a long break 15 after every 4 work sessions; all configurable. Starting a work session binds it to the selected task. Completing one adds a dot to that task. Breaks start automatically, or wait, depending on a setting.
- **Tasks.** Add (Enter), rename, reorder (drag a row; hold first on touch), complete (checkbox, or swipe from the leading edge), delete (swipe from the trailing edge, or Del, with an Undo toast), and open details (double-click) in a right-edge drawer with notes and an estimate stepper.
- **Settings.** A flyout from the title bar: durations, auto-start breaks, "Play intro at startup" (on by default), and motion (System / Full / Reduced).
- **Persistence.** Tasks, settings and the session log are saved as JSON under `%LocalAppData%/Tempo/`. The window restores its size and position.
- **Keyboard.** Space starts and pauses, N focuses the add box, Del deletes the selected task, Ctrl+, opens settings. All of these are `KeyBinding`s to view-model commands.

### The intro

A faux loading screen of about 2.7 s that plays at startup, built entirely with Presto (no Lottie). It feels premium because it is not a separate splash: it assembles the app's own ring and wordmark, then *becomes* the app, and those two elements fly into their places in the real UI. The data really does load behind it (`TempoStore.LoadAsync`), so the faux part is only the minimum duration.

| Time (ms) | Beat | Presto |
|---|---|---|
| 0–600 | A soft radial glow blooms out of the dark backdrop while its hue drifts coral → violet | scale + opacity, `Background` color keyframes in OKLCH |
| 150–900 | The focus ring's track draws on clockwise, then four cycle arcs (one per session to a long break) pop in, staggered, each in its mode hue | `Draw` with `outExpo`; arcs with `Stagger` and a `Spring` scale |
| 500–1500 | Particles stream in and form the wordmark "tempo" | `Recipes.Assemble` on the wordmark `TextBlock` (BRIDGE-PARTICLES, glyph points) |
| 1300–1900 | The tagline "one thing at a time" fades in word by word; a bright tick dot travels once around the ring, filling it with the focus accent behind it | `{presto:WordFade}`-style piece reveal; the motion path along the ring's geometry (`CreateMotionPath`) plus `Draw` on the fill arc |
| 1900–2300 | The arcs merge into the ring; a single glow pulse | arcs `Scale`/`Opacity` to the ring, `{presto:Glow}`-style pulse |
| 2300–2700 | Handoff: the intro swaps out for the main view. The ring flies into the Focus panel's ring and the wordmark into the title bar, while everything else fades through and the main view's entrance stagger starts | `Connected.Key="ring"` / `"wordmark"` on both views, with `Connected.Motion="{x:Static m:TempoMotion.IntroFlight}"`; `Presence.Enter` on the main view |

Rules:

- **Minimum and maximum.** The handoff waits for both the timeline and the load. If loading takes longer than the intro (unlikely), the ring keeps breathing until it finishes. The intro never extends the wait beyond the load plus its own length.
- **Skippable.** A click, Space, Enter or Esc completes the timeline at once (`Timeline.Complete()`) and hands off immediately.
- **Reduced motion.** The wordmark and ring fade in over 400 ms. There are no particles, no traveling dot and no flight; the main view fades through.
- **Off.** With "Play intro at startup" off, the app goes straight to the main view and its normal entrance.

### Where the motion is

Every animation is a `public static readonly AnimationBuilder` in one file, `Motion/TempoMotion.cs`, made with `Presto.Build()`, each with a `WhenReduced` form. XAML assigns them with `{x:Static m:TempoMotion.…}`. Where a `{presto:…}` recipe extension already says it exactly (`{presto:Reflow}`, `{presto:PageSharedAxis}`), XAML uses the extension instead.

| Moment | Feature | XAML spelling |
|---|---|---|
| Main view appears (after the intro) | `Presence.Enter` staggered over the task rows and focus panel (arrival batch, BRIDGE-PRESENCE-020) | `presto:Presence.Enter="{x:Static m:TempoMotion.RowEnter}"` |
| Add a task | `Presence.Arrive` on the new row; the rows below make room | `Presence.Arrive`, `presto:Animate.ItemLayout="{presto:Reflow}"` |
| Delete a task | `Presence.Exit` ghost, the gap closes, Undo toast | `Presence.Exit`, `ItemLayout`, `ToastHost` with `ToastHost.Countdown` |
| Swipe to complete or delete | `Swipe.Leading`/`Trailing` as `DataTemplate`s from a style, `Swipe.Command`, `Swipe.Dismiss` | style setters |
| Reorder | `Reorder.Enabled`, `Reorder.TouchHold`, `Reorder.Lift` | `Lift="{x:Static m:TempoMotion.RowLift}"` |
| Complete a task | A strike line draws across the title; the checkbox pops; the row moves to Done | `Follow.Draw="{Binding StrikeProgress}"`, `Animate.OnChanged` + `Watch`, `ItemLayout` |
| Session dots fill | The newest dot pops when a session lands on the task | `Animate.OnChanged="{x:Static m:TempoMotion.DotPop}"` |
| Select the active task | An indicator glides between rows | `Selection.Indicator`, `Selection.Motion` |
| Task details | Right-edge drawer | `<presto:Drawer IsOpen="{Binding IsDetailOpen}">` |
| Done group | Expands and collapses | `Accordion.Enabled` + `Accordion.Chevron`, or `Animate.ContentSize` |
| Countdown ring | Sweeps smoothly between view-model updates | `<presto:ProgressRing Value="{presto:Follow {Binding Progress}, Duration=250, Ease=linear}">` |
| Digits | Flip per changed cell | `<presto:SplitFlap Text="{Binding RemainingText}" Cells="5">` |
| Hold to abandon | Hold-to-confirm ring around the stop button | `ProgressRing HoldDuration="900"` + `Confirmed` → command |
| Start/pause button | Lift on hover, press-in, ripple; the play icon morphs to pause | `Motion.Hover="{presto:Lift}"`, `Motion.Press="{presto:PressIn}"`, `Ripple.Enabled`, `Motion.States` with `When="Running"` → `TempoMotion.IconToPause` |
| Running | The ring breathes slowly while a session runs | `Motion.States` `When="Running"` → `TempoMotion.RingBreathe` (loop, alternate) |
| Mode change | The accent recolors through OKLCH (focus coral → break aqua → long-break violet), the backdrop gradient shifts, and the panel's parts follow in a stagger | root `presto:Motion.State="{Binding Mode}"`; `MotionStates` in shared styles with `StaggerChildren`; builders using `Background(k => k.Color(…, ColorSpace.Oklch))` |
| Mode label | Slides on a shared axis when the mode changes | `TransitioningContentControl` with `PageTransition="{presto:PageSharedAxis Axis=Y}"` |
| Break | The task list dims and blurs back; a breathing guide ("In… Out…") scales on a 4 s loop | `Motion.States` `When="Break"` on the list; the guide's `Animate.OnAttached="{x:Static m:TempoMotion.Breathe}"` |
| Session complete | A burst from the ring, then a summary line rises in ("25 minutes on *Write proposal*") | `Recipes.Burst` (code, see Decisions); the summary's `Presence.Enter="{presto:LineRise}"` |
| Focus ⇄ Stats | Page transition; the switch's pill glides | `PageTransition="{x:Static m:TempoMotion.PageSwap}"` (`PageSharedAxis` with a mirrored back pair, so going back eases out too; ARCHITECTURE §6); `Selection.Indicator` on the segmented `ListBox` |
| Stats appear | Bars grow from the baseline in a stagger; totals flip in | `Presence.Enter="{x:Static m:TempoMotion.BarGrow}"`; totals on `<presto:SplitFlap>`, matching the countdown digits |
| Settings | Flyout opens with the popover motion | `presto:Popover.Open="{x:Static m:TempoMotion.FlyoutOpen}"` |
| Window cards | Subtle tilt on the stats cards | `Tilt.Enabled` |

### Structure

```
samples/Tempo/
  Tempo.csproj           WinExe, net10.0, Avalonia.Desktop, Semi.Avalonia, Inter, CommunityToolkit.Mvvm
  Program.cs             UsePlatformDetect().UsePresto().WithInterFont(); --skip-intro launch option
  App.axaml(.cs)         SemiTheme (Dark), styles, DevTools under DEBUG; reduced-motion setting applied here
  Motion/TempoMotion.cs  every AnimationBuilder in the app
  Motion/TempoIntro.cs   the intro timeline, laid out from TempoMotion builders
  Styles/Tempo.axaml     palette over Semi's tokens, control styles, the shared MotionStates for mode accents
  Views/                 MainWindow, IntroView, ShellView, TasksView, FocusView, StatsView, TaskDetail, SettingsFlyout
  ViewModels/            MainViewModel, TaskItemViewModel, FocusViewModel, StatsViewModel
  Model/                 TaskItem, SessionRecord, Settings, FocusMode, FocusClock, TempoStore
  README.md              what it is, how to run it, the motion map in brief
tests/Tempo.Tests/       headless tests (see Tasks)
```

Nothing in the library changes. Friction found while building Tempo goes to `docs/BACKLOG.md` as findings, as the "Library findings" task describes.

## Decisions

- **Tempo is standalone, not a gallery page.** It references only the library packages, never `PrestoSample`, and has its own palette and styles. Reason: it should read as an app someone built *with* Presto, and its code should be copyable as a whole. Rejected: a Showcase page, which would inherit the kit and the gallery's shell constraints.
- **Desktop only.** One project, no shared/heads split. Reason: desktop is the acceptance target, and a focus timer is a desktop app. The split can be added later if a Browser head is wanted.
- **Semi.Avalonia, not Fluent.** Tempo uses `SemiTheme` in its dark variant, and its own styles restyle the hero surfaces (the ring panel, task rows, title bar) over Semi's color tokens. Semi supplies the base controls, the flyout and the inputs. Reason: every gallery page runs on Fluent, so any template-coupled feature (selection indicator, popover motion, accordion chevron, submit button, ripple) that quietly depends on Fluent's internals would go unnoticed. A second theme tests that, and Semi (MIT, supports Avalonia ≥ 12.1) is a polished base with light/dark tokens. Theme transitions on properties Presto animates are cleared in Tempo's styles, so the two never fight over a property. Rejected: Fluent (tests nothing new); the Simple theme with fully custom styles (full control, but every control styled by hand, and still no third-party-theme coverage).
- **The intro is built with Presto, not Lottie.** Reason: it shows the library doing what the app does (draw, particles, motion paths, OKLCH color, connected flights), and it needs no asset with a license. Rejected: a Lottie file or a series of them (needs an asset, and plays a canned clip that cannot hand its elements off into the live UI).
- **The intro becomes the app.** The ring and wordmark carry `Connected.Key`s that match the Focus ring and the title-bar wordmark, so swapping `IntroView` for `ShellView` in the window's content flies them into place (BRIDGE-CONNECTED). Reason: a splash that fades out and a UI that fades in reads as two things; a handoff reads as one product. Rejected: a separate splash window (no shared-element flight across windows).
- **The intro is a code-built timeline.** `TempoIntro` lays out `TempoMotion` builders on one `Timeline` by position, and `IntroView` starts it on attach and awaits it alongside the load. Reason: a sequence with overlaps, a skip (`Complete()`) and an awaited end is what timelines are for, and XAML has no timeline spelling by design. It is also the only place Tempo shows timelines. Every step is still a named builder in `TempoMotion`.
- **The intro only adds a minimum duration.** The real load runs underneath; the handoff waits for both, a skip ends it at once, and a setting turns it off. Reason: a fake wait that cannot be skipped is the opposite of premium.
- **XAML owns the tree; `TempoMotion` owns the motion.** Every builder lives in `TempoMotion.cs` and is assigned from XAML. Code-behind may *trigger* a builder only where no XAML trigger exists, and never builds one inline. Reason: one place to read and tune all the motion, and it tests the "builders are the value lifecycle surfaces carry" decision (CORE-BUILDER) in a whole app. Recipe extensions are used where they say exactly what is meant, since those are XAML-assigned builders too.
- **The code-only exceptions are explicit.** These have no XAML spelling today (per the feature inventory):
  - particle effects (`Recipes.Burst` on session complete, `Recipes.Assemble` in the intro)
  - showing a toast (`ToastHost.Show`)
  - the reduced-motion flag (`Presto.Defaults.ReducedMotion`, `PrestoAvalonia.FollowSystemReducedMotion`)
  - the intro timeline (`TempoIntro`, above)

  Each is a call in a view's code-behind, reacting to a view-model event or the view's attach, and each is logged as a finding. Reason: the rule says to prefer XAML, not to bend the app around it. Rejected: app-side attached properties wrapping them, because that is the library growing quietly inside a sample.
- **Wall-clock time is the truth; Presto only presents it.** `FocusClock` keeps the session's start time and accumulated pause time through a `TimeProvider`. The view model refreshes `Remaining`/`Progress` from a 4 Hz `DispatcherTimer`, and the ring smooths between updates with `Follow`. Reason: the bridge sets `Engine.Hidden` and pauses the engine while the window is minimized (BRIDGE-INIT), and a focus timer must keep counting there. A `TimeProvider` also makes the clock testable. Rejected: a Presto `Timer` as the session clock.
- **The mode is a `Motion.State` on the root**, and accents are `MotionStates` in shared styles that descendants follow (BRIDGE-MOTION-041/045), staggered by `StaggerChildren`. Reason: one binding drives the whole window's mode change, with orchestration instead of per-element code. Colors interpolate in OKLCH so coral → aqua does not pass through gray.
- **Reduced motion is a first-class setting.** System (the default) follows the OS through `FollowSystemReducedMotion`; Full and Reduced set the flag. Every `TempoMotion` builder states its reduced form: motion becomes fades, loops stop, the burst is skipped, and the colors still change. Reason: the project-wide rule (ARCHITECTURE §5) applies to apps too, and a focus app's audience is likely to care.
- **MVVM with CommunityToolkit.Mvvm**, compiled bindings, `ObservableCollection` for the task list (so `Reorder` needs no command). Reason: the idiomatic Avalonia stack, already in central package management.
- **No assets beyond fonts.** Icons are `PathGeometry` resources, and the empty state is a `TextReveal` line, not a Lottie file. Reason: no licensing questions, and morphable icons need paths anyway.
- **Tempo's decisions live with Tempo.** On landing, the decisions above move into a short "Tempo" subsection of ARCHITECTURE §6 (rename §6 to "Samples"). `samples/Tempo/README.md` covers usage. Reason: no spec owns sample code, and §6 already holds the gallery's decisions.

## Spec changes

None. Tempo adds no public library types and changes no library behavior.

On landing:

- **ARCHITECTURE §6**: retitle it "Samples" and add a "Tempo" subsection with the Decisions above.
- **CLAUDE.md**: add a `samples/Tempo/` row to the Layout table and a `dotnet run --project samples/Tempo` line to Commands.
- **BACKLOG.md**: add the friction findings.

## Tasks

1. **Scaffold.** Create `samples/Tempo/Tempo.csproj` (package versions from `Directory.Packages.props`), `Program.cs`, `App.axaml` and an empty `MainWindow`, and add the project to `MonacoLabs.Presto.slnx` under `/samples/`. Add `Semi.Avalonia` (12.1.x) to `Directory.Packages.props` and apply `SemiTheme` (Dark) in `App.axaml`. Add the `--skip-intro` launch option. Create `tests/Tempo.Tests` (xunit v3, MTP, Avalonia.Headless, linking `CompositorClock.cs` as `PrestoSample.Tests` does) and add it under `/tests/`. *Check:* `dotnet build MonacoLabs.Presto.slnx`, and the window opens.
2. **Semi transition audit.** List the `Transitions` that Semi's templates and styles set on the controls Tempo animates (Button, ListBoxItem, ToggleButton, CheckBox, Expander, FlyoutPresenter, TextBox), and clear those on Presto-animated properties in `Styles/Tempo.axaml`. *Check (Desktop):* hover and press on a button, and select a task row, show only Presto's motion, with no double-easing or snap-back.
3. **Model and persistence.** Build `TaskItem`, `SessionRecord`, `Settings`, `FocusMode` and `FocusClock` (state machine: Idle → Work → Break, the long break every N sessions, pause/resume, skip, abandon), plus `TempoStore` (JSON load/save, tolerant of a missing or corrupt file). *Tests:* `FocusClockTests` (countdown under a fake `TimeProvider`, pause accounting, the long-break cadence, skip and abandon, keeps counting across a gap that simulates minimizing), `TempoStoreTests` (round-trip, a missing file gives defaults, a corrupt file gives defaults and is kept as `.bak`).
4. **View models.** Build `MainViewModel` (the Focus/Stats view, selection, settings, the motion setting), `TaskItemViewModel` (`StrikeProgress`, dots), `FocusViewModel` (`Remaining`, `RemainingText`, `Progress`, `Mode`, `IsRunning`, commands, and `SessionCompleted`/`TaskDeleted` events) and `StatsViewModel` (today, 7 days, streak). *Tests:* `MainViewModelTests` (add, complete, delete with undo, reorder persists; a completed session adds a dot to the bound task; stats aggregate the log).
5. **Styles and palette.** Write `Styles/Tempo.axaml`: dark palette, typography, the title bar, buttons, the task-row template and the shared mode `MotionStates`. Write `TempoMotion.cs` with every builder and its `WhenReduced`. *Tests:* `TempoMotionTests` (every public builder has a reduced form; every builder used as a `MotionState` validates, BRIDGE-MOTION-021…024).
6. **Tasks view.** Build the list with Presence enter/arrive/exit, `ItemLayout`, Reorder with a touch hold and lift, Swipe leading/trailing via a style with dismiss, the strike draw, dot pops, the selection indicator, the Done group, the details `Drawer` and the Undo `ToastHost`. *Check (Desktop):* add, reorder, swipe both ways, undo and details, with DevTools screenshots.
7. **Focus view.** Build the `ProgressRing` with `Follow`, the `SplitFlap` digits, the mode label transition, start/pause with the icon morph and micro-interactions, hold-to-abandon, the running breathe, the break dimming and breathing guide, and the completion burst and summary. *Check (Desktop):* a full work → break → work cycle with durations shortened in settings, with screenshots of each mode.
8. **Stats view and navigation.** Build the segmented switch with its indicator, the `PageSharedAxis` transition, staggered bars, totals and the streak. *Check (Desktop):* switch back and forth mid-session, and the bars replay on entry.
9. **Intro.** Build `IntroView` (backdrop glow, ring track, cycle arcs, wordmark, tagline, tick dot) and `TempoIntro` (the timeline from the beat table). Add the `Connected.Key`s on both views, the skip input, the wait for the load, and the reduced form. The window starts on `IntroView` and swaps to `ShellView` at the handoff. *Tests:* `IntroTests`: the full timeline lasts 2–3 s; the handoff waits for a slow load; a skip hands off at once; under reduced motion the intro lasts ≤ 500 ms and plays no particles; with the setting off, the window starts on `ShellView`. *Check (Desktop):* the intro at full and reduced motion, and the ring and wordmark landing exactly on their targets with no pop at the end of the flight.
10. **Settings and reduced motion.** Build the settings flyout with popover motion, durations, auto-start, the intro toggle and the motion setting (System/Full/Reduced), applied live. *Check (Desktop):* toggling to Reduced mid-session takes effect at the next transition, and nothing moves that shouldn't.
11. **Headless smoke.** *Tests:* `MainWindowTests` constructs `MainWindow`, attaches it, ticks through the intro and its handoff, an add, complete, delete and a mode change, and detaches it, in both motion modes, asserting no exceptions and that the end states hold (the row is gone, the accent is at the break color, the drawer is closed).
12. **Library findings.** List each friction point hit while building, at least the code-only exceptions and any feature that misbehaves on Semi's templates but not on Fluent's, in `docs/BACKLOG.md`, worded as findings with no fixes.
13. **Docs.** Write `samples/Tempo/README.md`. Update CLAUDE.md Layout and Commands, and ARCHITECTURE §6 as described in Spec changes. *Check:* `dotnet test --solution MonacoLabs.Presto.slnx` is green, including `SpecTests`/`SpecCoverageTests`, which must ignore the new projects.

## Open questions

- **Icon morph as a motion state.** `IconToPause` uses `PrestoAvalonia.MorphTo` (a function value) on `Path.Data` inside a `MotionState`. If state validation or the return to rest does not support geometry, fall back to cross-fading two paths, and log a finding.
- **ProgressRing fill per mode.** The ring's `Fill` is an `IBrush`. Confirm that a `MotionState` can recolor it through `Property("Fill", …)` with an OKLCH color keyframe, or whether only `Background`/`Foreground` have typed helpers (a finding either way).
- **Particles inside the intro timeline.** `Recipes.Assemble` returns a `ParticleEffect`, not a timeline child. The plan is to start it from a timeline callback at its position. Confirm that a skip can end it cleanly (`Stop`/`Cancel`) and that it lands the wordmark fully drawn; if a `ParticleEffect` cannot join a timeline's seek and complete, log a finding.
- **Connected flight on the first swap.** The handoff is the window's first content swap, onto a `ShellView` that has never been measured. Confirm that the Focus ring's target bounds are known when the flight starts. If not, attach `ShellView` hidden under the intro first, and log a finding.
