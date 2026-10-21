using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MonacoLabs.Presto.Compositor;

namespace MonacoLabs.Presto.Controls;

/// <summary>A card with two faces that turns over in perspective to show <see cref="Back"/> while <see cref="IsFlipped"/> is set, and <see cref="Front"/> otherwise.</summary>
/// <remarks>
/// The faces stack in one card that turns 180° through <c>RotateY</c>, or <c>RotateX</c> when <see cref="Orientation"/>
/// is vertical, with <c>Perspective</c>. Each frame shows the face turned towards the viewer, and the back is mirrored so
/// it reads the right way round once turned. Toggling mid-turn turns back from where the card stands, and leaving the
/// visual tree mid-turn lands the turn. Under
/// <see cref="PrestoDefaults.ReducedMotion"/>, read at each toggle, the faces swap at once.
/// </remarks>
public sealed class Flip : Control
{
    /// <summary>The face shown while the card is not flipped.</summary>
    public static readonly StyledProperty<Control?> FrontProperty =
        AvaloniaProperty.Register<Flip, Control?>(nameof(Front));

    /// <summary>The face shown while the card is flipped.</summary>
    public static readonly StyledProperty<Control?> BackProperty =
        AvaloniaProperty.Register<Flip, Control?>(nameof(Back));

    /// <summary>Whether the card shows its back.</summary>
    public static readonly StyledProperty<bool> IsFlippedProperty =
        AvaloniaProperty.Register<Flip, bool>(nameof(IsFlipped));

    /// <summary>Horizontal (the default) turns the card about its vertical axis, vertical about its horizontal one.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<Flip, Orientation>(nameof(Orientation));

    /// <summary>
    /// The turn's timing and ease, the builder's only; null (the default) turns over <see cref="MotionTokens.Long"/> with
    /// <see cref="MotionTokens.Ease"/>. A spring sets the duration.
    /// </summary>
    /// <remarks>Setting a builder that animates a property or sets anything else throws <see cref="NotSupportedException"/>.</remarks>
    public static readonly StyledProperty<AnimationBuilder?> MotionProperty =
        AvaloniaProperty.Register<Flip, AnimationBuilder?>(nameof(Motion));

    private readonly Decorator _front = new();
    private readonly Decorator _back = new() { IsVisible = false, RenderTransform = new ScaleTransform(-1, 1) };
    private readonly Panel _card;
    private Animation? _turn;

    /// <summary>Creates a card showing its front.</summary>
    public Flip()
    {
        _card = new Panel { Children = { _front, _back } };
        VisualChildren.Add(_card);
        LogicalChildren.Add(_card);
        // A depth tweened up from 0 (none) passes through tiny depths that magnify the card.
        Presto.Set(_card, new AnimationParams { ["Perspective"] = Recipes.FlipPerspective });
    }

    /// <inheritdoc cref="FrontProperty"/>
    public Control? Front
    {
        get => GetValue(FrontProperty);
        set => SetValue(FrontProperty, value);
    }

    /// <inheritdoc cref="BackProperty"/>
    public Control? Back
    {
        get => GetValue(BackProperty);
        set => SetValue(BackProperty, value);
    }

    /// <inheritdoc cref="IsFlippedProperty"/>
    public bool IsFlipped
    {
        get => GetValue(IsFlippedProperty);
        set => SetValue(IsFlippedProperty, value);
    }

    /// <inheritdoc cref="OrientationProperty"/>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <inheritdoc cref="MotionProperty"/>
    public AnimationBuilder? Motion
    {
        get => GetValue(MotionProperty);
        set => SetValue(MotionProperty, value);
    }

    private string Rotation => Axis(Orientation);

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FrontProperty) _front.Child = Front;
        else if (change.Property == BackProperty) _back.Child = Back;
        else if (change.Property == OrientationProperty) Reorient(change.GetOldValue<Orientation>());
        else if (change.Property == IsFlippedProperty) Turn();
        else if (change.Property == MotionProperty && Motion is { } motion)
            LayoutMotion.Timing(motion, "The card turns to its own angles, so Flip.Motion takes only timing and an ease.");
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _turn?.Complete();
    }

    private void Turn()
    {
        var angle = IsFlipped ? 180 : 0;
        if (VisualRoot == null || Engine.Current.Defaults.ReducedMotion)
        {
            Presto.Set(_card, new AnimationParams { [Rotation] = angle });
            ShowFacing();
            return;
        }
        var tokens = Recipes.Tokens;
        _turn = (Motion?.Clone() ?? Presto.Build().Duration(tokens.Long).Ease(tokens.Ease))
            .Property(Rotation, angle)
            .OnRender(_ => ShowFacing())
            .Animate(_card);
    }

    private void Reorient(Orientation old)
    {
        _back.RenderTransform = Orientation == Orientation.Horizontal ? new ScaleTransform(-1, 1) : new ScaleTransform(1, -1);
        if (!IsFlipped && _turn is not { Completed: false }) return;
        _turn?.Cancel();
        Presto.Set(_card, new AnimationParams { [Axis(old)] = 0, [Rotation] = IsFlipped ? 180 : 0 });
        ShowFacing();
    }

    private static string Axis(Orientation orientation) => orientation == Orientation.Horizontal ? "RotateY" : "RotateX";

    private void ShowFacing()
    {
        var turned = Math.Abs(Convert.ToDouble(Presto.Get(_card, Rotation, withUnit: false))) > 90;
        _front.IsVisible = !turned;
        _back.IsVisible = turned;
    }
}
