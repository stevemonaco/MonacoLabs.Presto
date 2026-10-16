namespace Tempo.ViewModels;

/// <summary>A change to the saved data that the confirm dialog asks about first.</summary>
public enum DataAction
{
    /// <summary><see cref="MainViewModel.RestoreSampleData"/>.</summary>
    Restore,
    /// <summary><see cref="MainViewModel.ClearAll"/>.</summary>
    Clear,
}
