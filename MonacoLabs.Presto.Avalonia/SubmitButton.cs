using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Shapes;
using Path = Avalonia.Controls.Shapes.Path;

namespace MonacoLabs.Presto;

/// <summary>
/// The submit-button recipe: <see cref="SubmitState.Busy"/> collapses a button to a circle around a spinner,
/// <see cref="SubmitState.Succeeded"/> or <see cref="SubmitState.Failed"/> draws a check or a cross in it, and
/// <see cref="SubmitState.Idle"/> springs it back to its content. Drive it through <see cref="StateProperty"/>, as a view
/// model would, or with <see cref="Run"/>.
/// </summary>
/// <remarks>
/// The spinner and the mark are drawn in the button's <c>Foreground</c> over its content, which fades out while it is
/// collapsed. The collapse takes <see cref="MotionTokens.Medium"/>, the mark draws in with <see cref="Recipes.DrawIn"/>
/// and the button expands on <see cref="Springs.Gentle"/>, ending on the width, corner radius and background it had
/// before. Under <see cref="PrestoDefaults.ReducedMotion"/>, read at each state change, every state shows at once and the
/// spinner stands still.
/// </remarks>
public static class SubmitButton
{
    /// <summary>What the button shows; <see cref="SubmitState.Idle"/> by default.</summary>
    public static readonly AttachedProperty<SubmitState> StateProperty =
        AvaloniaProperty.RegisterAttached<Button, SubmitState>("State", typeof(SubmitButton), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The background the button turns to on success; null keeps its own.</summary>
    public static readonly AttachedProperty<IBrush?> SuccessBrushProperty =
        AvaloniaProperty.RegisterAttached<Button, IBrush?>("SuccessBrush", typeof(SubmitButton));

    /// <summary>The background the button turns to on failure; null keeps its own.</summary>
    public static readonly AttachedProperty<IBrush?> FailureBrushProperty =
        AvaloniaProperty.RegisterAttached<Button, IBrush?>("FailureBrush", typeof(SubmitButton));

    private const double MarkSize = 20;

    private static readonly AttachedProperty<Parts?> PartsProperty =
        AvaloniaProperty.RegisterAttached<Button, Parts?>("Parts", typeof(SubmitButton));

    static SubmitButton() => StateProperty.Changed.AddClassHandler<Button>((button, e) =>
    {
        var parts = button.GetValue(PartsProperty);
        if (parts is null) button.SetValue(PartsProperty, parts = new Parts(button));
        parts.Show((SubmitState)e.NewValue!);
    });

    /// <summary>Gets what <paramref name="button"/> shows.</summary>
    public static SubmitState GetState(Button button) => button.GetValue(StateProperty);

    /// <summary>Sets what <paramref name="button"/> shows.</summary>
    public static void SetState(Button button, SubmitState value) => button.SetValue(StateProperty, value);

    /// <summary>Gets the background <paramref name="button"/> turns to on success.</summary>
    public static IBrush? GetSuccessBrush(Button button) => button.GetValue(SuccessBrushProperty);

    /// <summary>Sets the background <paramref name="button"/> turns to on success.</summary>
    public static void SetSuccessBrush(Button button, IBrush? value) => button.SetValue(SuccessBrushProperty, value);

    /// <summary>Gets the background <paramref name="button"/> turns to on failure.</summary>
    public static IBrush? GetFailureBrush(Button button) => button.GetValue(FailureBrushProperty);

    /// <summary>Sets the background <paramref name="button"/> turns to on failure.</summary>
    public static void SetFailureBrush(Button button, IBrush? value) => button.SetValue(FailureBrushProperty, value);

    /// <summary>
    /// Shows <paramref name="button"/> busy until <paramref name="work"/> ends, then its outcome for
    /// <see cref="MotionTokens.Medium"/> plus <see cref="MotionTokens.Long"/>, then idle again.
    /// </summary>
    /// <returns>
    /// Whether the work succeeded: it ran to completion and, as a <see cref="Task{Boolean}"/>, returned true. A fault stays on
    /// <paramref name="work"/> rather than being thrown here.
    /// </returns>
    public static async Task<bool> Run(Button button, Task work)
    {
        button.SetCurrentValue(StateProperty, SubmitState.Busy);
        await Task.WhenAny(work);
        var succeeded = work.IsCompletedSuccessfully && work is not Task<bool> { Result: false };
        button.SetCurrentValue(StateProperty, succeeded ? SubmitState.Succeeded : SubmitState.Failed);
        await Presto.Delay(Recipes.Tokens.Medium + Recipes.Tokens.Long);
        button.SetCurrentValue(StateProperty, SubmitState.Idle);
        return succeeded;
    }

    private sealed class Parts
    {
        private readonly Button _button;
        private readonly Path _spinner = Mark(Geometry.Parse("M10,2 A8,8 0 1 1 2,10"));
        private readonly Path _mark = Mark(null);
        private readonly Panel _layer;
        private Animation? _spin;
        private double _width;
        private CornerRadius _corner;
        private IBrush? _background;
        private bool _collapsed;
        private bool _tinted;

        public Parts(Button button)
        {
            _button = button;
            _spinner.Opacity = 0;
            _layer = new Panel { IsHitTestVisible = false, Children = { _spinner, _mark } };
            _spinner.Bind(Shape.StrokeProperty, button.GetObservable(TemplatedControl.ForegroundProperty));
            _mark.Bind(Shape.StrokeProperty, button.GetObservable(TemplatedControl.ForegroundProperty));
            button.PropertyChanged += (_, e) =>
            {
                if (e.Property == Visual.BoundsProperty && _layer.GetVisualParent() == button) Place();
            };
        }

        private Control? Content => _button.Presenter?.Child;

        public void Show(SubmitState state)
        {
            var reduced = Engine.Current.Defaults.ReducedMotion;
            var tokens = Recipes.Tokens;
            if (state != SubmitState.Idle && !_collapsed) Collapse(reduced ? 0 : tokens.Medium);
            if (state == SubmitState.Busy)
            {
                DrawAccessor.Apply(_mark, 0, 0);
                _spinner.Animate(new() { ["Opacity"] = 1, Duration = reduced ? 0 : tokens.Short });
                _spin ??= reduced ? null : _spinner.Animate(Recipes.Spinner());
                return;
            }
            StopSpinner(reduced ? 0 : tokens.Short);
            if (state == SubmitState.Idle)
            {
                Expand(reduced);
                return;
            }

            _mark.Data = state == SubmitState.Succeeded ? Prebuilt.Draws.Check : Prebuilt.Draws.Cross;
            DrawAccessor.Apply(_mark, 0, 0);
            _mark.Animate(Recipes.DrawIn());
            if ((state == SubmitState.Succeeded ? GetSuccessBrush(_button) : GetFailureBrush(_button)) is not { } brush) return;
            _tinted = true;
            _button.Animate(new() { ["Background"] = TweenValue.Of(brush), Duration = reduced ? 0 : tokens.Medium });
        }

        private static Path Mark(Geometry? data) => new()
        {
            Data = data,
            Width = MarkSize,
            Height = MarkSize,
            StrokeThickness = 2.5,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        private void Collapse(double duration)
        {
            _collapsed = true;
            _width = _button.Bounds.Width;
            _corner = _button.CornerRadius;
            _background = _button.Background;
            // Appended, so the layer draws over the template; a re-applied template clears it, so it is re-hosted each time.
            if (_layer.GetVisualParent() != _button)
            {
                Selection.VisualChildren(_button).Add(_layer);
                Selection.LogicalChildren(_button).Add(_layer);
            }
            Place();
            var height = _button.Bounds.Height;
            Content?.Animate(new() { ["Opacity"] = 0, Duration = Math.Min(duration, Recipes.Tokens.Short) });
            _button.Animate(new()
            {
                ["Width"] = TweenValue.FromTo(_width, height),
                ["CornerRadius"] = TweenValue.Of(new CornerRadius(height / 2)),
                Duration = duration,
                Ease = Recipes.Tokens.Ease,
            });
        }

        private void Expand(bool reduced)
        {
            if (!_collapsed) return;
            _collapsed = false;
            _mark.Animate(Recipes.DrawOut(duration: Recipes.Tokens.Short));
            Content?.Animate(new() { ["Opacity"] = 1, Duration = reduced ? 0 : Recipes.Tokens.Medium, OnComplete = fade => fade.Revert() });
            var expand = reduced ? Presto.Build().Duration(0) : Presto.Build().Ease(Springs.Gentle);
            expand.Width(_width).CornerRadius(_corner).OnComplete(animation => animation.Revert());
            if (_tinted && _background is not null) expand.Background(_background);
            _tinted = false;
            _button.Animate(expand);
        }

        private void StopSpinner(double duration)
        {
            var spin = _spin;
            _spin = null;
            spin?.Pause();
            _spinner.Animate(new() { ["Opacity"] = 0, Duration = duration, OnComplete = _ => spin?.Revert() });
        }

        private void Place()
        {
            _layer.Measure(Size.Infinity);
            _layer.Arrange(new Rect(_button.Bounds.Size));
        }
    }
}
