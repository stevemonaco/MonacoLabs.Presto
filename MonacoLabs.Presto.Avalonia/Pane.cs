using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;

namespace MonacoLabs.Presto;

/// <summary>
/// A collapsible side pane in a <see cref="Grid"/> column: collapsing eases the column's width to 0 and expanding eases it
/// back to the width it had, such as one a <see cref="GridSplitter"/> dragged it to.
/// </summary>
/// <remarks>
/// <para>
/// While the width moves, the column's <see cref="ColumnDefinition.MinWidth"/> is lifted, and it is restored once an expand
/// ends. Below that minimum the content keeps its width and is clipped, through a negative right margin, rather than
/// reflowed, so the column's cell should clip (a <see cref="Border"/> with <see cref="Visual.ClipToBounds"/>). The column
/// ends on a local pixel width, so a splitter resizes it as before.
/// </para>
/// <para>Collapsing takes <see cref="MotionTokens.Short"/> and expanding <see cref="MotionTokens.Medium"/>; under
/// <see cref="PrestoDefaults.ReducedMotion"/> the width jumps.</para>
/// </remarks>
public static class Pane
{
    private static readonly ConditionalWeakTable<ColumnDefinition, State> s_states = new();

    /// <summary>Whether <paramref name="column"/> was last collapsed rather than expanded.</summary>
    public static bool IsCollapsed(ColumnDefinition column) => s_states.TryGetValue(column, out var state) && state.Collapsed;

    /// <summary>Eases <paramref name="column"/> to 0 wide, remembering its width, with <paramref name="content"/> clipped rather than reflowed.</summary>
    public static Animation Collapse(ColumnDefinition column, Control content) => Move(column, content, collapse: true);

    /// <summary>Eases <paramref name="column"/> back to the width it had when it was collapsed.</summary>
    public static Animation Expand(ColumnDefinition column, Control content) => Move(column, content, collapse: false);

    /// <summary>Collapses <paramref name="column"/> if it is expanded, and expands it otherwise.</summary>
    public static Animation Toggle(ColumnDefinition column, Control content) => Move(column, content, !IsCollapsed(column));

    private static Animation Move(ColumnDefinition column, Control content, bool collapse)
    {
        PrestoAvalonia.EnsureInitialized();
        var state = s_states.GetValue(column, static c => new State { MinWidth = c.MinWidth });
        // The column keeps its MinWidth only while expanded and at rest, which is when a splitter may have moved it.
        if (!state.Collapsed && column.MinWidth > 0) state.Expanded = column.ActualWidth;
        state.Collapsed = collapse;
        column.MinWidth = 0;
        var width = collapse ? 0 : state.Expanded;
        var tokens = Recipes.Tokens;
        return Presto.Animate(column, new AnimationParams
        {
            ["Width"] = width,
            Duration = collapse ? tokens.Short : tokens.Medium,
            Ease = tokens.Ease,
            WhenReduced = new() { ["Width"] = width, Duration = 0 },
            OnUpdate = _ =>
            {
                var margin = content.Margin;
                content.Margin = new Thickness(margin.Left, margin.Top, Math.Min(0, column.Width.Value - state.MinWidth), margin.Bottom);
            },
            OnComplete = animation =>
            {
                // Handed back as a local value, so the animation's hold no longer masks a splitter's writes.
                column.Width = new GridLength(width);
                animation.Revert();
                if (!collapse) column.MinWidth = state.MinWidth;
            },
        });
    }

    private sealed class State
    {
        public double MinWidth { get; init; }

        public double Expanded { get; set; }

        public bool Collapsed { get; set; }
    }
}
