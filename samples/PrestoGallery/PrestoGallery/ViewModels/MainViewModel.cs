using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MonacoLabs.Presto;
using PrestoGallery.Builder;
using PrestoGallery.Pages;
using PrestoGallery.Showcase.Pages;

namespace PrestoGallery.ViewModels;

public partial class MainViewModel : ObservableObject
{
    /// <summary>Every page, by section; static so launch options can resolve a page before the app starts.</summary>
    public static IReadOnlyList<GallerySection> Catalog { get; } =
    [
        Section("Reference",
            new("Timer", "Presto.CreateTimer: a one second timer looping at 30 fps, reporting its current time and loop count.",
                () => new TimerPage()) { Twin = "Storyboard" },
            new("Playback settings", "Duration, delay, loops, loop delay, alternate, reversed and autoplay rebuild the animation; frame rate and playback rate retune the running one. The target is a real Button, so click it while it moves: drop the frame rate to 5 fps and it still counts every click.",
                () => new PlaybackSettingsPage()) { Twin = "Storyboard" },
            new("Callbacks", "Every callback an animation fires, in the order one frame fires them, with a counter each; plus awaiting the animation and its Then() callback. The target is a real Button: click it mid-flight and its own counter goes up while the callback counters keep ticking.",
                () => new CallbacksPage()) { Twin = "Storyboard" },
            new("Controls", "Play, pause, resume, restart, reverse, alternate, seek, stretch, complete, cancel, revert and reset, with the animation's state read back live. The target is a real Button, so click it while you scrub or reverse it; Revert hands it back to its own style.",
                () => new ControlsPage()) { Twin = "Storyboard" },
            new("Animation basics", "Presto.Animate: transform shorthands, opacity and a background color on one control, eased and alternating on loop. It's a real Button: click it while it moves and watch the like counter go up.",
                () => new AnimationBasicsPage()) { Twin = "Builder basics" },
            new("Value types", "Every value the tween parser accepts: numbers, unit strings, relative values, from-to arrays, colors, the codec-backed Avalonia struct types, a typed AvaloniaProperty, per-target function values, and discrete values (a bool and two enums) that flip at the midpoint. The targets are real Buttons, a ProgressBar and a profile card whose border, corners and avatar brush are its own properties; hover or click them mid-flight.",
                () => new ValueTypesPage()) { Twin = "Properties and keyframes" },
            new("Keyframes", "Duration-based and percentage-based keyframes, per-frame duration, delay and ease, and a playback ease over the whole sequence. The travelers are real Buttons and a ToggleSwitch: flip the switch while it rides the keyframe path.",
                () => new KeyframesPage()) { Twin = "Properties and keyframes" },
            new("Timeline", "Presto.CreateTimeline: children added at absolute, appended, relative, previous-child and label positions, a positioned timer, .Call, .Sync and a scrub slider. Each lane moves a real control, a Button, ToggleSwitch, Slider, TextBox or CheckBox: pause the timeline and use them where they stopped.",
                () => new TimelinePage()) { Twin = "Timeline and compositor" },
            new("Stagger", "Presto.Stagger: value and time staggers, ranges, start, total, ease distribution, jitter, modifier, a named grid against the measured auto-grid, and a stagger positioning timeline children.",
                () => new StaggerPage()) { Twin = "Ripple grid" },
            new("Eases", "Every ease plotted from its delegate next to a dot moving through it: the power, back and elastic families, cubic bezier, steps, irregular, linear stops, springs, a custom ease from SVG path data, wiggle, rough, slow-mo, expo-scale and the string parser.",
                () => new EasesPage()) { Twin = "Playground" },
            new("Springs", "Springs: a lab that sets one spring physically or by bounce and perceived duration, with its damping ratio, curve and a weight on a coil; the three damping regimes raced; continuous velocity against a restarted tween; a thrown puck; an ease against a spring follower; presets in real UI; and chained springs trailing the pointer.",
                () => new SpringsPage()),
            new("Animatable", "Presto.CreateAnimatable: a grid of dots following the pointer, each property registered with its own duration — X and Y from a center-out stagger, Rotate written through with none — beside a single dot easing after it, both created in a scope that reverts them when you navigate away, and a ball tossed on a Projectile arc.",
                () => new AnimatablePage()) { Twin = "Reusable builders" },
            new("Effects", "The effect shorthands on real controls: text and a picture blurred out and back, a drop shadow steered by five sliders, BoxShadow beside a DropShadowEffect on identical cards, a Flip card turning in perspective, a card tilting after the pointer and springing back, ClipInset, ClipCircle and MaskSweep reveals over one picture, and a row leaving with a blur-out Presence.Exit. Each writes one bridge-owned effect, clip or mask and removes it at rest, and every stage prints the code it ran.",
                () => new EffectsPage()),
            new("Compositor", "Both panels play the same rings and comet: the left is a CompositorScene ticked and drawn on the render thread, the right is real Ellipses animated on the UI thread, and the row below is a real Button, card, gear and ToggleSwitch whose animations were compiled onto the compositor. Press Freeze and watch the UI panel, its fps and the ProgressBar stop dead while everything else keeps moving and the scene counts how long the UI thread has been frozen. Pause, Resume, Reverse and the speed slider drive the compositor controls.",
                () => new CompositorPage()),
            new("Layout", "AnimateLayout and AnimateSharedElement: a grid of titled icon cards that shuffles, gains and loses cards, each easing into its new cell as FLIP played by the render thread, beside a panel that grows under PositionAndSize and a square that moves between two containers. Below, Animate.ContentSize eases real layout size instead: a FAQ card opens to its answer's natural height, and Next view swaps a panel's content, the old view fading out as a ghost while the box eases between the two views' sizes. Try Shuffle with slow on, then Freeze the UI thread and watch the cards keep easing.",
                () => new LayoutPage()),
            new("Lifecycle", "Presence.Initial, Enter and Exit side by side: three views, the last a virtualized list, whose rows rise in a stagger when a view loads, slide in when added on their own and leave on a ghost when removed, while a whole view navigated away plays nothing. The log beside them is written by each builder's OnBegin, so it shows what Presence classified. Navigate, show a view again, add and remove rows to see each moment. Below, a ContentControl swaps between two views that crossfade, the old one leaving on its ghost as the new one enters, and a three-step wizard whose host sets Presence.Mode to Wait, so each step slides in only after the last has slid out, with a picker to compare it with Sync. Last, a list filtered by tag chips through a FilteredView, whose rows fly to a Done list in the same Animate.ItemGroup when clicked, with no exit and no entrance.",
                () => new LifecyclePage()),
            new("Visibility", "Show and hide through a bound IsVisible: two cards whose switches set nothing but IsVisible play Presence.Enter and Exit, one under the default Presence.HideMode, Hold, which keeps the card in its slot while it exits and plays back if you switch it on again mid-way, the other under Ghost, which closes the layout at once and exits on a snapshot. Below, a Fluent Expander's rows re-enter in a stagger each time it opens, through Presence.EnterOnShow on its content border, as the sidebar's sections do. The log shows each Show, Hide and replayed Enter.",
                () => new VisibilityPage()),
            new("Page transitions", "PrestoPageTransition on Avalonia's three page hosts. Pick a page recipe, or a custom Enter and Exit builder pair, and press Next and Back to play it forward and reversed in a TransitioningContentControl, with the C# and XAML that set it printed beneath it; press the freeze button and the transition keeps playing on the render thread while the ProgressBar stops. Drag the Carousel's page sideways and a Presto PageSlide follows your finger, then push and pop the NavigationPage to see PagePush slide over and dim.",
                () => new PageTransitionsPage()),
            new("States", "Motion.Hover, Press and Focus on real Buttons, a TextBox, a checklist and cards, which lift on hover, squish on press and grow on keyboard focus. The checklist slides done items through a shared :checked state, a card's hover nudges its arrow through a style setter on Motion.State, a status chip's bound Motion.State pulses, shakes and settles, and a menu panel's Open state unfolds it and staggers its items in, with pickers for Order and StaggerFrom. Tab through it to see focus-visible differ from a click, and watch the log record each state change.",
                () => new StatesPage()),
            new("Value-driven", "{presto:Follow} and Follow: a view model's Level drives a bar's Width, a gauge needle's Follow.Rotate, a swatch's Background brush and a card's Margin, each easing to every new value with no animation code. Mash + and − and the springs carry their momentum into each retarget; pick a timing to retime the gauge live. Below, 200 virtualized rows ease their bound progress bars as values change, while shuffled, replaced or freshly scrolled-in rows snap to their new item instead of sweeping.",
                () => new ValueDrivenPage()),
            new("Derived values", "One MotionValue, five properties: PointerValue across a stage is mapped to a card's Follow.X, mapped and sprung into its Follow.Rotate, bound to its Opacity with Follow.Bind, mapped through three colors onto its Background and, through VelocityOf, into a Follow.Blur, with the C# that builds the graph printed beneath it. Sweep fast and the card blurs; stop sharply and the tilt springs on past the pointer. The header's opacity is derived in a view model and written by {presto:Follow} with Duration=0.",
                () => new DerivedValuesPage()),
            new("Draggable", "PrestoAvalonia.CreateDraggable: a spring-released card bounded by its container, a grid-snapped card and an x-only rail dragged by its handle, with the first one's X, Y, progress and callbacks read back live. Drag a card by its body, then tick its CheckBox: the CheckBox keeps its own clicks and never starts a drag.",
                () => new DraggablePage()) { Twin = "Reorder" },
            new("Scroll", "PrestoAvalonia.OnScroll: a card revealed by an animation the observer plays and pauses, a card watching named thresholds, and a timeline scrubbed by the scroll position in sync mode, with progress, velocity and direction read back live. Back to top and the section buttons ease the panel there through ScrollTo and ScrollIntoView, and the observers fire and scrub along the way; a wheel mid-flight stops it. Last, a list's shadows fade in through Scroll.CueFor as rows pass its edges, a chip row's ends fade through Scroll.FadeEdges only while chips lie past them, and ScrollRemainingValue reads back how far the list can still scroll.",
                () => new ScrollPage()) { Twin = "Recipe gallery" },
            new("Shapes", "PrestoAvalonia.CreateDrawable and PrestoAvalonia.CreateMotionPath: a stroke drawn onto a Path through its \"Draw\" property, a control riding a second path's contour with X, Y and Rotate sampled from it, and a third riding a Catmull-Rom spline through six points, turned by its autoRotate argument, with a curviness slider that redraws its track.",
                () => new ShapesPage()) { Twin = "Submit flow" },
            new("Text", "PrestoAvalonia.SplitText: a headline taken apart into per-line and per-character controls, with a staggered effect that re-runs over the new pieces whenever the width changes.",
                () => new TextPage()) { Twin = "Recipe gallery" },
            new("Image effects", "CSS color filters on FilteredImage: one photo with a slider per filter, bound two-way, and the XAML and builder its values amount to printed live; gray thumbnails that colorize on hover through Motion.Hover and Recipes.Colorize and turn gray again when the pointer leaves; a photo developing in on Presence.Initial with Recipes.Develop; a duotone card whose two colors animate whenever its theme variant changes; a hue cycle looped from Animate.OnAttached; a slideshow whose SourceTransition plays the kind picked (Crossfade, Dissolve, Pixelate, Ripple or Wipe) on every Previous and Next, interrupting from the composite on screen when clicked quickly; and a card that dissolves in on Presence.Enter and pixelates out before it is removed, since an exit ghost is only a snapshot. Flip the card's switch to put it in a light ThemeVariantScope and back, and press Reset to return the photo to its identity, where it draws plain.",
                () => new ImageEffectsPage()),
            new("Frames", "Frame-by-frame playback from a SpriteSheet and an ImageSequence. The adventurer plays its 24 poses, cut from a 9 × 3 sheet, at the fps and in the direction the controls pick, and the scrub slider is bound two-way to Frame, pausing it on whatever frame you drag to and playing on from there when you let go. Its hue slider is bound to HueRotate, a color filter every frame player inherits. The zombie is started from XAML alone with {presto:PlayFrames Fps=12}, and below it a timeline plays an eight-frame image sequence and writes each caption in step with it.",
                () => new FramesPage()),
            new("Particles", "An emitter playground over a ParticleScene, whose particles are simulated and drawn on the render thread rather than tweened. Press on the stage to move the origin, drag to aim and release to emit; the sliders, shape and palette feed the EmitterParams printed beneath it, and with a rate above 0 the stream follows every change. Stop ends emission and lets the particles die out, Clear removes them at once, and the count reads the scene a few times a second.",
                () => new ParticlesPage()),
            new("Ripple", "Ripple: Material press ink on any Control, drawn on the render thread over the content. Hold a press and the ink grows from your finger until it covers the element, then fades as you let go. Buttons show white ink, the theme accent when no Color is set, and an unbounded circle on an icon button; three focusable cards ripple from the center on Enter or Space and tint on hover through StateLayer; and a scrolling list's rows cancel a held ripple when the list scrolls. Under reduced motion a press only tints.",
                () => new RipplePage()),
            new("Lottie", "Lottie files played by LottieView through Skottie, its Frame tweened like any other property. A spinner loops from XAML alone with {presto:PlayLottie}; the toggle icon's markers become a button each, Play(\"on\") and Play(\"off\"), while its twin plays the hover segment through Motion.Hover. The large spinner plays at the speed and in the direction picked, its scrub slider is bound two-way to Progress and pauses it while dragged, and its HueRotate and Grayscale sliders are filters it inherits. A timeline plays the on segment and slides a label in on the same clock, and the last spinner can switch to RenderThread: press Freeze and it keeps turning while the UI thread sleeps.",
                () => new LottiePage())),
        Section("Showcase",
            new GalleryPage("Title sequence", "One 9-second timeline builds a whole title card in eight labeled beats. Letterbox bars close while the camera slowly pushes in, a comet flies in on a motion path and draws its trail behind it, the monogram draws itself with a spring pop and morphs into the mark through an OKLCH hue sweep while the dot grid ripples out from the center, added on top of its drift. PRESTO then swings up in 3D out of a blur and a wave passes through it, the subtitle scrambles and flickers on, a shine sweeps the title and the cards flip in on a spring. Press Loop to watch it repeat, try the SEEK buttons to jump to a beat by its label, and scrub, reverse or drag the speed and duration sliders to retime the whole sequence without a jump. Under reduced motion it opens on its final frame and plays only when you press play.",
                () => new TitleSequencePage()),
            new GalleryPage("Morph lab", "The whole Shapes surface at once: a path looping through six geometries as a chain of MorphTo children with the ease and the sampling precision live, so low precision is visibly polygonal; a signature that writes itself and erases through keyframed \"Draw\" values; and a marker riding a track's contour with its rotation taken from the tangent, followed by five more strung out behind it at increasing motion path offsets, each supplied per index by a function value. Under reduced motion each plays once and the signature stays drawn.",
                () => new MorphLabPage()),
            new GalleryPage("Particle field", "Thousands of plain Particle objects and no Avalonia objects below the canvas: particles laid out on a grid and drifting on one animation with a center-out grid stagger, ticked and drawn by the scene's own engine on the render thread, parting around the pointer through a push damped per frame with Utils.Damp rather than tweened, and bursting outwards within 260px of each click as a spring on Composition.Additive, so the dots keep drifting while they fly. The count slider walks the field from 500 to 20 000, and freezing the dispatcher for two seconds leaves the fps the scene draws in its top-left corner climbing while the UI-thread one beside the slider stalls.",
                () => new ParticleFieldPage()),
            new GalleryPage("Card gallery", "The imperative counterpart to Storefront's declarative grid: each card is handed AnimateLayout in code, and its arrival and departure are compositor animations started by hand, where Storefront declares the same in XAML with Animate.ItemLayout and Presence. Opening the page plays one wave of cards rising into place from the top-left corner. Filter by a chip, flip the sort or the grid/list switch, and every card eases from where it was drawn into its new slot on the render thread, while leaving cards shrink out and are removed in their animation's OnComplete. The Position/Position+Size switch re-arms every card, so the grid/list switch either snaps each card to its new width and eases only the move, or eases the size too. Drop mid-reflow pulls a card out a third of the way through a re-sort. Click a card to lift it into the detail pane over a grid that blurs behind it, then click it again, even mid-flight, and it reverses from where it is drawn. Filtering or re-sorting while a card is open closes it first.",
                () => new GalleryFlipPage()),
            new GalleryPage("Board", "Three lanes of draggable cards. Drag one and watch the lane under it open a gap where it would land, following the card through the drag and the throw; a gentle drop fills the gap with nothing else moving. Fling a card to throw it a lane further, or at the bin to spin it off the board. Hold a card near the bottom edge to auto-scroll the board, or press Escape mid-drag and it springs back to its slot. Above them, drag a priority by its grip and it lifts while the others slide aside; press Escape mid-drag to put it back, or move the focused one with Alt+Left/Right.",
                () => new BoardPage()),
            new GalleryPage("Dashboard", "Four counters, twelve bars, a donut and a sparkline, all retargeted where they stand: a counter is a Text tween whose TextModifier formats the number with Utils.RoundPad, a bar is its Height under a staggered animation, a donut slice is the pair of contour fractions CreateDrawable reveals, and the sparkline morphs its Data into the next dataset. A timer behind the live switch pushes a fresh round of seeded data every 800 ms, so the default replace composition is on show: each push reads the value on screen as its from and retires the tweens it overrides, and nothing snaps back to a starting value. The transport drives the reveal timeline that counts the whole board up from zero and draws the arcs and the stroke; replaying it after a push rewinds to the values it was built with. Under reduced motion the reveal and every push land at once.",
                () => new DashboardPage()),
            new GalleryPage("Scroll story", "A long article driven entirely by where its ScrollViewer is: a hero whose three layers scroll at their own Scroll.Speed for depth, three chapters that Presence.Reveal fades up as they cross a named threshold, their headings masking in by line, with every crossing of their enter and leave thresholds and its direction reported to the rail, a pull-quote whose whole crossing is shaped by an eased sync, and a stage the observer pins in place for 960px of scroll, whose timeline is seeked frame by frame to the scroll position, panning a strip of panels and counting itself up, and snapped to the panel labels once scrolling stops. The pinned timeline is created with Autoplay = false and handed to Link(). A rail across the top follows ScrollValue MotionValues through Follow: the scroll's progress fills it, and the sign and size of its velocity turn and stretch a vane through springs. Every observer is re-measured when the window resizes. Under reduced motion the hero layers scroll together.",
                () => new ScrollStoryPage()),
            new GalleryPage("Interruption lab", "What happens when an animation is interrupted mid-flight. Click a pane or press Burst for five quick retargets of three pucks at once, one per composition mode, and compare their trails: replace and overlap trace the same sharp path, while additive rounds every corner and arrives first. Press Cancel newest mid-burst and replace just stops where it is, while overlap is yanked back toward the older targets it was still animating to underneath. Sweep the pointer quickly across the tiles: retarget eases each one back from wherever it had got to, while restart snaps it to fully raised before it falls.",
                () => new InterruptionPage()),
            new GalleryPage("Typography", "The text module composed into four panels. The paragraph's words rise out of their own masks line by line; drag the width slider mid-reveal and the text re-wraps while the reveal carries on instead of starting over. The headline's last word drops out letter by letter and the next one rises in while its Animate.ContentSize host eases the gap to the new word's width, so the headline never jumps. Sweep the pointer across MOTION to lift and brighten the letters nearest it, then leave and watch them spring back, or pick Bulge to split it into vector outlines and run a TextWarp magnifier along under the pointer, while the terminal types, decodes and counts to 100 on a loop. Under reduced motion the headline keeps its word, the bulge holds the word straight and the terminal runs once.",
                () => new TypographyPage()),
            new GalleryPage("Type lab", "Kinetic type in five experiments, set in a bundled Archivo family with nine weights and six widths. A ribbon of text rides a Bézier through TextPath: drag its four handles and the letters follow, press Morph to turn the curve into a circle and then an S-curve with the text glued on, or Flow to run it along the contour. A headline breathes from FontWeight 100 to 900 in a staggered, alternating wave, and the line beneath does the same from ExtraCondensed to Expanded FontStretch. Click the shatter word and MorphText bursts it into the next one, its leftover letters breaking into vector shards of their outlines or, with Vector shards off, flying away as bitmap snapshots; switch the burst off for a plain morph, or Shape morph on to grow each new letter out of a leftover old one. A SplitFlap departures board flips its rows as flights update every few seconds. Drag any letter of the elastic word and its neighbors follow on a spring trail that weakens with distance, then everything springs home with overshoot when you let go. Under reduced motion the waves hold a static ramp of weights and widths and the curve swaps shape at once; the shatter word crossfades, the board's changed cells fade in, and a dragged letter moves alone and drops home at once.",
                () => new TypeLabPage()),
            new GalleryPage("Glyph lab", "Letters as vector outlines, in Inter, Archivo and the bundled Playfair Display and Dancing Script. Recipes.WriteIn traces a script word's strokes and then fills them: replay it at another speed, letter by letter or all at once. One large glyph cycles A to Z and 0 to 9 through Recipes.MorphGlyphs in the font you pick; show its contours to see each one in its own color, holes dashed, and a dotted ring where an unpaired contour collapses or grows. O, 8, B and 0 morph side by side, with their counters kept on the left and garbled on the right, where each outline is one figure. Click the shatter word to break it into vector shards with Recipes.Shatter, as many per letter as the slider says, and press Reverse mid-flight to reassemble it. Under reduced motion the word fades in, the glyphs crossfade through one pass of the alphabet and stop, the single figures swap at once, and the shatter word fades out and back in as the next one.",
                () => new GlyphLabPage()),
            new GalleryPage("Morph clock", "Numbers that change shape instead of being swapped. A clock in Archivo with tabular figures is one HH:mm:ss block that ticks every second through Recipes.MorphGlyphs, which pairs numerals from the end, so only the digits that change morph and the rest stay the block's own text. A countdown from 0:10 runs down with Start and Stop and morphs 0:00 into DONE. Three button labels morph into a glyph and back: Submit becomes Sending and then a check as it steps through the submit states, Play turns into Pause, and the menu's three bars cross into an X. Press Refresh and three stats change three ways at once: morphing outlines, RollDigits' odometer and a SplitFlap board. Under reduced motion the clock and countdown change at once, the labels and morphing stats crossfade, RollDigits sets its number at once and the board's changed cells fade in.",
                () => new MorphClockPage()),
            new GalleryPage("Particle type", "A word drawn by thousands of particles. GlyphPoints.Sample reads points along or inside the outline PrestoAvalonia.TextOutline gives for it, and ParticleScene.AddFormation sets one particle on each, every one on a spring to its own point, stepped and drawn on the render thread. Move the pointer through the word and Repel pushes the particles aside until you leave and they spring back. Click the stage and Retarget flies the particles to the next word, fading the spares out and spawning the shortfall: Nearest pairs each point with the nearest particle, Sweep sets them off left to right, Burst throws them outward first and Swarm sends each one across the word, and the duration slider sets how long they take to settle. Switch between edge, fill or both, change the spacing and watch the live count, or pick another color. The freeze button retargets and then blocks the UI thread for two seconds, and the particles still fly into the next word. Beneath the stage, Recipes.Disintegrate blows a caption away as particles and Recipes.Assemble gathers them back into it. Under reduced motion each new word, spacing or sampling fades in on its points instead of flying there, the pointer pushes nothing, and the caption only fades out and in.",
                () => new ParticleTypePage()),
            new GalleryPage("Kinetic poem", "A lyric video on one 120 BPM timeline, seven phrases a bar each with a label apiece: the opening line traces its outlines and fills them as Recipes.WriteIn writes, words slam in from 2.5× scale on the beat, STRETCH spreads its letters from the center, a line ripples through a bounded Recipes.Wave, another drops away letter by letter with Recipes.Fall, BREAK shatters on the beat into GlyphShards of its outlines that fly apart through Recipes.Scatter, and the next line melts off the bottom of the frame through a TextWarpKind.Melt warp on the \"Warp\" shorthand, with seeds so every play is the same. A beat dot pulses with the timeline, so it follows a scrub, a seek or a reverse. Jump to a phrase with the SEEK buttons, or scrub, pause and reverse it from the bar. Under reduced motion each phrase only fades in and out, the shards fade where they were cut, the melt holds straight and the dot only dims.",
                () => new KineticPoemPage()),
            new GalleryPage("Micro-interactions", "Six everyday UI patterns, each on real controls: a submit button that SubmitButton.Run collapses to a spinner and then draws a check in, a like that presses in, pops and bursts (Motion.Press, Animate.OnTapped and Recipes.Burst), a TabStrip underline that stretches between tabs under labels that lift on hover (Motion.Hover), an Accordion whose sections fold open one at a time with a springing chevron, toasts from a ToastHost that slide in and out through Presence and restack through Animate.ItemLayout, and a TextBox that shakes through Recipes.Shake. A Save button's label morphs into a check mark through Recipes.MorphGlyphs and back after a pause. Every button also carries a press Ripple. Press Notify a few times, hover a toast to hold its countdown or swipe it away, and submit the email field without an @. Under reduced motion the Save label crossfades to the check and back.",
                () => new MicroInteractionsPage()),
            new GalleryPage("Loaders", "Nine loading indicators, each one builder printed beneath it: staggers, a center-out grid wave, a MorphTo blob, a Draw ring, squash keyframes and a TextModifier counter. Drag the speed slider to retune every loader's own Speed while the rest of the gallery keeps its pace, and flip Pause all to freeze them mid-motion. Render thread plays the same builders for the equalizer, orbiting dots, grid wave, ball, typing and ripples through AnimateOnCompositor, so those six keep moving through the UI-thread freeze; the blob, ring and counter animate Data, Draw and Text, which have no composition property, so they stay on the UI thread and stop with it.",
                () => new LoadersPage()),
            new GalleryPage("Carousel", "Five slides on an x-only draggable that coasts on inertia and lands on a slide: a soft swipe moves one, a hard flick skips several and bounces off the last. Drag one halfway and watch its background, art and title slide at different rates; the arrows and the Left/Right keys spring to a slide too. The ring around the active dot draws the four-second autoplay, which holds while the pointer is over the strip and stays off under reduced motion, where slides jump instead of springing.",
                () => new Showcase.Pages.CarouselPage()),
            new GalleryPage("Marquee", "Three Marquees that lay their content out once and repeat it end to end. The logo wall's copies are real Buttons built from its ContentTemplate, so click a name in any copy and the readout shows it; its edges fade, the speed slider eases it to a new Speed, and Pause, the pointer over it or Tab focus inside it eases it to a stop. Beside it a ticker scrolls up, and a now-playing title in a narrow label rests while it fits and scrolls once Long title makes it overflow. Under reduced motion all three hold still, start-aligned and clipped.",
                () => new Showcase.Pages.MarqueePage()),
            new GalleryPage("Connected", "Twelve album cards; click one and its art and title fly into the detail view on transitions of their own, 560 and 460 ms long, so the title lands first while the other cards scatter away from it on one staggered compositor animation. Press Back or Escape at any point, or click the card again while it closes, and every piece reverses from where it is drawn.",
                () => new ConnectedPage()),
            new GalleryPage("Connected navigation", "A connected animation between two views, as the Community Toolkit does between pages: scroll the gallery, click a destination, and it is swapped for a detail view built from scratch while the photo arcs and the city name flies into place on the render thread, the two views meeting only through prepared keys. The text, facts and buttons ride in behind them on one staggered compositor builder. Press Back or Escape, even mid-flight, to fly them home into the scrolled list as the other cards ripple back in, and turn on Block the UI thread to see the flights land while the dispatcher sleeps.",
                () => new ConnectedNavPage()),
            new GalleryPage("Connected XAML", "The same destinations with no animation code at all: a view model swaps a gallery for a detail page through DataTemplates, and the views' XAML carries the rest. Connected.Key marks the photo and city name as the same thing in both views, Connected.Motion gives the photo its arc both ways, and Connected.Anchor brings the detail text in behind the photo's flight. Scroll the list, open a destination and go back, then flip the switch to host the pages in a cross-fading TransitioningContentControl, where the two views overlap and the photo flies from the live one. The XAML that does it is printed beneath the stage.",
                () => new ConnectedXamlPage()),
            new GalleryPage("Bloom navigation", "Four tabs, each with its own hue, on a TransitioningContentControl whose PageTransition is {presto:PageBloom}. Click one and the new page grows in a circle from the pointer, or from the stage's center when you pick it with the keyboard, until it covers the farthest corner; at 65% its tiles rise in from the center out, as the underline, labels and header follow. Click tabs quickly and each interrupted bloom finishes its circle under the next, so none stack. Under reduced motion the page fades in instead.",
                () => new BloomNavPage()),
            new GalleryPage("Gestures", "Four draggables with their live velocity. Pull the feed down (PullToRefresh): it stiffens as it goes and the arrow turns as the ring draws; it springs home from a short pull and holds at the threshold from a long one while the ring spins, then three new rows slide in and the list springs back. Swipe an inbox row either way to reveal archive or delete; past 40% the icon pops and the release carries the row off and collapses its gap, and Undo brings the last one back from the side it left. Pull the curtain down by its handle, or tap the handle to step it closed, half and full: it rests at the nearest of the three, and the content under it sinks, shrinks and dims in step, springs included. Flick the bottom sheet: its release coasts on inertia to the nearest stop, and a hard flick overshoots the open or closed stop and bounces back onto it. Drag the card's handle down to scrub its opening timeline, then let go: it settles on closed, half or open, a flick advancing one rest, and grabbing it mid-settle carries on from where it is. Move the pointer over the trails stage and two trails of eight dots chase it (MotionValue.Trail): in blue each dot is a spring following the one before, and in aqua each replays the pointer 40 ms after the one before. Drag the ring beside it: the dot below follows its Resist(-80, 80) value, so past either end of the track it stretches only a fifth as far as you drag.",
                () => new GesturesPage()),
            new GalleryPage("Photo viewer", "Three photos under one Transformable each. Pinch to zoom about your fingers, turn two fingers to rotate and let go to land on a quarter turn, double-tap or double-click to zoom in and again to zoom out, or hold Ctrl and scroll to zoom about the cursor. Push past the zoom limits and the photo rubber-bands, then springs back; once zoomed, one finger or the mouse pans it within its edges and a flick coasts. At rest a swipe goes to the Carousel and changes the photo, which resets the one you left; pick one from the filmstrip too. The dimmed backdrop behind the photos is a FilteredImage whose SourceTransition dissolves it to each new photo. The readout follows Scale, Rotation and the release speed, and Reset eases everything home.",
                () => new PhotoViewerPage()),
            new GalleryPage("Liquid", "A tab bar whose pill stretches: click a tab and the pill's leading edge darts there in 300 ms while the trailing edge follows 70 ms later on a slower ease, squashing as it goes, both edges plain doubles on one animation with per-property tween params. Click across the bar quickly and it retargets from wherever it is without snapping. Press the gooey button to spring three actions out of it and again, even mid-flight, to pull them back in: a single Path rebuilt every frame joins each one to the button with a metaball neck that thins and breaks as it leaves.",
                () => new LiquidPage()),
            new GalleryPage("Liquid type", "Display type through the Distort decorator's raster effects. A serif title flows like water: Turbulence displaces its pixels through noise, as strongly as the slider says, while a looped TurbulencePhase moves the noise along the line. Two words drift together inside one Distort with Goo set, fuse where they meet and pull apart again as the neck between them thins and breaks. Hover the heading and its Glitch jumps up and back to 0 within 300 ms, splitting its color channels and shifting bands; Raise error glitches a banner in. A last strip sets the same word side by side as a vector Wave warp of its outlines and as raster Turbulence, with when to use each. Under reduced motion every Distort stays at identity and the warp holds still: the words sit apart, nothing flows or glitches, the slider is disabled and the banner only fades in.",
                () => new LiquidTypePage()),
            new GalleryPage("Swipe deck", "Twenty destinations dealt three at a time. Drag the top card: it tilts about its bottom edge and leans a few degrees in 3D towards its travel, stamps LIKE or NOPE on the side it travels, and lifts the two cards beneath it as it goes. Let go short of 110 px and it springs home; drag past it, or flick it in any direction, and it flies off along its throw while the next card is promoted onto a draggable of its own. The buttons dismiss the same way, and Undo flies the last card back to the top from where it left.",
                () => new SwipeDeckPage()),
            new GalleryPage("Chart morph", "Eight data points, four charts. Switch between Bars, Donut, Scatter and Line and every point morphs its own Path into its next shape with MorphTo, staggered from the center (from the first wedge for the donut), keeping its color the whole way; the axis labels count to the new scale and the gridlines fade out for the donut. In Line the stroke draws itself once the markers land and undraws before any morph away. Press Shuffle or switch again mid-morph and each point carries on from the shape it is drawn as, and hover a point to lift it and read its values.",
                () => new ChartMorphPage()),
            new GalleryPage("Storefront", "A shop front built almost entirely from library recipes in XAML. A shimmering skeleton stands in while a timer plays the fetch, then the products fade up in order. Hover a card to lift it, press it, tilt its picture with the pointer or Tab to it for a glow, and hover a sold-out card's gray picture to colorize it through a FilteredImage; pick category chips to recolor them through a :checked Motion.States entry and reflow the grid. Add pops the button, flies the picture to the cart and bumps the badge with a burst of particles, and clicking a card opens a quick view in a Modal, revealed in a circle from the card over a blurred grid and shrinking back into it when it closes, by its button, Escape or a click outside. Flip Reduced motion in the sidebar to see every recipe's reduced form in context.",
                () => new StorefrontPage()),
            new GalleryPage("Activity feed", "A live feed of 2,000 events in a virtualized ListBox that uses every Presence trigger. The first screen rises in as the page opens; scroll slowly and each row fades up the first time it is shown, so scrolling back up plays nothing and a scrollbar drag lands at rest. Every four seconds a new event slides in at the top and the unread badge pulses; scroll down first and a pill arrives through Presence.Arrive to count them instead; click it to jump to the newest through ScrollToIndex, which from far down jumps close first and eases the last stretch. Scrolled down, new events insert at once while Presence.ScrollAnchor keeps the rows being read still. Day headers are items of their own, and the rows below an insert or a dismissal ease into place through Animate.ItemLayout. Dismiss collapses a row out, and near the bottom shimmering skeleton rows stand in through Animate.OnAttached while the next 50 events load. The counters tally each trigger's plays; set Reveal to Every time, or move it to OnAttached, and scrolling back replays rows.",
                () => new ActivityFeedPage()),
            new GalleryPage("Chat log", "A chat log in a virtualized ListBox whose scroller sets Scroll.StickToEnd from a template style. Messages of one to three lines arrive every 2.5 seconds, or type one and Send; at the end each new bubble slides in from its side while the log eases on to the new end, retargeting if more arrive mid-ease. Scroll up and StickToEnd lets go: new messages leave the view alone and a Jump to newest pill fades in. Click it and ScrollToIndex eases to the last message aligned at the end, where StickToEnd re-arms and follows again. Under reduced motion the log jumps instead.",
                () => new ChatLogPage()),
            new GalleryPage("Popups", "Popover on the five popup owners, dressed as an editor toolbar. Every presenter scales and slides out of its anchor through Recipes.PopoverIn, from whichever edge it ended up on, and plays PopoverOut back into it before it closes, whether you pick an item, click away or press Escape. Inside, Presence.Arrive staggers the rows: the File menu's items, the status drop-down's options and a right-clicked sketch's menu slide in one after another, the Share flyout's avatars pop in and the rich tooltip's lines rise. The Share flyout sets its own springier Popover, which wins over the style, and its link is a CodePanel whose Copy morphs into a check. Reopen the context menu or the drop-down while it closes and it plays back from where it stands; the tooltip closes at once and its ghost fades. Under reduced motion they fade in and close at once.",
                () => new PopupsPage()),
            new GalleryPage("Stress bench", "Up to 20 000 squares animated three ways: Presto animations on the UI thread, a compositor animation per visual, or one compositor scene drawing them all. Raise the count and switch modes to see where the UI frame times in the graph, the fps readouts and the gen-0 count give way. The graph is itself drawn on the render thread, so a stalled UI thread shows as a coral bar that keeps growing, and the worker switch adds a third engine on a thread of its own.",
                () => new StressBenchPage()),
            new GalleryPage("DevTools", "Four kinds of motion left running for the Presto inspector: a looping, alternating timeline with a label, a staggered equalizer, an Animatable spring retargeted every 1.4 s and an animation created with Autoplay off. Press Ctrl+Shift+F12 in a debug build, or Toggle inspector, to open the overlay over the window: freeze, step one frame or slow everything to 0.25×, select the timeline to scrub it and read its Gantt and tweens, hover a row to outline what it moves, use pick mode on a shape to find its animations, and watch the spring in the springs panel. Closing the overlay puts the engine back to normal speed.",
                () => new DevToolsPage())),
        Section("Builder",
            new GalleryPage("Builder basics", "The Animation basics like-button spelled with the fluent builder: Presto.Build() chains the transform shorthands, opacity, a Palette brush and the playback settings, and the code beneath the stage is the exact call that ran, captured at compile time. It's a real Button, so click it while it moves and watch the counter go up; Replay restarts it. Under reduced motion it only changes color, once.",
                () => new BuilderBasicsPage()),
            new GalleryPage("Properties and keyframes", "Three builder stages, each with the code that ran beneath it: one property with its own from, to and ease beside another with its own duration and delay; a ball bouncing through per-property keyframes above a square following animation-level keyframes at 0, 30 and 100 percent; and a card whose Palette background, CornerRadius and BorderThickness are typed values, the last set through any AvaloniaProperty. Replay restarts a stage.",
                () => new PropertiesPage()),
            new GalleryPage("Reusable builders", "A builder is a reusable recipe: one static Pop builder plays on whichever button you click, and the same recipe runs over the whole row with a stagger delay layered on its snapshot. The slider changes the recipe's duration, printed as the Pop.Duration line under it, so the next play uses it while a pop already running keeps its own, because every Animate snapshots the builder.",
                () => new RecipesPage()),
            new GalleryPage("Timeline and compositor", "One fadeUp builder assembled into a timeline, each card added at its own position with Add(target, builder, position); the slider scrubs it and Play resumes. Below, a gear spun by AnimateOnCompositor(builder) runs on the render thread, so press Freeze and watch it keep turning while the timeline above stops dead for two seconds.",
                () => new TimelineBuilderPage()),
            new GalleryPage("XAML", "Builders attached from markup, three ways, plus cues: a builder written out in full and shared through x:Static on a row of cards, and the library's {presto:Fade} markup extension whose Delay cascades a list in, both entrances on Presence.Arrive; and a binding on Animate.OnAttached to a view model that swaps Recipes.Float for a looping Recipes.Shake when you flip the switch. Last, a view model that only raises Cue properties, which Animate.Cue picks up: a failed save shakes the field through OnCue={presto:Shake}, a good one shows a toast through DoOnCue={presto:ShowToast} that its own button dismisses, and Cheer throws a {presto:Burst}. The x:Static, binding and cue panels print their XAML and the C# it points at, the {presto:Fade} panel its XAML; Replay re-attaches them all.",
                () => new XamlPage()),
            new GalleryPage("Reorder", "Two drag-to-reorder lists set up entirely in XAML with Reorder.Enabled and Animate.ItemLayout=\"{presto:Reflow}\". Drag a row and the others reflow around it; Alt+Up/Down moves the focused row and Escape cancels a drag. The left list lifts with the default {presto:Reorder}, the right with Intensity=2, which doubles its scale-up and shadow.",
                () => new ReorderPage()),
            new GalleryPage("Motion tokens", "The library recipes read a design system's tokens instead of literals: FadeUp, SlideIn and PopIn take their durations, eases and distances from Recipes.Tokens, so picking the Standard, Expressive or Reduced MotionTokens preset restyles every animation on the stage at once. The Reduced motion switch in the sidebar sets Presto.Defaults.ReducedMotion, so on Replay each recipe plays its reduced form, a short fade, whatever the preset. The preset is put back when you leave the page. The printed code is the settings and the calls that ran.",
                () => new MotionTokensPage()),
            new GalleryPage("Color spaces", "Color tweens along CSS Color 4 paths with k.Color(from, to, space, hue), which expands one tween into keyframes whose eases are slices of its own. Two glows loop a hue cycle: RGB keyframes visibly pulse from bright yellow to dark blue, while an OKLCH full turn at fixed lightness and chroma glows evenly, and the readouts under them show each one's OKLCH L and its contrast with the white text. Below, Next status flips a pill from syncing to warning to error in the space picked: Default fades blue to amber through gray, Oklch goes around the hue wheel and stays saturated. The printed code is the builder that ran. Under reduced motion each glow turns once.",
                () => new ColorSpacesPage()),
            new GalleryPage("Recipe gallery", "Every library recipe on its own tile, grouped by family, with the one-line call that plays it printed beneath: entrances, exits that put their tile back when they end, attention jolts, loops and loading indicators that Play toggles, interactions and a tilt on real buttons through Motion.Hover, Press and Focus, and the Typewriter, CountUp and RollDigits text recipes. Pick a token preset, drag the duration and intensity sliders, or flip Reduced motion in the sidebar to play every reduced form instead. The XAML tab builds a stage from the markup extensions: a fade-up, a pop, a shake, a lifting button, a tilting card and rows that fade out when removed.",
                () => new RecipeGalleryPage()),
            new GalleryPage("Playground", "Every control edits one step of a builder chain over six squares: transforms, duration, an ease by name, a stagger delay and where it spreads from, loop and alternate. The squares replay as you change it, and the code beneath is the C# for exactly that chain, ready to paste.",
                () => new PlaygroundPage()),
            new GalleryPage("Warp studio", "Three lines of Archivo split into outline pieces and bent by the Warp shorthands. Pick any of the seven TextWarpKind values, drag the strength, phase and radius, stagger the warp line by line, loop the phase, or let the pointer drive WarpOrigin. Presets set it all at once: a waving Flag, a Rainbow arc, a Magnifier that follows the pointer, a Whirlpool, a slow Drip, a Star Wars crawl and a Jelly the pointer kicks. The code beneath is the SplitText, TextWarp.SetKind and Animate calls for exactly those settings. Under reduced motion the shorthands hold their identities, so the headline stays straight and the phase runs once instead of looping.",
                () => new WarpStudioPage()),
            new GalleryPage("Ripple grid", "Click any cell and a wave runs out from it across a 16 by 9 grid. Grid staggers do all the work: one sets each cell's delay by its distance from the click, and two more, as ranges inside the Scale and Rotate keyframes, dip and twist the nearest cells hardest, except under reduced motion, where only the color wave runs. Click again mid-wave and it restarts from the new cell.",
                () => new RippleGridPage()),
            new GalleryPage("Submit flow", "A submit button driven by small recipes awaited in turn: it collapses into a circle, spins while a Presto timer stands in for the request, then turns aqua, pops with Recipes.Pulse, draws a tick and throws Recipes.Confetti, or turns coral, shakes with Recipes.Shake and draws a cross, before springing back. Tick \"Fail the next request\" to see the error branch. Under reduced motion the button snaps between shapes and the spinner holds still.",
                () => new SubmitFlowPage()),
            new GalleryPage("Counters", "Four stat tiles counted by Text tweens, each with its own textModifier for currency, thousands or percentages and counting on from the value it shows. Below, an odometer rolls its digit strips with one Y tween whose target digit comes per strip from a function value, staggered from the last digit; tick Mechanical to add a Modifier that snaps each strip to whole digits. New data retargets both, and each tile pops through Animate.OnChanged as its watched value changes. At the bottom, Recipes.RollDigits rolls a readout to a random value, moving only the characters that change.",
                () => new CountersPage()),
            new GalleryPage("Toast stack", "A ToastHost whose item template attaches its entrance and exit, {presto:Slide} recipes on Presence.Arrive and Presence.Exit, and marks its countdown bar with ToastHost.Countdown, which fades instead of shrinking under reduced motion, all from XAML. The page only calls Show with a five-second duration. When the bar runs out, or you press ✕, the toast leaves: the exit plays on a ghost while {presto:Reflow} on Animate.ItemLayout closes up the stack. Hover a toast to hold its countdown, or swipe it away. Press Notify a few times: past MaxCount, four here, the oldest leaves on its own.",
                () => new ToastsPage()),
            new GalleryPage("Form states", "A sign-up form as a state machine: Idle, Validating, Error and Success. The card and badge bind Motion.State to the view model's State and list a MotionState per value in XAML, so each transition plays the entering state and returns whatever it no longer names to rest: a breathing pulse and spinning badge while checking, a shake on error, a spring pop on success, and a gentle float at rest; under reduced motion nothing loops, travels, spins or shakes.",
                () => new FormStatesPage()),
            new GalleryPage("Storyboard", "A looping scene of hops, landing flashes, an equalizer, a turning sun and a swooping comet, all one timeline built paused with Autoplay(false): Play, Restart or drag the slider to seek it, and read CurrentTime, IterationProgress, OnLoop calls and OnUpdate calls per second back live. Change PlaybackEase, FrameRate or PlaybackRate and it rebuilds at the same moment; drop to 6 fps and the updates per second follow. The comet's spring trail is not in the timeline: pause or scrub and it keeps settling onto the comet.",
                () => new StoryboardPage())),
    ];

    public IReadOnlyList<GallerySection> Sections => Catalog;

    [ObservableProperty]
    public partial GalleryPage? SelectedPage { get; set; }

    [ObservableProperty]
    public partial Control? CurrentPage { get; set; }

    /// <summary>Whether the page transition plays backwards: going back, or in the wide layout a page above the previous one in the nav.</summary>
    [ObservableProperty]
    public partial bool IsBack { get; set; }

    /// <summary>Whether the compact layout shows Home, the nav at full width, instead of <see cref="CurrentPage"/>.</summary>
    [ObservableProperty]
    public partial bool AtHome { get; private set; }

    private const int HistoryLimit = 50;
    private readonly List<GalleryPage> _history = [];
    private readonly bool _startGiven;
    private bool _goingBack;
    private bool? _compact;

    /// <summary>Whether the view shows the compact layout, reported by the view from its width.</summary>
    /// <remarks>
    /// No page is built before the first report: a compact start with no start page goes <see cref="AtHome"/> and builds none
    /// until one is chosen, and any other start builds <see cref="SelectedPage"/>. A later switch keeps the open page and the
    /// history; going wide from Home opens <see cref="SelectedPage"/>, the page Home was reached from, or after a compact start
    /// the first page, as a wide start does.
    /// </remarks>
    public bool IsCompact
    {
        get => _compact ?? false;
        set
        {
            if (_compact == value) return;
            var starting = _compact is null;
            _compact = value;
            OnPropertyChanged();
            if (!starting)
            {
                if (!value) AtHome = false;
            }
            else if (value && !_startGiven) AtHome = true;
            else CurrentPage = SelectedPage?.Create();
        }
    }

    public bool CanGoBack => _history.Count > 0 || (IsCompact && !AtHome);

    /// <summary>The app-wide motion preference; <c>MainView</c> binds it to <c>Motion.Preference</c>.</summary>
    [ObservableProperty]
    public partial MotionPreference Motion { get; set; } = MotionPreference.Full;

    /// <summary>The OS reduce-motion preference, shown whichever <see cref="Motion"/> is chosen.</summary>
    [ObservableProperty]
    public partial string SystemMotion { get; private set; } = "";

    /// <summary>The sections the nav shows, filtered in place by <see cref="SearchText"/> so surviving rows keep their containers.</summary>
    public ObservableCollection<NavSection> VisibleSections { get; }

    private readonly NavSection[] _nav;

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    public bool HasNoMatches => VisibleSections.Count == 0;

    /// <summary><see cref="SelectedPage"/> while the nav shows it, otherwise null: the page stays open while the search filters it out.</summary>
    public GalleryPage? NavSelectedPage => SelectedPage is { } page && VisibleSections.Any(section => section.Pages.Contains(page)) ? page : null;

    public MainViewModel() : this(null)
    {
    }

    /// <summary>Selects <paramref name="start"/>, or the first page when it is null; <see cref="IsCompact"/>'s first report opens it.</summary>
    public MainViewModel(GalleryPage? start)
    {
        _nav = [.. Sections.Select(section => new NavSection(section))];
        VisibleSections = [.. _nav];
        _startGiven = start is not null;
        SelectedPage = start ?? Sections[0].Pages[0];
        PrestoAvalonia.WatchSystemReducedMotion(reduced => SystemMotion = reduced switch
        {
            true => "OS preference: reduced motion",
            false => "OS preference: full motion",
            null => "OS preference: not available here",
        });
    }

    // In the compact layout a change while at Home is a choice from the nav, which starts a new history.
    partial void OnSelectedPageChanged(GalleryPage? oldValue, GalleryPage? newValue)
    {
        OnPropertyChanged(nameof(NavSelectedPage));
        if (_compact is null) return;
        if (_goingBack) IsBack = true;
        else
        {
            IsBack = !IsCompact && oldValue is not null && Order(newValue) < Order(oldValue);
            if (IsCompact && AtHome) _history.Clear();
            else if (oldValue is not null) Remember(oldValue);
        }
        CurrentPage = newValue?.Create();
        AtHome = false;
    }

    partial void OnAtHomeChanged(bool value)
    {
        if (!value) CurrentPage ??= SelectedPage?.Create();
    }

    /// <summary>Opens <paramref name="page"/> from a tap on its nav row, also when it is already selected but the compact layout is at Home.</summary>
    public void OpenFromNav(GalleryPage page)
    {
        SelectedPage = page;
        AtHome = false;
    }

    /// <summary>Opens the previous page; with none, the compact layout goes Home and the wide one does nothing.</summary>
    [RelayCommand]
    public void GoBack()
    {
        if (_history.Count == 0)
        {
            if (IsCompact) AtHome = true;
            return;
        }
        var previous = _history[^1];
        _history.RemoveAt(_history.Count - 1);
        _goingBack = true;
        try
        {
            SelectedPage = previous;
        }
        finally
        {
            _goingBack = false;
        }
    }

    private void Remember(GalleryPage page)
    {
        if (_history.Count == HistoryLimit) _history.RemoveAt(0);
        _history.Add(page);
    }

    partial void OnSearchTextChanged(string value)
    {
        var query = value?.Trim() ?? "";
        NavSection.Sync(VisibleSections, _nav, section => section.Filter(query));
        OnPropertyChanged(nameof(HasNoMatches));
        OnPropertyChanged(nameof(NavSelectedPage));
    }

    /// <summary>Opens the first page the nav shows; Enter in the search box.</summary>
    public void OpenFirstMatch()
    {
        if (VisibleSections.Count > 0) OpenFromNav(VisibleSections[0].Pages[0]);
    }

    [RelayCommand]
    private void Open(string title) => SelectedPage = Sections.SelectMany(section => section.Pages).First(page => page.Title == title);

    private int Order(GalleryPage? page) => Sections.SelectMany(section => section.Pages).TakeWhile(entry => entry != page).Count();

    private static GallerySection Section(string name, params GalleryPage[] pages) =>
        new(name, [.. pages.Select(page => page with { Section = name })]);
}
