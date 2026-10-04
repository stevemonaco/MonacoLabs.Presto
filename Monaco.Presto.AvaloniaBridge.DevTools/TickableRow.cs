using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;

namespace Monaco.Presto.AvaloniaBridge.DevTools;

/// <summary>One line of the tickable list: a root, a timeline child or a tween, kept across refreshes and updated in place.</summary>
internal sealed partial class TickableRow : INotifyPropertyChanged
{
    private TickableState? _state;
    private double _progress;
    private string _iteration = "";
    private string _target = "";
    private string _detail = "";
    private bool _isEnded;
    private bool _isExpanded;
    private int _shape = -1;

    internal TickableRow(Timer timer, int depth)
    {
        Timer = timer;
        Indent = new Thickness(depth * 14, 0, 0, 0);
        Glyph = timer switch { Timeline => "≡", Animation => "◆", _ => "○" };
        Label = timer.Id;
        Origin = timer.Origin;
        ScopeName = timer.Scope is { } scope ? $"scope {(scope.Root is { } owner ? Summarize(owner) : scope.GetType().Name)}" : null;
    }

    internal TickableRow(Tween tween, int depth)
    {
        Tween = tween;
        Indent = new Thickness(depth * 14, 0, 0, 0);
        Glyph = "↳";
        Label = tween.Property;
        _target = Summarize(tween.Target);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    internal Timer? Timer { get; }

    internal Tween? Tween { get; }

    internal int Generation { get; set; }

    public Thickness Indent { get; }

    public string Glyph { get; }

    public string Label { get; }

    public string? Origin { get; }

    public string? ScopeName { get; }

    public bool IsExpandable => _shape > 0;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }

    public string Target
    {
        get => _target;
        private set => Set(ref _target, value);
    }

    /// <summary>A timer's animated property names, or a tween's from → current → to values.</summary>
    public string Detail
    {
        get => _detail;
        private set => Set(ref _detail, value);
    }

    public TickableState? State
    {
        get => _state;
        private set
        {
            if (Set(ref _state, value)) OnPropertyChanged(nameof(StateText));
        }
    }

    public string StateText => _state?.ToString().ToLowerInvariant() ?? "";

    public double Progress
    {
        get => _progress;
        private set => Set(ref _progress, value);
    }

    public string Iteration
    {
        get => _iteration;
        private set => Set(ref _iteration, value);
    }

    public bool IsEnded
    {
        get => _isEnded;
        private set => Set(ref _isEnded, value);
    }

    internal bool Matches(string search) =>
        Label.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        _target.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        ScopeName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true ||
        _detail.Contains(search, StringComparison.OrdinalIgnoreCase);

    /// <summary>Polls the timer or tween; <paramref name="parent"/> is the state of the timer this row sits under, null for a root.</summary>
    internal void Update(TickableState? parent, bool ended)
    {
        IsEnded = ended;
        if (Tween is { } tween)
        {
            Progress = ProgressOf(tween);
            Detail = Values(tween);
            return;
        }
        var timer = Timer!;
        State = timer.Completed ? TickableState.Completed
            : timer.Cancelled ? TickableState.Cancelled
            : parent is { } state && state != TickableState.Running ? state
            : parent is null && timer.Paused ? TickableState.Paused
            : timer.Began ? TickableState.Running
            : timer.CurrentTime < 0 || (parent is null && timer.Delay > 0) ? TickableState.Delay
            : TickableState.Pending;
        Progress = timer.Progress;
        var count = double.IsPositiveInfinity(timer.IterationCount) ? "∞" : timer.IterationCount.ToString();
        Iteration = $"{timer.CurrentIteration + 1}/{count}";
        var shape = Shape(timer);
        if (shape != _shape)
        {
            _shape = shape;
            OnPropertyChanged(nameof(IsExpandable));
            Describe(timer);
        }
    }

    internal static double ProgressOf(Tween tween)
    {
        var time = tween.Parent.IterationTime - tween.StartTime;
        return tween.Duration > 0 ? Math.Clamp(time / tween.Duration, 0, 1) : time >= 0 ? 1 : 0;
    }

    internal static string Values(Tween tween) => $"{Display(tween.GetFrom())} → {Display(tween.GetCurrent())} → {Display(tween.GetTo())}";

    internal static string Display(TweenSnapshot value) =>
        Decimal().Replace(value.Text, m => double.Parse(m.ValueSpan, CultureInfo.InvariantCulture).ToString("0.###", CultureInfo.InvariantCulture));

    [GeneratedRegex(@"-?\d+\.\d{4,}")]
    private static partial Regex Decimal();

    private void Describe(Timer timer)
    {
        var targets = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var properties = new List<string>();
        foreach (var tween in AllTweens(timer))
        {
            targets.Add(tween.Target);
            if (!properties.Contains(tween.Property)) properties.Add(tween.Property);
        }
        Target = targets.Count switch
        {
            0 => "",
            1 => Summarize(targets.First()),
            var count => $"{count} targets",
        };
        Detail = string.Join(", ", properties);
    }

    /// <summary>The tweens of <paramref name="timer"/>, then those of each of its descendants.</summary>
    internal static IEnumerable<Tween> AllTweens(Timer timer)
    {
        foreach (var tween in timer.Tweens) yield return tween;
        foreach (var child in timer.Children)
        {
            foreach (var tween in AllTweens(child)) yield return tween;
        }
    }

    internal static int Shape(Timer timer)
    {
        var count = 0;
        foreach (var _ in timer.Tweens) count++;
        foreach (var child in timer.Children) count += 1 + Shape(child);
        return count;
    }

    internal static string Summarize(object target) => target switch
    {
        StyledElement { Name.Length: > 0 } named => named.Name!,
        Control { Parent: Panel panel } control => $"{control.GetType().Name}[{panel.Children.IndexOf(control)}]",
        _ => target.GetType().Name,
    };

    private bool Set<T>(ref T field, T value, [CallerMemberName] string name = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
