# Changelog

All notable changes to the MonacoLabs.Presto packages are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the packages use
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0-preview.1] - 2026-10-22

The first public preview. All four packages ship together at the same version and depend on each other exactly.

### Core engine (`MonacoLabs.Presto`)

- An anime.js v4.5.0-derived engine with no UI dependency: [timers](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md#timers), animations, [timelines](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md#timelines) and full [playback control](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md#playback-control), with the timing model covered by suites ported from upstream.
- [Values](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md#values), [keyframes](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md#keyframes), [stagger](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md#stagger) and the full [ease set](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md#eases), including springs and a string parser.
- [Animatable](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md#animatable), [scope](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md#scope) and [inertia](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md#inertia).
- Trim- and NativeAOT-compatible; see [Trimming and NativeAOT](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md#trimming-and-nativeaot).

### Avalonia bridge (`MonacoLabs.Presto.Avalonia`)

- Controls as first-class targets through a typed fluent builder and from XAML; see [Avalonia setup](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md#avalonia-setup) and [From XAML](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md#from-xaml).
- [Draggable](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md#draggable) with drag to reorder, [onScroll](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md#onscroll), [shapes](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md#shapes) and [split text](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md#split-and-scramble-text).
- [Render-thread](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md#render-thread) animation, including layout and connected animations.
- [Lifecycle animations](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md#lifecycle-animations), [state motion](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md#state-motion) and [reduced motion](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md#reduced-motion) support.

### Controls (`MonacoLabs.Presto.Controls`, in the bridge package)

- Sheets, drawers, modals, toasts, pagers, pull to refresh, a progress ring, split-flap boards and a [marquee](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md#marquee).
- Image controls for [effects](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md#effects) (image filters, pixel transitions) and [sprite sheets and image sequences](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md#sprite-sheets-and-image-sequences).
- [Recipes](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md#recipes): motion tokens, [page transitions](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md#page-transitions), ripple, confetti and bursts.

### Lottie (`MonacoLabs.Presto.Avalonia.Lottie`)

- [Lottie](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md#lottie) playback through SkiaSharp.Skottie: a `LottieView` whose frame Presto animates.

### DevTools (`MonacoLabs.Presto.Avalonia.DevTools`)

- An in-app overlay that lists running animations and timers with a transport bar, a scrub track, tween tables, ease thumbnails and target highlighting.
