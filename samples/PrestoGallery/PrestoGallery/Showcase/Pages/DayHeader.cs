namespace PrestoGallery.Showcase.Pages;

/// <summary>The feed's heading over one day's events, an item of its own because <c>ListBox</c> has no grouping.</summary>
public sealed record DayHeader(DateTime Day)
{
    public string Label =>
        Day == DateTime.Today ? "TODAY" : Day == DateTime.Today.AddDays(-1) ? "YESTERDAY" : Day.ToString("dddd d MMMM").ToUpperInvariant();
}
