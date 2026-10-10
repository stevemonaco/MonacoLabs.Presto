using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using MonacoLabs.Presto.Compositor;

namespace MonacoLabs.Presto.Tests;

/// <summary><see cref="ParticleFormation"/>s held on points in a <see cref="ParticleScene"/>, and <see cref="Recipes.Disintegrate"/> and <see cref="Recipes.Assemble"/> built on them.</summary>
public sealed class ParticleFormationTests : CompositorTestBase
{
    private static readonly Point[] Line = [new(10, 32), new(20, 32), new(30, 32), new(40, 32)];

    [AvaloniaFact]
    public void AFormationSpringsOntoItsPointsAndDrawsThere()
    {
        var (scene, canvas) = ShowScene();
        ParticleFormation? formation = null;

        canvas.Invoke<ParticleScene>(s => formation = s.AddFormation(Line, Line.Select(p => p + new Vector(0, 25)).ToArray(), Colors.White, 4, null, null));
        Tick();
        Assert.All(formation!.Dots.ToArray(), d => Assert.True(d.Y > 50));

        Assert.True(Pump(() => AtTargets(formation), 120), "the formation never reached its points");
        Assert.Equal(4, scene.Live);
        Assert.True(CountPixels(canvas, new Rect(8, 30, 4, 4), Colors.Black) > 0, "nothing drew on the first point");
    }

    [AvaloniaFact]
    public void ARepelPointPushesOnlyTheParticlesWithinItsRadius()
    {
        var (_, canvas) = ShowScene();
        ParticleFormation? formation = null;
        canvas.Invoke<ParticleScene>(s => formation = s.AddFormation(Line, Colors.White));
        Tick();

        canvas.Invoke<ParticleScene>(_ =>
        {
            formation!.Repel = new Point(8, 32);
            formation.RepelRadius = 15;
        });
        TickFor(1000);

        var dots = formation!.Dots.ToArray();
        Assert.True(dots[0].X > 12, $"the particle 2 DIPs from the repel point sits at {dots[0].X}");
        Assert.True(dots[1].X > 20.5, $"the particle 12 DIPs from the repel point sits at {dots[1].X}");
        Assert.Equal(30, dots[2].X, 0.01);
        Assert.Equal(40, dots[3].X, 0.01);

        canvas.Invoke<ParticleScene>(_ => formation.Repel = null);
        Assert.True(Pump(() => AtTargets(formation), 120), "the particles never sprang back");
    }

    [AvaloniaFact]
    public void DurationScalesTheSpringInTime()
    {
        var down = Line.Select(p => p + new Vector(0, 150)).ToArray();
        var (slow, fast) = (new ParticleScene().AddFormation(Line, Colors.White), new ParticleScene().AddFormation(Line, Colors.White));
        fast.Duration = 200;
        slow.Retarget(down);
        fast.Retarget(down);

        for (var t = 0; t < 10; t++) slow.Step(16);
        // Half the step at half the duration integrates the same curve in half the time.
        for (var t = 0; t < 10; t++) fast.Step(8);

        Assert.Equal(slow.Dots[0].Y, fast.Dots[0].Y, 6);
        Assert.Throws<ArgumentOutOfRangeException>(() => fast.Duration = 0);
    }

    [AvaloniaFact]
    public void SweepSetsParticlesOffLeftToRight()
    {
        var formation = new ParticleScene().AddFormation(Line, Colors.White);

        formation.Retarget(Line.Select(p => p + new Vector(0, 100)).ToArray(), FormationTransition.Sweep);
        for (var t = 0; t < 4; t++) formation.Step(16);

        var dots = formation.Dots.ToArray().OrderBy(d => d.TargetX).ToArray();
        Assert.True(dots[0].Y > 32, "the leftmost particle has not set off");
        Assert.Equal(32, dots[^1].Y);
    }

    [AvaloniaFact]
    public void BurstThrowsEveryParticleOutwardBeforeItGathers()
    {
        var square = new Point[] { new(0, 0), new(100, 0), new(100, 100), new(0, 100) };
        var formation = new ParticleScene().AddFormation(square, Colors.White);

        formation.Retarget(square, FormationTransition.Burst);
        for (var t = 0; t < 6; t++) formation.Step(16);

        Assert.All(formation.Dots.ToArray(), d => Assert.True(Point.Distance(new Point(d.X, d.Y), new Point(50, 50)) > Point.Distance(new Point(0, 0), new Point(50, 50))));
    }

    [AvaloniaFact]
    public void SwarmPairsBySeededShuffleRatherThanDistance()
    {
        var row = Enumerable.Range(0, 40).Select(i => new Point(i * 10, 0)).ToArray();
        var (first, second) = (new ParticleScene().AddFormation(row, Colors.White), new ParticleScene().AddFormation(row, Colors.White));

        first.Retarget(row, FormationTransition.Swarm);
        second.Retarget(row, FormationTransition.Swarm);

        var targets = first.Dots.ToArray().Select(d => d.TargetX).ToArray();
        Assert.Equal(targets, second.Dots.ToArray().Select(d => d.TargetX));
        Assert.Contains(targets.Select((x, i) => (x, i)), t => t.x != row[t.i].X);
        Assert.Equal(row.Select(p => p.X).Order(), targets.Order());
    }

    [AvaloniaFact]
    public void RetargetPairsAsAFullNearestScanWouldWithTiesToTheLowerIndex()
    {
        var random = new Random(7);
        Point Next() => new(Math.Round(random.NextDouble() * 400), Math.Round(random.NextDouble() * 120));
        var from = Enumerable.Range(0, 1500).Select(_ => Next()).ToArray();
        var to = Enumerable.Range(0, 1200).Select(_ => Next()).Concat(from.Take(50)).ToArray();
        var formation = new ParticleScene().AddFormation(from, Colors.White);

        formation.Retarget(to);

        var taken = new bool[from.Length];
        var expected = new Point?[from.Length];
        foreach (var point in to)
        {
            var (nearest, best) = (-1, double.MaxValue);
            for (var i = 0; i < from.Length; i++)
            {
                var distance = ((Vector)(from[i] - point)).SquaredLength;
                if (!taken[i] && distance < best) (nearest, best) = (i, distance);
            }
            if (nearest < 0) continue;
            taken[nearest] = true;
            expected[nearest] = point;
        }
        var dots = formation.Dots.ToArray();
        for (var i = 0; i < from.Length; i++)
        {
            if (expected[i] is { } target) Assert.Equal(target, new Point(dots[i].TargetX, dots[i].TargetY));
        }
    }

    [AvaloniaFact]
    public void RetargetPairsNearestAndSpawnsOrFadesTheDifference()
    {
        var (scene, canvas) = ShowScene();
        ParticleFormation? formation = null;
        canvas.Invoke<ParticleScene>(s => formation = s.AddFormation(Line, Colors.White));
        Tick();

        Point[] six = [new(41, 40), new(31, 40), new(21, 40), new(11, 40), new(50, 50), new(55, 50)];
        canvas.Invoke<ParticleScene>(_ => formation!.Retarget(six));
        Tick();
        var dots = formation!.Dots.ToArray();
        Assert.Equal(6, formation.Count);
        Assert.Equal([11d, 21, 31, 41, 50, 55], dots.Select(d => d.TargetX));
        Assert.All(dots[4..], d => Assert.True(d.Opacity < 1));
        Assert.True(Pump(() => AtTargets(formation), 120), "the retargeted formation never settled");

        canvas.Invoke<ParticleScene>(_ => formation.Retarget(six[..2]));
        Tick();
        Assert.Equal(6, scene.Live);
        Pump(() => false, (int)(ParticleFormation.FadeTime / Frame) + 2);
        Assert.Equal(2, formation.Count);

        canvas.Invoke<ParticleScene>(_ => formation.Retarget([]));
        Pump(() => false, (int)(ParticleFormation.FadeTime / Frame) + 2);
        Assert.Equal(0, scene.Live);
    }

    [AvaloniaFact]
    public void FormationsCountAgainstTheSceneCap()
    {
        var scene = new ParticleScene { MaxLive = 10 };
        var (_, canvas) = ShowScene(scene);
        ParticleFormation? formation = null;
        var points = Enumerable.Range(0, 15).Select(i => new Point(i * 4, 10)).ToArray();

        canvas.Invoke<ParticleScene>(s => formation = s.AddFormation(points, Colors.White));
        canvas.Invoke<ParticleScene>(s => s.Add(new EmitterParams { Burst = 5, Lifetime = (60000, 60000) }, new Point(32, 32)));
        Tick();
        Assert.Equal(10, formation!.Count);
        Assert.Equal(10, scene.Live);

        canvas.Invoke<ParticleScene>(_ => formation.Retarget([.. points, .. points]));
        Tick();
        Assert.Equal(10, scene.Live);
    }

    [AvaloniaFact]
    public void DisintegrateHidesTheBlockAndBlowsItsFillPointsAway()
    {
        var block = ShowText();
        var letters = new Rect(block.TranslatePoint(default, TopLevel.GetTopLevel(block)!)!.Value, block.Bounds.Size);

        var effect = Recipes.Disintegrate(block);
        Tick();

        Assert.Equal(0, block.Opacity);
        var dots = effect.Formation!.Dots.ToArray();
        Assert.True(dots.Length > 20, $"{dots.Length} particles");
        Assert.All(dots, d => Assert.True(letters.Inflate(4).Contains(new Point(d.X, d.Y)), $"({d.X}, {d.Y}) is off the letters"));
        TickFor(Recipes.Tokens.Long);
        Assert.Contains(effect.Formation.Dots.ToArray(), d => !letters.Inflate(10).Contains(new Point(d.X, d.Y)));

        Assert.True(Pump(() => effect.Then().IsCompleted, 200), "the particles never faded out");
        Assert.Equal(0, block.Opacity);
    }

    [AvaloniaFact]
    public void AssembleConvergesOnTheLettersThenShowsTheBlock()
    {
        var block = ShowText();

        var effect = Recipes.Assemble(block);
        Tick();

        Assert.Equal(0, block.Opacity);
        Assert.True(effect.Formation!.Count > 20);
        Assert.True(Pump(() => effect.Then().IsCompleted, 200), "the particles never settled and faded");
        RunUntil(() => block.Opacity == 1);
        Assert.Equal(1, block.Opacity);
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void StartingAgainMidEffectCancelsTheFirst(bool disintegrate)
    {
        var block = ShowText();
        var first = disintegrate ? Recipes.Disintegrate(block) : Recipes.Assemble(block);
        TickFor(Recipes.Tokens.Short);

        var second = disintegrate ? Recipes.Disintegrate(block) : Recipes.Assemble(block);
        Tick();

        Assert.True(first.Then().IsCompleted);
        Assert.Equal(0, first.Formation!.Count);
        Assert.True(second.Formation!.Count > 20);
        Assert.Equal(0, block.Opacity);
        Assert.True(Pump(() => second.Then().IsCompleted, 200), "the second effect never finished");
        RunUntil(() => block.Opacity == (disintegrate ? 0 : 1));
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReducedTheBlockFadesAndNothingSpawns(bool disintegrate)
    {
        var block = ShowText();
        Presto.Defaults.ReducedMotion = true;

        var effect = disintegrate ? Recipes.Disintegrate(block) : Recipes.Assemble(block);
        Frames.Advance(Recipes.Tokens.Short / 2);

        Assert.True(effect.Then().IsCompleted);
        Assert.Empty(OverlayLayer.GetOverlayLayer(block)!.Children.OfType<CompositorCanvas>());
        Assert.InRange(block.Opacity, 0.01, 0.99);
        RunUntil(() => block.Opacity is 0 or 1);
        Assert.Equal(disintegrate ? 0 : 1, block.Opacity);
    }

    private static (ParticleScene Scene, CompositorCanvas Canvas) ShowScene(ParticleScene? scene = null)
    {
        scene ??= new ParticleScene();
        var canvas = new CompositorCanvas { Scene = scene, Width = 64, Height = 64 };
        new Window { Content = canvas, Background = Brushes.Black, Width = 64, Height = 64 }.Show();
        Tick();
        return (scene, canvas);
    }

    private static TextBlock ShowText()
    {
        var block = new TextBlock { Text = "OB", FontSize = 40, Foreground = Brushes.Teal };
        new Window { Content = new StackPanel { Children = { block } }, Width = 200, Height = 200 }.Show();
        Tick();
        return block;
    }

    private static bool AtTargets(ParticleFormation formation)
    {
        foreach (var dot in formation.Dots)
        {
            if (Math.Abs(dot.X - dot.TargetX) + Math.Abs(dot.Y - dot.TargetY) > 0.1) return false;
        }
        return true;
    }
}
