using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using MonacoLabs.Presto;
using PrestoGallery.Showcase;

namespace PrestoGallery.Pages;

/// <summary>
/// <c>Motion</c> states on real controls: hover, press and focus sugar, <c>:checked</c> and <c>:focus</c> through
/// <c>Motion.States</c>, a parent's hover reaching a child through a style setter on <c>Motion.State</c>, a bound status
/// chip, a card driven by a view model's enum and a class binding, and a menu whose items follow the panel's state in a stagger. The log is written from the controls' own class and
/// property changes.
/// </summary>
public partial class StatesPage : UserControl
{
    public static readonly StyledProperty<string> StatusProperty =
        AvaloniaProperty.Register<StatesPage, string>(nameof(Status), "Idle");

    private const int MaxEntries = 14;

    private static readonly string[] Statuses = ["Idle", "Syncing", "Failed", "Done"];
    private static readonly string[] Watched = [":pointerover", ":pressed", ":focus", ":focus-visible", ":checked", "starred"];
    private static readonly StaggerFrom[] Froms = [StaggerFrom.First, StaggerFrom.Last, StaggerFrom.Center];

    #region snippet StatesBuilders
    public static readonly AnimationBuilder Lift = Presto.Build().Y(-4).Duration(220).Ease(Eases.OutQuart);
    public static readonly AnimationBuilder Squish = Presto.Build().Scale(.95).Duration(120).Ease(Eases.OutQuad);
    public static readonly AnimationBuilder Ring = Presto.Build().Scale(1.06).Duration(240).Ease(Eases.OutBack(2));
    public static readonly AnimationBuilder Grow = Presto.Build().Width(280).Duration(260).Ease(Eases.OutQuart);
    public static readonly AnimationBuilder Done = Presto.Build().X(12).Opacity(.5).Duration(260).Ease(Eases.OutQuart);
    public static readonly AnimationBuilder Nudge = Presto.Build().X(6).Duration(200).Ease(Eases.OutQuart);
    public static readonly AnimationBuilder Pulse = Presto.Build()
        .Opacity(k => k.To(.35).To(1))
        .Duration(900)
        .Ease(Eases.InOutSine)
        .Loop()
        .WhenReduced(reduced => reduced.Loop(false));
    public static readonly AnimationBuilder Alarm = Presto.Build()
        .Background(Palette.Coral)
        .X(k => k.To(-6).To(6).To(-3).To(0))
        .Duration(360);
    public static readonly AnimationBuilder Calm = Presto.Build().Duration(900).Ease(Eases.OutQuad);
    public static readonly AnimationBuilder Success = Presto.Build().Background(Palette.Aqua).Duration(300);
    public static readonly AnimationBuilder Dim = Presto.Build().Opacity(.45).Duration(300).Ease(Eases.OutQuad);
    public static readonly AnimationBuilder Star = Presto.Build().Y(-4).Property("BorderBrush", TweenValue.Of(Palette.Amber)).Duration(260).Ease(Eases.OutQuart);
    public static readonly AnimationBuilder DarkInk = Presto.Build().Foreground(Palette.Background).Duration(300);
    public static readonly AnimationBuilder Trophy = Presto.Build().Scale(1.06).Duration(420).Ease(Eases.OutBack(2));
    public static readonly AnimationBuilder Unfold = Presto.Build().Opacity(1).ScaleY(1, from: .6).Duration(220).Ease(Eases.OutQuart);
    public static readonly AnimationBuilder SlideIn = Presto.Build().Opacity(1).Y(0, from: 14).Duration(320).Ease(Eases.OutQuart);
    #endregion

    private readonly ObservableCollection<string> _entries = [];

    public StatesPage()
    {
        InitializeComponent();
        Log.ItemsSource = _entries;
        foreach (var control in Stage.GetLogicalDescendants().OfType<Control>())
        {
            if (control.Name is not { } name) continue;
            control.Classes.CollectionChanged += (_, e) => RecordClasses(name, e);
            control.PropertyChanged += (_, e) =>
            {
                if (e.Property == Motion.StateProperty) Record($"{name}  State = {e.NewValue ?? "none"}");
            };
        }
        Code.CSharp = Snippets.StatesBuilders;
        Code.Xaml = string.Join("\n\n", Snippets.StatesButton, Snippets.StatesTextBox, Snippets.StatesCheckBox, Snippets.StatesArrow, Snippets.StatesCard, Snippets.StatesChip, Snippets.StatesUpload, Snippets.StatesMenu, Snippets.StatesMenuItem);
        OrderPicker.SelectedIndex = (int)Motion.GetOrder(MenuPanel);
        FromPicker.SelectedIndex = 0;
    }

    public string Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    private void RecordClasses(string name, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null) foreach (string item in e.OldItems) if (Watched.Contains(item)) Record($"{name}  − {item}");
        if (e.NewItems != null) foreach (string item in e.NewItems) if (Watched.Contains(item)) Record($"{name}  + {item}");
    }

    private void Record(string entry)
    {
        _entries.Insert(0, entry);
        while (_entries.Count > MaxEntries) _entries.RemoveAt(_entries.Count - 1);
    }

    private void ToggleMenu(object? sender, RoutedEventArgs e) => Motion.SetState(MenuPanel, MenuToggle.IsChecked == true ? "Open" : null);

    private void OrderChanged(object? sender, SelectionChangedEventArgs e)
    {
        Motion.SetOrder(MenuPanel, (MotionOrder)OrderPicker.SelectedIndex);
        Record($"MenuPanel  Order = {Motion.GetOrder(MenuPanel)}");
    }

    private void FromChanged(object? sender, SelectionChangedEventArgs e)
    {
        Motion.SetStaggerFrom(MenuPanel, Froms[FromPicker.SelectedIndex]);
        Record($"MenuPanel  Stagger {((ComboBoxItem)FromPicker.SelectedItem!).Content}");
    }

    private void NextStatus(object? sender, RoutedEventArgs e) => Status = Statuses[(Array.IndexOf(Statuses, Status) + 1) % Statuses.Length];
}
