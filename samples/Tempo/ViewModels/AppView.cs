namespace Tempo.ViewModels;

/// <summary>The main area's view, picked by the title bar's switch, or in compact by the bottom bar.</summary>
public enum AppView
{
    Focus,
    Stats,

    /// <summary>The task list as a page of its own, shown only in compact.</summary>
    Tasks,
}
