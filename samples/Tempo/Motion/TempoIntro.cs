using MonacoLabs.Presto;
using Tempo.Views;

namespace Tempo.Motion;

/// <summary>The intro's timeline: <see cref="TempoMotion"/>'s intro steps laid out by position on <see cref="IntroView"/>'s parts.</summary>
/// <remarks>Code-only: a timeline has no XAML spelling (BACKLOG, CORE-TIMELINE).</remarks>
public static class TempoIntro
{
    /// <summary>
    /// Plays the intro, about 2.3 s before the handoff; reduced, only the ring and wordmark fade in. <paramref name="particles"/>
    /// receives the wordmark's assembly when the timeline starts it.
    /// </summary>
    public static Timeline Play(IntroView intro, Action<ParticleEffect> particles)
    {
        var timeline = Presto.CreateTimeline();
        if (Presto.Defaults.ReducedMotion)
        {
            return timeline
                .Add(intro.Track, TempoMotion.IntroTrackDraw, 0)
                .Call(_ => particles(Recipes.Assemble(intro.Wordmark)), 0);
        }

        // Particles cannot be a timeline child, so a callback starts them at their position (BACKLOG, BRIDGE-PARTICLES).
        return timeline
            .Add(intro.Glow, TempoMotion.IntroGlowBloom, 0)
            .Add(intro.Track, TempoMotion.IntroTrackDraw, 150)
            .Add(intro.Arcs.Children, TempoMotion.IntroArcPop, 450)
            .Call(_ => particles(Recipes.Assemble(intro.Wordmark)), 500)
            .Add(intro.Tagline.Children, TempoMotion.IntroTagline, 1300)
            .Add(intro.Dot, TempoMotion.IntroTick(intro.Track), 1300)
            .Add(intro.Fill, TempoMotion.IntroTickFill, 1300)
            .Add((object[])[.. intro.Arcs.Children, intro.Fill, intro.Dot, .. intro.Tagline.Children], TempoMotion.IntroMerge, 1900)
            .Add(intro.Glow, TempoMotion.IntroPulse, 1900);
    }
}
