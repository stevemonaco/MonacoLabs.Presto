using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace Tempo.Views;

/// <summary>A − / value / + stepper, clamped to its limits, whose value morphs glyph by glyph on each change (set at once under reduced motion).</summary>
public partial class Stepper : UserControl
{
    public static readonly StyledProperty<int> ValueProperty =
        AvaloniaProperty.Register<Stepper, int>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<int> MaximumProperty = AvaloniaProperty.Register<Stepper, int>(nameof(Maximum), 60);

    private const int Min = 1;

    public Stepper()
    {
        InitializeComponent();
    }

    public int Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public int Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    private void Step(object? sender, RoutedEventArgs e) => Value = Math.Clamp(Value + (sender == Increase ? 1 : -1), Min, Maximum);
}
