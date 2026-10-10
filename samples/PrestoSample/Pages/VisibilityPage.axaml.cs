using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MonacoLabs.Presto;
using PrestoSample.Showcase;
using Timer = MonacoLabs.Presto.Timer;

namespace PrestoSample.Pages;

/// <summary>
/// Show and hide through a bound <see cref="Visual.IsVisible"/>: a card under each <see cref="PresenceHideMode"/>, and a
/// Fluent <see cref="Expander"/> whose rows re-enter through <c>Presence.EnterOnShow</c>, beside a log of what played.
/// </summary>
public partial class VisibilityPage : ShowcasePage
{
    private static readonly IBrush ShowTint = Brush.Parse("#86efac");
    private static readonly IBrush HideTint = Brush.Parse("#fca5a5");
    private static readonly IBrush EnterTint = Brush.Parse("#93c5fd");

    public VisibilityPage()
    {
        InitializeComponent();
        CardCode.Code = $"{Snippets.VisibilityHoldCard}\n\n{Snippets.VisibilityGhostCard}";
        SettingsCode.Code = $"{Snippets.VisibilitySettingsStyles}\n\n{Snippets.VisibilitySettings}";
    }

    // Style setters have applied by now, so the rows' shared entrance can be found on any of them.
    protected override void OnAttached()
    {
        Presence.GetEnter(HoldCard)?.OnBegin(_ => Moments.Record("Show", ShowTint, "Hold card: Enter"));
        Presence.GetExit(HoldCard)?.OnBegin(_ => Moments.Record("Hide", HideTint, "Hold card: Exit on the live card"));
        Presence.GetEnter(GhostCard)?.OnBegin(_ => Moments.Record("Show", ShowTint, "Ghost card: Enter"));
        Presence.GetExit(GhostCard)?.OnBegin(_ => Moments.Record("Hide", HideTint, "Ghost card: Exit on a ghost"));
        var row = ((Panel)Settings.Content!).Children[0];
        Presence.GetEnter(row)?.OnBegin(timer => Moments.Record("Enter", EnterTint, $"\"{RowName(timer)}\" replayed by EnterOnShow"));
    }

    private static string? RowName(Timer timer) =>
        ((Animation)timer).Targets[0] is Border { Child: ToggleSwitch { Content: string name } } ? name : null;
}
