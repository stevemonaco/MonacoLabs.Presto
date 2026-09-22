using System.Collections.Generic;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using PrestoSample.Pages;

namespace PrestoSample.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public IReadOnlyList<GalleryPage> Pages { get; } =
    [
        new("Timer", "createTimer: a one second timer looping at 30 fps, reporting its current time and loop count.",
            () => new TimerPage()),
        new("Animation basics", "animate: transform shorthands, opacity and a background colour on one control, eased and alternating on loop.",
            () => new AnimationBasicsPage()),
        new("Value types", "Every value the tween parser accepts: numbers, unit strings, relative values, from-to arrays, colours, the codec-backed Avalonia struct types, a typed AvaloniaProperty and per-target function values.",
            () => new ValueTypesPage()),
        new("Keyframes", "Duration-based and percentage-based keyframes, per-frame duration, delay and ease, and a playback ease over the whole sequence.",
            () => new KeyframesPage()),
        new("Playback settings", "Duration, delay, loops, loop delay, alternate, reversed and autoplay rebuild the animation; frame rate and playback rate retune the running one.",
            () => new PlaybackSettingsPage()),
        new("Callbacks", "Every callback an animation fires, in the order one frame fires them, with a counter each; plus awaiting the animation and its Then() callback.",
            () => new CallbacksPage()),
        new("Controls", "Play, pause, resume, restart, reverse, alternate, seek, stretch, complete, cancel, revert and reset, with the animation's state read back live.",
            () => new ControlsPage()),
        new("Timeline", "createTimeline: children added at absolute, appended, relative, previous-child and label positions, a positioned timer, .Call, .Sync and a scrub slider.",
            () => new TimelinePage()),
        new("Stagger", "stagger: value and time staggers, ranges, start, total, ease distribution, jitter, modifier, a named grid against the measured auto-grid, and a stagger positioning timeline children.",
            () => new StaggerPage()),
        new("Eases", "Every ease plotted from its delegate next to a dot moving through it: the power, back and elastic families, cubic bezier, steps, irregular, linear stops, springs and the string parser.",
            () => new EasesPage()),
    ];

    [ObservableProperty]
    public partial GalleryPage? SelectedPage { get; set; }

    [ObservableProperty]
    public partial Control? CurrentPage { get; set; }

    public MainViewModel() => SelectedPage = Pages[0];

    partial void OnSelectedPageChanged(GalleryPage? value) => CurrentPage = value?.Create();
}
