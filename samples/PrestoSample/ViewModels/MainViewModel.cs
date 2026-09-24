using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using PrestoSample.Pages;
using PrestoSample.Showcase.Pages;

namespace PrestoSample.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public IReadOnlyList<GallerySection> Sections { get; } =
    [
        Section("Reference",
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
                () => new LayoutPage())),
        Section("Showcase",
            new GalleryPage("Title sequence", "One timeline builds a whole title card: a monogram draws itself and morphs into the mark, \"PRESTO\" staggers in from the centre, a subtitle scrambles into place and three cards fade up. Try the SEEK buttons to jump to a beat by its label, and scrub, reverse or stretch the sequence from the transport bar.",
                () => new TitleSequencePage()),
            new GalleryPage("Morph lab", "The whole Shapes surface at once: a path looping through six geometries as a chain of MorphTo children with the ease and the sampling precision live, so low precision is visibly polygonal; a signature that writes itself and erases through keyframed \"Draw\" values; and a marker riding a track's contour with its rotation taken from the tangent, followed by five more strung out behind it at increasing motion path offsets, each supplied per index by a function value.",
                () => new MorphLabPage()),
            new GalleryPage("Particle field", "Thousands of plain objects below the canvas: particles laid out on a grid and drifting on one animation with a centre-out grid stagger, ticked and drawn by the scene's own engine on the render thread, parting around the pointer through a damped push and bursting from each click as a spring that blends onto the drift rather than replacing it. The count slider walks the field from 500 to 20 000, and freezing the dispatcher for two seconds leaves the fps the scene draws inside itself climbing while the UI-thread one beside the slider stalls.",
                () => new ParticleFieldPage()),
            new GalleryPage("Card gallery", "Twenty-four seeded records in an ObservableCollection, filtered by chips, sorted by a toggle and switched between a grid of cards and a list of rows, with every card handed AnimateLayout when it was built so each change reorders and resizes the whole field in one layout pass while the render thread eases the pixels after it; cards arriving scale and fade in on a staggered compositor animation and cards leaving play one whose OnComplete takes them out of the panel, and a button pulls a card straight out mid-reflow to see whether a transition in flight strands a transform. Clicking a card lifts it out of the field into a detail pane as a shared element and the field closes the gap behind it; clicking it again returns it to its cell from wherever it is drawn, halfway through the expand or not.",
                () => new GalleryFlipPage()),
            new GalleryPage("Board", "Three lanes of draggable cards. Drag one and watch the lane under it open a gap where it would land, following the card through the drag and the throw; a gentle drop fills the gap with nothing else moving. Fling a card to throw it a lane further, or at the bin to spin it off the board.",
                () => new BoardPage()),
            new GalleryPage("Dashboard", "Four counters, twelve bars, a donut and a sparkline, all retargeted where they stand: a counter is a Text tween whose TextModifier formats the number with Utils.RoundPad, a bar is its Height under a staggered animation, a donut slice is the pair of contour fractions CreateDrawable reveals, and the sparkline morphs its Data into the next dataset. A timer behind the live switch pushes a fresh round of seeded data every 800 ms, so the default replace composition is on show: each push reads the value on screen as its from and retires the tweens it overrides, and nothing snaps back to a starting value. The transport drives the reveal timeline that counts the whole board up from zero.",
                () => new DashboardPage()),
            new GalleryPage("Scroll story", "A long article driven entirely by where its ScrollViewer is: a hero whose three layers trail the scrollbar under a smoothed sync, three chapters played and paused at their own named enter and leave thresholds with every crossing and its direction reported to the rail, a pull-quote whose whole crossing is shaped by an eased sync, and a pinned section — an overlay the observer shows while its 1600px track passes — whose timeline is seeked frame by frame to the scroll position, panning a strip of panels and counting itself up. A rail across the top reads Progress off an observer watching the whole article and turns a vane through an Animatable from Velocity and Backward, and every observer is re-measured when the window resizes.",
                () => new ScrollStoryPage()),
            new GalleryPage("Interruption lab", "What happens when an animation is interrupted mid-flight. Click a pane or press Burst to retarget three pucks at once, one per composition mode, and compare their trails; then press Cancel newest mid-burst to see which modes were still running the older animations underneath. Sweep the pointer across the tiles under retarget and then restart to see which one jumps.",
                () => new InterruptionPage()),
            new GalleryPage("Typography", "The text module composed into four panels. Drag the width slider mid-reveal to re-wrap the paragraph without restarting it, and watch the headline swap its last word letter by letter. Sweep the pointer across MOTION to lift the letters nearest it while the terminal types, decodes and counts to 100 on a loop.",
                () => new TypographyPage())),
    ];

    [ObservableProperty]
    public partial GalleryPage? SelectedPage { get; set; }

    [ObservableProperty]
    public partial Control? CurrentPage { get; set; }

    public MainViewModel() => SelectedPage = Sections[0].Pages[0];

    partial void OnSelectedPageChanged(GalleryPage? value) => CurrentPage = value?.Create();

    private static GallerySection Section(string name, params GalleryPage[] pages) =>
        new(name, [.. pages.Select(page => page with { Section = name })]);
}
