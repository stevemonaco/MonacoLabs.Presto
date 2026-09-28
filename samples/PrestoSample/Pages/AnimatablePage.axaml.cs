using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Pages;

public partial class AnimatablePage : UserControl
{
    private readonly Animatable _particles;
    private readonly Animatable _follower;
    private readonly Animatable _momentum;
    private readonly Animatable _rest;

    public AnimatablePage()
    {
        InitializeComponent();

        for (var i = 0; i < Particles.Rows * Particles.Columns; i++) Particles.Children.Add(new Border { Classes = { "dot" } });

        var duration = Presto.Stagger(50, new() { Ease = "in(1)", From = StaggerFrom.Center, Grid = [Particles.Rows, Particles.Columns] });
        (_particles, _follower) = PrestoAvalonia.CreateScope(this).Execute(_ =>
        (
            Presto.CreateAnimatable(Particles.Children, new AvaloniaAnimatableParams
            {
                ["X"] = new() { Duration = duration },
                ["Y"] = new() { Duration = duration },
                ["Rotate"] = new() { Duration = 0 },
                Ease = "outElastic(.3, 1.4)",
            }),
            Follower.CreateAnimatable(new() { ["X"] = 500, ["Y"] = 500, Ease = Eases.Out(3) })
        ));

        var spring = new Spring(stiffness: 90, damping: 9);
        (_momentum, _rest) = PrestoAvalonia.CreateScope(this).Execute(_ =>
        (
            MomentumDot.CreateAnimatable(new() { ["X"] = new(), Ease = spring, PreserveVelocity = true }),
            RestDot.CreateAnimatable(new() { ["X"] = new(), Ease = spring })
        ));
    }

    private void MoveParticles(object? sender, PointerEventArgs e)
    {
        var point = e.GetPosition(ParticleStage);
        var halfWidth = ParticleStage.Bounds.Width / 2;
        var halfHeight = ParticleStage.Bounds.Height / 2;
        _particles
            .Set("X", Utils.MapRange(point.X, 0, ParticleStage.Bounds.Width, -halfWidth, halfWidth))
            .Set("Y", Utils.MapRange(point.Y, 0, ParticleStage.Bounds.Height, -halfHeight, halfHeight))
            .Set("Rotate", Utils.RadToDeg(-Math.Atan2(halfWidth - point.X, halfHeight - point.Y)));
    }

    private void MoveFollower(object? sender, PointerEventArgs e)
    {
        var point = e.GetPosition(FollowerStage);
        var halfWidth = FollowerStage.Bounds.Width / 2;
        var halfHeight = FollowerStage.Bounds.Height / 2;
        _follower
            .Set("X", Utils.MapRange(point.X, 0, FollowerStage.Bounds.Width, -halfWidth, halfWidth))
            .Set("Y", Utils.MapRange(point.Y, 0, FollowerStage.Bounds.Height, -halfHeight, halfHeight));
    }

    private void MoveMomentum(object? sender, PointerEventArgs e)
    {
        var halfWidth = MomentumStage.Bounds.Width / 2;
        var x = Utils.MapRange(e.GetPosition(MomentumStage).X, 0, MomentumStage.Bounds.Width, -halfWidth, halfWidth);
        _momentum.Set("X", x);
        _rest.Set("X", x);
    }

    private void SetExplicit(object? sender, RoutedEventArgs e) => _follower.Set("X", -140, 1200, "inOutQuad");

    private void RevertFollower(object? sender, RoutedEventArgs e) => _follower.Revert();
}
