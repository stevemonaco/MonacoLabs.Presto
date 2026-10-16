using Avalonia.Logging;

namespace MonacoLabs.Presto.Tests;

/// <summary>Records what is logged to one area at or above a level, replacing <see cref="Logger.Sink"/> until disposed.</summary>
internal sealed class LogSink : ILogSink, IDisposable
{
    private readonly ILogSink? _previous = Logger.Sink;
    private readonly string _area;
    private readonly LogEventLevel _minimum;

    public LogSink(string area, LogEventLevel minimum)
    {
        _area = area;
        _minimum = minimum;
        Logger.Sink = this;
    }

    public List<(LogEventLevel Level, string Message)> Entries { get; } = [];

    public bool IsEnabled(LogEventLevel level, string area) => level >= _minimum && area == _area;

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate) => Entries.Add((level, messageTemplate));

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues) =>
        Entries.Add((level, messageTemplate));

    public void Dispose() => Logger.Sink = _previous;
}
