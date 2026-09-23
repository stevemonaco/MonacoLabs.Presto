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
        new("Shapes", "svg.createDrawable and svg.createMotionPath: a stroke drawn onto a Path through its \"Draw\" property, and a control riding a second path's contour with X, Y and Rotate sampled from it.",
            () => new ShapesPage()),
        new("Text", "splitText: a headline taken apart into per-line and per-character controls, with a staggered effect that re-runs over the new pieces whenever the width changes.",
            () => new TextPage()),
        new("Draggable", "createDraggable: a spring-released card bounded by its container, a grid-snapped card and an x-only rail dragged by its handle, with the first one's X, Y, progress and callbacks read back live.",
            () => new DraggablePage()),
        new("Scroll", "onScroll: a card revealed by an animation the observer plays and pauses, a card watching named thresholds, and a timeline scrubbed by the scroll position in sync mode, with progress, velocity and direction read back live.",
            () => new ScrollPage()),
        new("Animatable", "createAnimatable: a grid of dots following the pointer, each property registered with its own duration — X and Y from a centre-out stagger, Rotate written through with none — beside a single dot easing after it, both created in a scope that reverts them when you navigate away.",
            () => new AnimatablePage()),
        new("Compositor", "CompositorCanvas and AnimateOnCompositor: a ring of dots rippling out from the centre, drawn and ticked by its own engine on Avalonia's render thread, and a gear whose Rotate was compiled into a composition key frame animation, beside the identical ring over real controls on the UI thread and a button that freezes the dispatcher for two seconds so you can watch which ones keep moving.",
            () => new CompositorPage()),
        new("Layout", "AnimateLayout and AnimateSharedElement: a grid of numbered cards that shuffles, gains and loses cards, each easing into its new cell as FLIP inverted onto the composition transform and played by the render thread, beside a panel that grows under PositionAndSize and a square that moves between two containers.",
            () => new LayoutPage()),
    ];

    [ObservableProperty]
    public partial GalleryPage? SelectedPage { get; set; }

    [ObservableProperty]
    public partial Control? CurrentPage { get; set; }

    public MainViewModel() => SelectedPage = Pages[0];

    partial void OnSelectedPageChanged(GalleryPage? value) => CurrentPage = value?.Create();
}
