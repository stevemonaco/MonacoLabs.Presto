# Launch screens for the Gallery and Tempo heads

## Why

The Browser, Android and iOS heads of both samples still carry the Avalonia templates' launch screens: "Powered by Avalonia" on a white or navy page on the web, and the Avalonia logo animating on a white (Gallery) or dark (Tempo) Android splash. None of them looks like the app that follows, and on the web nothing shows progress while about 20 MB of WASM downloads. For Tempo this matters most, because its intro is the brand moment: whatever the platform shows first should hand off to the intro's first frame without a pop.

## What

**Tempo** starts on the intro's own canvas everywhere. On Android and iOS the launch screen is plain `#121318`: no icon, nothing to vanish when the intro starts drawing. On the web the loading page draws the intro's empty track ring at the intro's size and position, and the download progress fills it in the Focus accent. When the download completes the full ring pulses gently until the runtime starts, then it fades as Avalonia takes over and the intro draws the track again in the same place.

**The Gallery** gets a Presto mark: a forward-leaning **P**, its bowl one stroke, with two speed lines behind it, white on a rounded badge in the gallery's accent gradient (`#2B579F` → `#4C8DFF`). The SVG source lives at `samples/PrestoGallery/PrestoGallery/Assets/presto-mark.svg`, and every other form is derived from it. On Android 12+ the splash icon is an animated vector of the mark: the P's stroke draws in, then the speed lines fade in (under the system's 1000 ms cap). Before Android 12 the static mark is centred on the window background. On iOS the launch screen centres the mark (a PNG at @2x/@3x) on the gallery's background. On the web the loading page shows the mark, a "Presto Gallery" wordmark, and a thin accent progress bar with a percentage. The page also takes the mark as its favicon, and its title becomes "Presto Gallery". Every Gallery launch screen uses the gallery's `Palette.Background` (`#0B0E14`), whatever the system theme, because the Gallery itself is always dark.

**Both web heads** gain Lucid's two tricks. The first is `onDownloadResourceProgress` writing `--wasm-load-percentage`/`--wasm-load-percentage-text` CSS variables, so the progress is pure CSS. The second is an unhandled `error`/`unhandledrejection` handler that alerts once and reloads. They stay single-threaded: `WasmEnableThreads` stays off.

What does not change: the desktop heads, the apps' own startup (Tempo's intro, the Gallery's first page), the Android/iOS app icons (still Avalonia's; a launcher icon is separate work), and `Tempo.Browser`'s `visibilitychange` flush.

## Decisions

- **Tempo's mobile launch screen is background-only.** The intro starts from an empty `#121318` canvas, so a plain launch screen is a seamless hand-off. A ring mark was rejected: Android 12+ clips splash icons to a 192 dp circle, smaller than the intro's 240 DIP ring, so it would jump in size and then vanish. On Android 12+, `windowSplashScreenAnimatedIcon` points at a transparent drawable, because the platform shows the launcher icon when the attribute is unset.
- **No translucent post-splash theme in Tempo.** Both heads' `MyTheme.Main` set `windowIsTranslucent`, which lets the launcher show through between the splash and Avalonia's first frame. Tempo's post-splash theme paints `#121318` instead, so the frame between the two matches both. The Gallery's does the same with `#0B0E14`.
- **Tempo's web ring mirrors the intro's layout, not just its ring.** The intro centres a stack: ring 240, spacing 28, the wordmark at 51, spacing 28, the tagline at 15. So the ring sits above the viewport's centre. The loading page lays out the same stack in CSS, with the wordmark and tagline as invisible placeholders, so the ring lands where the intro draws its track. Its track is `#2C2F38` (`TempoLine`) with an 8 px stroke. The fill is a `stroke-dasharray` arc in `TempoMotion.FocusAccent`, starting at 12 o'clock like the Focus ring.
- **The pulse after 100% covers the runtime's start.** Download progress reaches 100% before .NET and Avalonia have started, a visible pause on slow devices. A full ring that pulses (opacity 1 → 0.6, 1.6 s) says "still working" without a second progress source. Under `prefers-reduced-motion` it holds still.
- **The error reload has a loop guard.** Lucid reloads on every unhandled error, so a deploy that fails at startup alert-loops forever. The heads store the reload time in `sessionStorage`. When another error arrives within 10 s of a reload, the page shows the message on the loading screen instead of reloading again.
- **One SVG source for the mark.** The Android vector, the iOS PNGs and the favicon are derived from `presto-mark.svg`. Hand-made variants would drift. The derived PNGs are committed, so the iOS head needs no build-time rasteriser.
- **Single-threaded WASM.** Multithreaded WASM still deadlocks with today's Avalonia/.NET 10. It will be revisited with .NET 11 or the Avalonia fixes. Recorded in ARCHITECTURE §4 so the heads don't drift into it.

## Spec changes

No library spec changes: this is sample-only. ARCHITECTURE §5 changes:

- *Gallery*: add the Presto mark (source path, derived forms), and launch screens on the always-dark `Palette.Background`.
- *Tempo*: add a decision that the launch screens hand off to the intro's first frame (background-only on mobile; on the web, the intro's track ring as the download progress).
- §4: add "WASM heads run single-threaded" with its reason.

## Tasks

1. **Presto mark.** Add `presto-mark.svg` to the Gallery's assets. Derive from it the Android `drawable/presto_mark.xml` (static vector), the `drawable-v31/presto_mark_anim.xml` animated vector (stroke draws in via `trimPathEnd`, speed lines fade in), the iOS `LaunchMark@2x.png`/`@3x.png`, and the Browser `favicon.svg`.
2. **Gallery Android.** Colours become `#0B0E14` (drop `values-night`). `splash_screen.xml` centres `presto_mark`, and `values-v31/styles.xml` uses `presto_mark_anim`. The post-splash theme is opaque `#0B0E14`, and the activity label becomes "Presto Gallery". Check: install on the device and capture the splash (`adb shell screenrecord`), with no white frame before the first page.
3. **Gallery iOS.** `LaunchScreen.xib` gets a `#0B0E14` background and the mark centred at 128 pt, and the PNGs are included as `BundleResource`. Check: compile-only (no Mac).
4. **Gallery Browser.** The new `index.html`/`app.css`/`main.js` add the mark, the wordmark, the progress bar, the error reload with its guard, the favicon and the title. Check: `dotnet run` the head and load it in a browser with network throttling. The bar fills, the page fades into the Gallery, and a thrown error reloads once and then shows the message.
5. **Tempo Android.** The splash icon becomes transparent (`drawable/splash_blank.xml`), `splash_screen.xml` is colour-only, and the post-splash theme is opaque `#121318`. Check: on the device, the splash is plain and the intro starts with no flash or pop.
6. **Tempo iOS.** Confirm `LaunchScreen.xib` is plain `#121318`; it already is. Check: compile-only.
7. **Tempo Browser.** The loading page gets the intro-matched track ring and progress fill, the pulse after 100%, the reduced-motion hold, and the error reload with its guard. Check: throttled load. The ring fills and pulses, and the intro's track draws over the ring's place without a jump (compare one screenshot just before the fade with one at the intro's first frame).
8. **Docs.** ARCHITECTURE §4/§5 per Spec changes, both READMEs' head notes, and BACKLOG: add a launcher-icon item for both apps. Remove nothing.

## Open questions

None.
