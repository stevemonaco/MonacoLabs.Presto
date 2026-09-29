using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase;

namespace PrestoSample.Pages;

/// <summary>
/// <c>Motion</c> states on real controls: hover, press and focus sugar, <c>:checked</c> and <c>:focus</c> through
/// <c>Motion.States</c>, a parent's hover reaching a child through a style setter on <c>Motion.State</c>, a bound status
/// chip, and a card driven by a view model's enum and a class binding. The log is written from the controls' own class and
/// property changes. The XAML snippets are constants pinned to the markup by a test.
/// </summary>
public partial class StatesPage : UserControl
{
    public const string ButtonXaml = """
        <Button x:Name="SaveButton" Classes="specimen" Content="Save"
                presto:Motion.Hover="{x:Static local:StatesPage.Lift}"
                presto:Motion.Press="{x:Static local:StatesPage.Squish}"
                presto:Motion.Focus="{x:Static local:StatesPage.Ring}" />
        """;

    public const string TextBoxXaml = """
        <TextBox x:Name="SearchBox" Width="180" HorizontalAlignment="Left" PlaceholderText="Search">
            <presto:Motion.States>
                <presto:MotionStates>
                    <presto:MotionState When=":focus" Motion="{x:Static local:StatesPage.Grow}" />
                </presto:MotionStates>
            </presto:Motion.States>
        </TextBox>
        """;

    public const string CheckBoxXaml = """
        <Style Selector="CheckBox.task">
            <Setter Property="presto:Motion.Focus" Value="{x:Static local:StatesPage.Ring}" />
            <Setter Property="presto:Motion.States">
                <presto:MotionStates>
                    <presto:MotionState When=":checked" Motion="{x:Static local:StatesPage.Done}" />
                </presto:MotionStates>
            </Setter>
        </Style>
        """;

    public const string ArrowXaml = """
        <Style Selector="Path.arrow">
            <Setter Property="presto:Motion.States">
                <presto:MotionStates>
                    <presto:MotionState When="hot" Motion="{x:Static local:StatesPage.Nudge}" />
                </presto:MotionStates>
            </Setter>
        </Style>
        <Style Selector="Border.card:pointerover Path.arrow">
            <Setter Property="presto:Motion.State" Value="hot" />
        </Style>
        """;

    public const string CardXaml = """
        <Border x:Name="InboxCard" Classes="card"
                presto:Motion.Hover="{x:Static local:StatesPage.Lift}"
                presto:Motion.Press="{x:Static local:StatesPage.Squish}">
        """;

    public const string ChipXaml = """
        <Border x:Name="StatusChip" Classes="status" presto:Motion.State="{Binding $parent[local:StatesPage].Status}">
            <presto:Motion.States>
                <presto:MotionStates>
                    <presto:MotionState When="Syncing" Motion="{x:Static local:StatesPage.Pulse}" />
                    <presto:MotionState When="Failed" Motion="{x:Static local:StatesPage.Alarm}" Leave="{x:Static local:StatesPage.Calm}" />
                    <presto:MotionState When="Done" Motion="{x:Static local:StatesPage.Success}" />
                </presto:MotionStates>
            </presto:Motion.States>
        """;

    public const string UploadXaml = """
        <Border x:Name="UploadCard" Classes="card" Width="220"
                Classes.starred="{Binding IsStarred}" presto:Motion.State="{Binding Phase}">
            <presto:Motion.States>
                <presto:MotionStates>
                    <presto:MotionState When="Uploading" Motion="{x:Static local:StatesPage.Pulse}" />
                    <presto:MotionState When="Paused" Motion="{x:Static local:StatesPage.Dim}" />
                    <presto:MotionState When="Uploaded" Motion="{x:Static local:StatesPage.Success}" />
                    <presto:MotionState When=".starred" Motion="{x:Static local:StatesPage.Star}" />
                    <presto:MotionState When="Uploaded.starred" Motion="{x:Static local:StatesPage.Trophy}" />
                </presto:MotionStates>
            </presto:Motion.States>
            <TextBlock Text="{Binding Phase, StringFormat='report.pdf · {0}'}" presto:Motion.State="{Binding Phase}">
                <presto:Motion.States>
                    <presto:MotionStates>
                        <presto:MotionState When="Uploaded" Motion="{x:Static local:StatesPage.DarkInk}" />
                    </presto:MotionStates>
                </presto:Motion.States>
            </TextBlock>
        </Border>
        """;

    public static readonly StyledProperty<string> StatusProperty =
        AvaloniaProperty.Register<StatesPage, string>(nameof(Status), "Idle");

    private const int MaxEntries = 14;

    private static readonly string[] Statuses = ["Idle", "Syncing", "Failed", "Done"];
    private static readonly string[] Watched = [":pointerover", ":pressed", ":focus", ":focus-visible", ":checked", "starred"];

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
        .Loop();
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
        XamlCode.Text = string.Join("\n\n", ButtonXaml, TextBoxXaml, CheckBoxXaml, ArrowXaml, $"{CardXaml}\n    …", $"{ChipXaml}\n    …", UploadXaml);
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

    private void NextStatus(object? sender, RoutedEventArgs e) => Status = Statuses[(Array.IndexOf(Statuses, Status) + 1) % Statuses.Length];
}
