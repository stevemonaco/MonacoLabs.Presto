using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Threading;
using Monaco.Presto.AvaloniaBridge.Compositor;
using Monaco.Presto.Diagnostics;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>An Avalonia page transition that plays <see cref="Enter"/> on the incoming host and <see cref="Exit"/> on the outgoing one.</summary>
/// <remarks>
/// It holds builders, not animations, so one instance can serve several hosts and every transition reads
/// <see cref="Recipes.Tokens"/>, reduced motion and function values afresh. A cancelled transition leaves both hosts where
/// they stand for the next one to take over; a host no transition claims by the next frame is reverted.
/// </remarks>
public sealed class PrestoPageTransition : IPageTransition, IProgressPageTransition
{
    private static readonly ConditionalWeakTable<Visual, HostRun> s_hosts = new();

    /// <summary>Plays on the incoming host, stating the values it arrives from; null leaves it still.</summary>
    public AnimationBuilder? Enter { get; set; }

    /// <summary>Plays on the outgoing host until it is hidden, stating the values it leaves towards; null leaves it still.</summary>
    public AnimationBuilder? Exit { get; set; }

    /// <summary>Plays on the incoming host going back; null plays <see cref="Exit"/> reversed.</summary>
    public AnimationBuilder? BackEnter { get; set; }

    /// <summary>Plays on the outgoing host going back; null plays <see cref="Enter"/> reversed.</summary>
    public AnimationBuilder? BackExit { get; set; }

    /// <summary>How the two hosts stack while the transition runs.</summary>
    public PageStacking Stacking { get; set; } = PageStacking.IncomingAboveForward;

    /// <summary>True plays on the render thread, false on the UI thread, and null on the render thread whenever every property has a composition counterpart.</summary>
    public bool? UseCompositor { get; set; }

    private (Visual? From, Visual? To, bool Forward, HostRun? Incoming, HostRun? Outgoing)? _progress;

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException"><see cref="UseCompositor"/> is true and a builder in play sets something the compositor has no counterpart for.</exception>
    public Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return Task.CompletedTask;
        var (incoming, outgoing) = Begin(from, to, forward, seekable: false);
        return Finish(incoming, outgoing, cancellationToken);
    }

    /// <inheritdoc/>
    /// <remarks>Seeks paused UI-thread animations, never compositor ones, and does nothing while several pages are visible.</remarks>
    public void Update(double progress, Visual? from, Visual? to, bool forward, double pageLength, IReadOnlyList<PageTransitionItem> visibleItems)
    {
        if (visibleItems.Count > 0) return;
        if (_progress is not { } run || run.From != from || run.To != to || run.Forward != forward
            || run.Incoming?.IsCurrent == false || run.Outgoing?.IsCurrent == false)
        {
            _progress?.Incoming?.Release();
            _progress?.Outgoing?.Release();
            var (incoming, outgoing) = Begin(from, to, forward, seekable: true);
            _progress = run = (from, to, forward, incoming, outgoing);
        }
        run.Incoming?.Seek(progress);
        run.Outgoing?.Seek(progress);
    }

    /// <inheritdoc/>
    public void Reset(Visual visual)
    {
        if (s_hosts.TryGetValue(visual, out var run)) run.Release();
    }

    private (HostRun? Incoming, HostRun? Outgoing) Begin(Visual? from, Visual? to, bool forward, bool seekable)
    {
        PrestoAvalonia.EnsureInitialized();
        using var origin = PrestoDiagnostics.Origin("PageTransition");
        var enter = to == null ? null : forward ? Enter?.ToParams() : BackEnter?.ToParams() ?? Reversed(Exit);
        var exit = from == null ? null : forward ? Exit?.ToParams() : BackExit?.ToParams() ?? Reversed(Enter);
        var compositor = !seekable && (UseCompositor ?? ((enter == null || PrestoAvalonia.FitsCompositor(enter)) && (exit == null || PrestoAvalonia.FitsCompositor(exit))));
        bool? incomingAbove = Stacking switch
        {
            PageStacking.IncomingAboveForward => forward,
            PageStacking.IncomingAbove => true,
            PageStacking.OutgoingAbove => false,
            _ => null,
        };

        if (to != null) to.IsVisible = true;
        var incoming = to == null ? null : new HostRun(to, enter, compositor, seekable, incomingAbove is { } above ? above ? 1 : 0 : null);
        var outgoing = from == null ? null : new HostRun(from, exit, compositor, seekable, incomingAbove is { } below ? below ? 0 : 1 : null);
        return (incoming, outgoing);
    }

    private static AnimationParams? Reversed(AnimationBuilder? builder) =>
        builder?.ToParams() is { } parameters ? parameters with { Reversed = parameters.Reversed != true } : null;

    private static async Task Finish(HostRun? incoming, HostRun? outgoing, CancellationToken cancellationToken)
    {
        using (cancellationToken.Register(() =>
        {
            incoming?.Cancel();
            outgoing?.Cancel();
        }))
        {
            // Awaited one by one, not through Task.WhenAll, so completion posts straight to the UI context without a thread-pool hop.
            await (incoming?.Done ?? Task.CompletedTask);
            await (outgoing?.Done ?? Task.CompletedTask);
        }

        if (cancellationToken.IsCancellationRequested) return;
        // Hidden before anything reverts, so the outgoing host never shows at rest.
        if (outgoing is { IsCurrent: true }) outgoing.Hide();
        incoming?.Release();
        outgoing?.Release();
    }

    private sealed class HostRun
    {
        private readonly Visual _host;
        private readonly Animation? _animation;
        private readonly Animation? _stood;
        private readonly CompositorAnimation? _compositorAnimation;
        private readonly IDisposable? _zIndex;
        private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private HostRun? _previous;

        public HostRun(Visual host, AnimationParams? parameters, bool compositor, bool seekable, int? zIndex)
        {
            _host = host;
            HostRun? otherThread = null;
            if (s_hosts.TryGetValue(host, out var previous))
            {
                previous.Stop();
                // A seek measures from the host's base values, so it cannot start where a running transition stands.
                if (seekable) previous.Revert();
                else if (parameters != null && (compositor ? previous._animation != null : previous._compositorAnimation != null)) otherThread = previous;
                else _previous = previous;
            }
            s_hosts.AddOrUpdate(host, this);

            if (zIndex is { } z) _zIndex = host.SetValue(Visual.ZIndexProperty, z, BindingPriority.Animation);
            if (parameters == null)
            {
                _done.SetResult();
            }
            else if (compositor)
            {
                _compositorAnimation = PrestoAvalonia.RunOnCompositor(host, parameters);
                // Reverted once the compositor has seeded from it, so both land in one commit and the offset is never drawn twice.
                otherThread?.Revert();
                _ = _compositorAnimation.Then(_ => _done.TrySetResult());
            }
            else
            {
                // The UI thread reads its start values from the host, so the host has to stand where the compositor left it.
                if (otherThread != null)
                {
                    var mirror = otherThread._compositorAnimation!.ScratchOf(host)!;
                    var stood = new AnimationParams
                    {
                        ["X"] = mirror.X,
                        ["Y"] = mirror.Y,
                        ["Rotate"] = mirror.Rotate,
                        ["Scale"] = mirror.Scale,
                        ["ScaleX"] = mirror.ScaleX,
                        ["ScaleY"] = mirror.ScaleY,
                        ["Opacity"] = mirror.Opacity,
                    };
                    // Read before the revert, which winds the mirror back to its start.
                    otherThread.Revert();
                    _stood = Presto.Set(host, stood);
                }
                _animation = Presto.Animate(host, parameters with { Autoplay = seekable ? false : parameters.Autoplay, HostLifetime = WhenDetached.Continue });
                _ = _animation.Then(_ => _done.TrySetResult());
            }
        }

        public Task Done => _done.Task;

        public bool IsCurrent => s_hosts.TryGetValue(_host, out var run) && run == this;

        public void Cancel()
        {
            Stop();
            if (TopLevel.GetTopLevel(_host) is { } top) top.RequestAnimationFrame(_ => Dispatcher.UIThread.Post(Release));
            else Release();
        }

        public void Hide() => _host.IsVisible = false;

        public void Seek(double progress) => _animation?.Seek(progress * _animation.Duration);

        public void Release()
        {
            if (!IsCurrent) return;
            s_hosts.Remove(_host);
            Revert();
        }

        private void Stop()
        {
            _animation?.Cancel();
            // A pause keeps the composition properties registered, so the next compositor animation seeds from where they stand.
            _compositorAnimation?.Pause();
            _done.TrySetResult();
        }

        private void Revert()
        {
            _animation?.Revert();
            _stood?.Revert();
            _compositorAnimation?.Revert();
            _zIndex?.Dispose();
            _previous?.Revert();
            _previous = null;
        }
    }
}
