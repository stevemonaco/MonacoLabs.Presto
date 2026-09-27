using Avalonia.Controls;

namespace Monaco.Presto.AvaloniaBridge.Events;

/// <summary>Parameters for <see cref="PrestoAvalonia.OnScroll"/>; every one of them has a default, so all are optional.</summary>
/// <remarks>
/// Without a <see cref="Target"/> the observer watches the first control of whatever is linked to it, and without a
/// <see cref="Container"/> it watches the target's nearest <see cref="ScrollViewer"/> ancestor.
/// </remarks>
public sealed record ScrollObserverParams
{
    /// <summary>Names the observer, in place of the index it is created with.</summary>
    public string? Id { get; init; }

    /// <summary>The control whose crossings are watched.</summary>
    public Control? Target { get; init; }

    /// <summary>The scroller the target is watched in.</summary>
    public ScrollViewer? Container { get; init; }

    /// <summary>The axis to watch; vertical by default.</summary>
    public ScrollAxis? Axis { get; init; }

    /// <summary>Whether the observer keeps watching once the target has passed its leave threshold; true by default.</summary>
    public bool? Repeat { get; init; }

    /// <summary>Where the target enters the view; <c>"end start"</c> by default.</summary>
    public ScrollThreshold? Enter { get; init; }

    /// <summary>Where the target leaves the view; <c>"start end"</c> by default.</summary>
    public ScrollThreshold? Leave { get; init; }

    /// <summary>How the linked object follows the scroll; <c>"play pause"</c> by default.</summary>
    public ScrollSync? Sync { get; init; }

    /// <summary>Runs when the target crosses <see cref="Enter"/>, whichever way the scroller is moving.</summary>
    public Action<ScrollObserver>? OnEnter { get; init; }

    /// <summary>Runs when the target crosses <see cref="Leave"/>, whichever way the scroller is moving.</summary>
    public Action<ScrollObserver>? OnLeave { get; init; }

    /// <summary>Runs with <see cref="OnEnter"/>, only while the scroller moves towards the end of the content.</summary>
    public Action<ScrollObserver>? OnEnterForward { get; init; }

    /// <summary>Runs with <see cref="OnLeave"/>, only while the scroller moves towards the end of the content.</summary>
    public Action<ScrollObserver>? OnLeaveForward { get; init; }

    /// <summary>Runs with <see cref="OnEnter"/>, only while the scroller moves back towards the start.</summary>
    public Action<ScrollObserver>? OnEnterBackward { get; init; }

    /// <summary>Runs with <see cref="OnLeave"/>, only while the scroller moves back towards the start.</summary>
    public Action<ScrollObserver>? OnLeaveBackward { get; init; }

    /// <summary>Runs on every frame the target is in view on, and on any other frame its progress changes on.</summary>
    public Action<ScrollObserver>? OnUpdate { get; init; }

    /// <summary>Runs after the observer has measured itself against the container again, following a resize of either.</summary>
    public Action<ScrollObserver>? OnResize { get; init; }

    /// <summary>Invoked once the linked object has reached the end of the sync range.</summary>
    public Action<ScrollObserver>? OnSyncComplete { get; init; }
}
