using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using MonacoLabs.Presto;

namespace Tempo.Views;

/// <summary>A − / value / + stepper, clamped to its limits, whose value morphs glyph by glyph on each step (set at once under reduced motion); a bound change sets it at once.</summary>
public partial class Stepper : UserControl
{
    public static readonly StyledProperty<int> ValueProperty =
        AvaloniaProperty.Register<Stepper, int>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<int> MaximumProperty = AvaloniaProperty.Register<Stepper, int>(nameof(Maximum), 60);

    private const int Min = 1;

    private Timeline? _morph;
    private bool _stepping;

    public Stepper()
    {
        InitializeComponent();
        Number.Text = "0";
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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
            ShowValue();
    }

    private void Step(object? sender, RoutedEventArgs e)
    {
        _stepping = true;
        Value = Math.Clamp(Value + (sender == Increase ? 1 : -1), Min, Maximum);
        _stepping = false;
    }

    // Code-only: MorphGlyphs has no XAML trigger on a binding change (BACKLOG, BRIDGE-RECIPES).
    // Only a step morphs: a surface move drops the DataContext while loaded, and that morph, cut off in the native popup, blanked it.
    private void ShowValue()
    {
        var text = Value.ToString(CultureInfo.InvariantCulture);
        if (_stepping && IsLoaded && !Presto.Defaults.ReducedMotion)
        {
            _morph = Recipes.MorphGlyphs(Number, text);
            return;
        }
        _morph?.Complete();
        Number.Text = text;
    }
}
