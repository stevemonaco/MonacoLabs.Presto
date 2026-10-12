using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MonacoLabs.Presto;
using PrestoGallery.Showcase;

namespace PrestoGallery.Pages;

public partial class AnimatablePage : ShowcasePage
{
    private Animatable? _particles;
    private Animatable? _follower;
    private Animatable? _momentum;
    private Animatable? _rest;
    private Animation? _toss;

    public AnimatablePage()
    {
        InitializeComponent();
        for (var i = 0; i < Particles.Rows * Particles.Columns; i++) Particles.Children.Add(new Border { Classes = { "dot" } });
    }

    protected override void OnAttached()
    {
        #region snippet AnimatableParticles
        var duration = Presto.Stagger(50, new() { Ease = "in(1)", From = StaggerFrom.Center, Grid = [Particles.Rows, Particles.Columns] });
        _particles = Presto.CreateAnimatable(Particles.Children, new AvaloniaAnimatableParams
        {
            ["X"] = new() { Duration = duration },
            ["Y"] = new() { Duration = duration },
            ["Rotate"] = new() { Duration = 0 },
            Ease = "outElastic(.3, 1.4)",
        });
        #endregion

        #region snippet AnimatableFollower
        _follower = Follower.CreateAnimatable(new() { ["X"] = 500, ["Y"] = 500, Ease = Eases.Out(3) });
        #endregion

        #region snippet AnimatableSprings
        var spring = new Spring(stiffness: 90, damping: 9);
        _momentum = MomentumDot.CreateAnimatable(new() { ["X"] = new(), Ease = spring, PreserveVelocity = true });
        _rest = RestDot.CreateAnimatable(new() { ["X"] = new(), Ease = spring });
        #endregion
    }

    private void MoveParticles(object? sender, PointerEventArgs e)
    {
        var point = e.GetPosition(ParticleStage);
        var halfWidth = ParticleStage.Bounds.Width / 2;
        var halfHeight = ParticleStage.Bounds.Height / 2;
        _particles?
            .Set("X", Utils.MapRange(point.X, 0, ParticleStage.Bounds.Width, -halfWidth, halfWidth))
            .Set("Y", Utils.MapRange(point.Y, 0, ParticleStage.Bounds.Height, -halfHeight, halfHeight))
            .Set("Rotate", Utils.RadToDeg(-Math.Atan2(halfWidth - point.X, halfHeight - point.Y)));
    }

    private void MoveFollower(object? sender, PointerEventArgs e)
    {
        var point = e.GetPosition(FollowerStage);
        var halfWidth = FollowerStage.Bounds.Width / 2;
        var halfHeight = FollowerStage.Bounds.Height / 2;
        _follower?
            .Set("X", Utils.MapRange(point.X, 0, FollowerStage.Bounds.Width, -halfWidth, halfWidth))
            .Set("Y", Utils.MapRange(point.Y, 0, FollowerStage.Bounds.Height, -halfHeight, halfHeight));
    }

    private void MoveMomentum(object? sender, PointerEventArgs e)
    {
        var halfWidth = MomentumStage.Bounds.Width / 2;
        var x = Utils.MapRange(e.GetPosition(MomentumStage).X, 0, MomentumStage.Bounds.Width, -halfWidth, halfWidth);
        _momentum?.Set("X", x);
        _rest?.Set("X", x);
    }

    private void SetExplicit(object? sender, RoutedEventArgs e) => _follower?.Set("X", -140, 1200, "inOutQuad");

    private void FlingFollower(object? sender, RoutedEventArgs e)
    {
        if (_follower is null) return;
        #region snippet AnimatableFling
        var edgeX = FollowerStage.Bounds.Width / 2 - Follower.Width / 2;
        var edgeY = FollowerStage.Bounds.Height / 2 - Follower.Height / 2;
        _follower
            .Fling("X", new Inertia() { Min = -edgeX, Max = edgeX })
            .Fling("Y", new Inertia() { Min = -edgeY, Max = edgeY });
        #endregion
    }

    private void RevertFollower(object? sender, RoutedEventArgs e) => _follower?.Revert();

    private void Toss(object? sender, RoutedEventArgs e) => Scope.Execute(_ =>
    {
        _toss?.Revert();
        #region snippet AnimatableToss
        const double gravity = .0015;
        var (x, y) = Projectile.Launch(speed: .6, angleDegrees: -60, gravity);
        _toss = Ball.Animate(new() { ["X"] = x, ["Y"] = y, Duration = 2 * -y.Velocity / gravity });
        #endregion
    });
}
