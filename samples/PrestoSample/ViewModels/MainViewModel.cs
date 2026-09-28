using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using PrestoSample.Builder;
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
            new("Animation basics", "animate: transform shorthands, opacity and a background colour on one control, eased and alternating on loop. It's a real Button: click it while it moves and watch the like counter go up.",
                () => new AnimationBasicsPage()),
            new("Value types", "Every value the tween parser accepts: numbers, unit strings, relative values, from-to arrays, colours, the codec-backed Avalonia struct types, a typed AvaloniaProperty and per-target function values. The targets are real Buttons, a ProgressBar and a profile card whose border, corners and avatar brush are its own properties; hover or click them mid-flight.",
                () => new ValueTypesPage()),
            new("Keyframes", "Duration-based and percentage-based keyframes, per-frame duration, delay and ease, and a playback ease over the whole sequence. The travellers are real Buttons and a ToggleSwitch: flip the switch while it rides the keyframe path.",
                () => new KeyframesPage()),
            new("Playback settings", "Duration, delay, loops, loop delay, alternate, reversed and autoplay rebuild the animation; frame rate and playback rate retune the running one. The target is a real Button, so click it while it moves: drop the frame rate to 5 fps and it still counts every click.",
                () => new PlaybackSettingsPage()),
            new("Callbacks", "Every callback an animation fires, in the order one frame fires them, with a counter each; plus awaiting the animation and its Then() callback. The target is a real Button: click it mid-flight and its own counter goes up while the callback counters keep ticking.",
                () => new CallbacksPage()),
            new("Controls", "Play, pause, resume, restart, reverse, alternate, seek, stretch, complete, cancel, revert and reset, with the animation's state read back live. The target is a real Button, so click it while you scrub or reverse it; Revert hands it back to its own style.",
                () => new ControlsPage()),
            new("Timeline", "createTimeline: children added at absolute, appended, relative, previous-child and label positions, a positioned timer, .Call, .Sync and a scrub slider. Each lane moves a real control, a Button, ToggleSwitch, Slider, TextBox or CheckBox: pause the timeline and use them where they stopped.",
                () => new TimelinePage()),
            new("Stagger", "stagger: value and time staggers, ranges, start, total, ease distribution, jitter, modifier, a named grid against the measured auto-grid, and a stagger positioning timeline children.",
                () => new StaggerPage()),
            new("Eases", "Every ease plotted from its delegate next to a dot moving through it: the power, back and elastic families, cubic bezier, steps, irregular, linear stops, springs and the string parser.",
                () => new EasesPage()),
            new("Shapes", "svg.createDrawable and svg.createMotionPath: a stroke drawn onto a Path through its \"Draw\" property, and a control riding a second path's contour with X, Y and Rotate sampled from it.",
                () => new ShapesPage()),
            new("Text", "splitText: a headline taken apart into per-line and per-character controls, with a staggered effect that re-runs over the new pieces whenever the width changes.",
                () => new TextPage()),
            new("Draggable", "createDraggable: a spring-released card bounded by its container, a grid-snapped card and an x-only rail dragged by its handle, with the first one's X, Y, progress and callbacks read back live. Drag a card by its body, then tick its CheckBox: the CheckBox keeps its own clicks and never starts a drag.",
                () => new DraggablePage()),
            new("Scroll", "onScroll: a card revealed by an animation the observer plays and pauses, a card watching named thresholds, and a timeline scrubbed by the scroll position in sync mode, with progress, velocity and direction read back live.",
                () => new ScrollPage()),
            new("Animatable", "createAnimatable: a grid of dots following the pointer, each property registered with its own duration — X and Y from a centre-out stagger, Rotate written through with none — beside a single dot easing after it, both created in a scope that reverts them when you navigate away.",
                () => new AnimatablePage()),
            new("Compositor", "Both panels play the same rings and comet: the left is a CompositorScene ticked and drawn on the render thread, the right is real Ellipses animated on the UI thread, and the row below is a real Button, card, gear and ToggleSwitch whose animations were compiled onto the compositor. Press Freeze and watch the UI panel, its fps and the ProgressBar stop dead while everything else keeps moving and the scene counts how long the UI thread has been frozen. Pause, Resume, Reverse and the speed slider drive the compositor controls.",
                () => new CompositorPage()),
            new("Layout", "AnimateLayout and AnimateSharedElement: a grid of titled icon cards that shuffles, gains and loses cards, each easing into its new cell as FLIP played by the render thread, beside a panel that grows under PositionAndSize and a square that moves between two containers. Try Shuffle with slow on, then Freeze the UI thread and watch the cards keep easing.",
                () => new LayoutPage())),
        Section("Showcase",
            new GalleryPage("Title sequence", "One timeline builds a whole title card: a monogram draws itself and morphs into the mark, \"PRESTO\" staggers in from the centre, a subtitle scrambles into place and three cards fade up. Try the SEEK buttons to jump to a beat by its label, and scrub, reverse or stretch the sequence from the transport bar.",
                () => new TitleSequencePage()),
            new GalleryPage("Morph lab", "The whole Shapes surface at once: a path looping through six geometries as a chain of MorphTo children with the ease and the sampling precision live, so low precision is visibly polygonal; a signature that writes itself and erases through keyframed \"Draw\" values; and a marker riding a track's contour with its rotation taken from the tangent, followed by five more strung out behind it at increasing motion path offsets, each supplied per index by a function value.",
                () => new MorphLabPage()),
            new GalleryPage("Particle field", "Thousands of plain objects below the canvas: particles laid out on a grid and drifting on one animation with a centre-out grid stagger, ticked and drawn by the scene's own engine on the render thread, parting around the pointer through a damped push and bursting from each click as a spring that blends onto the drift rather than replacing it. The count slider walks the field from 500 to 20 000, and freezing the dispatcher for two seconds leaves the fps the scene draws inside itself climbing while the UI-thread one beside the slider stalls.",
                () => new ParticleFieldPage()),
            new GalleryPage("Card gallery", "Opening the page plays one wave of cards rising into place from the top-left corner. Filter by a chip, flip the sort or the grid/list switch, or press Drop mid-reflow, and watch every card ease from where it was drawn into its new slot on the render thread. Click a card to lift it into the detail pane, then click it again, even mid-flight, to send it back.",
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
                () => new TypographyPage()),
            new GalleryPage("Micro-interactions", "Six everyday UI patterns, each on real controls: a submit button that turns into a spinner and a check, a like with a spring pop, a TabStrip underline that stretches between tabs, an accordion whose sections reflow, toasts and a validated TextBox. Press Notify a few times, hover a toast to hold its timer or swipe it away, and submit the email field without an @. Every tile runs on the same Animate, Animatable, Timer, Draggable and AnimateLayout calls as the rest of the gallery.",
                () => new MicroInteractionsPage()),
            new GalleryPage("Loaders", "Nine loading indicators in the anime.js homepage spirit, each a single Animate or timeline call printed beneath it: staggers, a centre-out grid wave, a MorphTo blob, a Draw ring, squash keyframes and a TextModifier counter. Drag the speed slider to retune every loader's own Speed while the rest of the gallery keeps its pace, and flip Pause all to freeze them mid-motion.",
                () => new LoadersPage()),
            new GalleryPage("Carousel", "Five slides on an x-only draggable that snaps to the slide width and springs home on release. Drag one halfway and watch its background, art and title slide at different rates; the arrows and the Left/Right keys spring to a slide too. The ring around the active dot draws the four-second autoplay, which holds while the pointer is over the strip.",
                () => new Showcase.Pages.CarouselPage()),
            new GalleryPage("Connected", "Twelve album cards; click one and its art and title fly into the detail view on transitions of their own, 560 and 460 ms long, so the title lands first while the other cards scatter away from it on one staggered compositor animation. Press Back or Escape at any point, or click the card again while it closes, and every piece reverses from where it is drawn.",
                () => new ConnectedPage()),
            new GalleryPage("Connected navigation", "A connected animation between two views, as the Community Toolkit does between pages: scroll the gallery, click a destination, and it is swapped for a detail view built from scratch while the photo arcs and the city name flies into place on the render thread, the two views meeting only through prepared keys. The text, facts and buttons ride in behind them on one staggered compositor builder. Press Back or Escape, even mid-flight, to fly them home into the scrolled list as the other cards ripple back in, and turn on Block the UI thread to see the flights land while the dispatcher sleeps.",
                () => new ConnectedNavPage()),
            new GalleryPage("Connected XAML", "The same destinations with no animation code at all: a view model swaps a gallery for a detail page through DataTemplates, and the views' XAML carries the rest. Connected.Key marks the photo and city name as the same thing in both views, Connected.Motion gives the photo its arc both ways, and Connected.Anchor brings the detail text in behind the photo's flight. Scroll the list, open a destination and go back, then flip the switch to host the pages in a cross-fading TransitioningContentControl, where the two views overlap and the photo flies from the live one. The XAML that does it is printed beneath the stage.",
                () => new ConnectedXamlPage()),
            new GalleryPage("Bloom navigation", "Four tabs, each with its own hue. Click one and a circle of its colour grows from the pointer, or from the tab's centre when you pick it with the keyboard, until it covers the farthest corner; one timeline swaps the content under it at 65% and fades it while the new tiles rise in, as the underline, labels and header follow. Click tabs quickly and the blooms stack, each clearing the ones beneath it as it finishes. The bloom's Scale runs on the UI thread so it can share the timeline with the swap.",
                () => new BloomNavPage()),
            new GalleryPage("Gestures", "Three draggables with their live velocity. Pull the feed down: it stiffens as it goes, springs home from a short pull and holds from a long one while the arrow turns into a spinner, three new rows slide in and the list springs back. Swipe an inbox row either way to reveal archive or delete; past 40% the icon pops and the release carries the row off and collapses its gap, and Undo brings the last one back from the side it left. Pull the curtain down by its handle, or tap the handle to step it closed, half and full: it rests at the nearest of the three, and the content under it sinks, shrinks and dims in step, springs included.",
                () => new GesturesPage()),
            new GalleryPage("Liquid", "A tab bar whose pill stretches: click a tab and the pill's leading edge darts there in 300 ms while the trailing edge follows 70 ms later on a slower ease, squashing as it goes, both edges plain doubles on one animation with per-property tween params. Click across the bar quickly and it retargets from wherever it is without snapping. Press the gooey button to spring three actions out of it and again, even mid-flight, to pull them back in: a single Path rebuilt every frame joins each one to the button with a metaball neck that thins and breaks as it leaves.",
                () => new LiquidPage()),
            new GalleryPage("Swipe deck", "Twenty destinations dealt three at a time. Drag the top card: it tilts about its bottom edge, stamps LIKE or NOPE on the side it travels, and lifts the two cards beneath it as it goes. Let go short of 110 px and it springs home; drag past it, or flick it in any direction, and it flies off along its throw while the next card is promoted onto a draggable of its own. The buttons dismiss the same way, and Undo flies the last card back to the top from where it left.",
                () => new SwipeDeckPage()),
            new GalleryPage("Chart morph", "Eight data points, four charts. Switch between Bars, Donut, Scatter and Line and every point morphs its own Path into its next shape with MorphTo, staggered from the centre (from the first wedge for the donut), keeping its colour the whole way; the axis labels count to the new scale and the gridlines fade out for the donut. In Line the stroke draws itself once the markers land and undraws before any morph away. Press Shuffle or switch again mid-morph and each point carries on from the shape it is drawn as, and hover a point to lift it and read its values.",
                () => new ChartMorphPage()),
            new GalleryPage("Stress bench", "Up to 20 000 squares animated three ways: Presto animations on the UI thread, a compositor animation per visual, or one compositor scene drawing them all. Raise the count and switch modes to see where the UI frame times in the graph, the fps readouts and the gen-0 count give way. The graph is itself drawn on the render thread, so a stalled UI thread shows as a coral bar that keeps growing, and the worker switch adds a third engine on a thread of its own.",
                () => new StressBenchPage())),
        Section("Builder",
            new GalleryPage("Builder basics", "The Animation basics like-button spelled with the fluent builder: Presto.Build() chains the transform shorthands, opacity, a Palette brush and the playback settings, and the code beneath the stage is the exact call that ran, captured at compile time. It's a real Button, so click it while it moves and watch the counter go up; Replay restarts it.",
                () => new BuilderBasicsPage()),
            new GalleryPage("Properties and keyframes", "Three builder stages, each with the code that ran beneath it: one property with its own from, to and ease beside another with its own duration and delay; a ball bouncing through per-property keyframes above a square following animation-level keyframes at 0, 30 and 100 percent; and a card whose Palette background, CornerRadius and BorderThickness are typed values, the last set through any AvaloniaProperty. Replay restarts a stage.",
                () => new PropertiesPage()),
            new GalleryPage("Recipes", "A builder is a reusable recipe: one static Pop builder plays on whichever button you click, and the same recipe runs over the whole row with a stagger delay layered on its snapshot. The slider changes the recipe's duration, so the next play uses it while a pop already running keeps its own, because every Animate snapshots the builder.",
                () => new RecipesPage()),
            new GalleryPage("Timeline and compositor", "One fadeUp builder assembled into a timeline, each card added at its own position with Add(target, builder, position); the slider scrubs it and Play resumes. Below, a gear spun by AnimateOnCompositor(builder) runs on the render thread, so press Freeze and watch it keep turning while the timeline above stops dead for two seconds.",
                () => new TimelineBuilderPage()),
            new GalleryPage("XAML", "A builder attached from markup with Animate.OnAttached, three ways: a static recipe on a row of cards, a FadeUp markup extension whose Delay cascades a list in, and a binding to a view model that swaps an idle float for an alert shake when you flip the switch. Each panel prints its XAML and the C# it points at; Replay re-attaches all three.",
                () => new XamlPage()),
            new GalleryPage("Motion tokens", "Recipes built from a design system's tokens instead of literals: Enter, SlideIn and Pop are functions of a MotionTokens record, so picking Standard, Expressive or Reduced motion restyles every animation on the stage at once. Reduced motion zeroes the distances and stagger, leaving quick fades. The printed code is the token set, the recipes and the calls that ran.",
                () => new MotionTokensPage()),
            new GalleryPage("Playground", "Every control edits one step of a builder chain over six squares: transforms, duration, an ease by name, a stagger delay and where it spreads from, loop and alternate. The squares replay as you change it, and the code beneath is the C# for exactly that chain, ready to paste.",
                () => new PlaygroundPage()),
            new GalleryPage("Ripple grid", "Click any cell and a wave runs out from it across a 16 by 9 grid. Grid staggers do all the work: one sets each cell's delay by its distance from the click, and two more, as ranges inside the Scale and Rotate keyframes, dip and twist the nearest cells hardest. Click again mid-wave and it restarts from the new cell.",
                () => new RippleGridPage()),
            new GalleryPage("Submit flow", "A submit button driven by nine small recipes awaited in turn: it collapses into a circle, spins while a Presto timer stands in for the request, then turns aqua and draws a tick, or turns coral, shakes and draws a cross, before springing back. Tick \"Fail the next request\" to see the error branch.",
                () => new SubmitFlowPage()),
            new GalleryPage("Counters", "Four stat tiles counted by Text tweens, each with its own textModifier for currency, thousands or percentages and counting on from the value it shows. Below, an odometer rolls its digit strips with one Y tween whose target digit comes per strip from a function value, staggered from the last digit; tick Mechanical to add a Modifier that snaps each strip to whole digits. New data retargets both.",
                () => new CountersPage()),
            new GalleryPage("Toast stack", "An ItemsControl of toasts whose item template attaches its entrance and a five-second countdown bar from XAML with Animate.OnAttached. When the bar runs out, or you press ✕, the page collapses the toast's container and the stack closes up before the item leaves the collection. Press Notify a few times: past four toasts the oldest leaves on its own.",
                () => new ToastsPage()),
            new GalleryPage("Form states", "A sign-up form as a state machine: Idle, Validating, Error and Success. The card and badge bind Animate.OnAttached to builders the view model looks up by state, so every transition swaps what they play: a breathing pulse and spinning badge while checking, a shake on error, a spring pop on success, and a gentle float at rest.",
                () => new FormStatesPage()),
            new GalleryPage("Storyboard", "A keyframed hop built paused with Autoplay(false): Play, Restart or drag the slider to seek it, and read CurrentTime, IterationProgress, OnLoop calls and OnUpdate calls per second back live. Change PlaybackEase, FrameRate or PlaybackRate and it rebuilds at the same moment; drop to 6 fps and the updates per second follow.",
                () => new StoryboardPage())),
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
