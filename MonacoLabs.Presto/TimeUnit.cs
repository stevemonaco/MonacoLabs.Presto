namespace MonacoLabs.Presto;

/// <summary>Unit every duration, delay and time value is expressed in, selected through <see cref="Engine.TimeUnit"/>.</summary>
public enum TimeUnit
{
    /// <summary>Milliseconds, the default (upstream <c>'ms'</c>).</summary>
    Milliseconds,

    /// <summary>Seconds, so a duration of 1 lasts one second (upstream <c>'s'</c>).</summary>
    Seconds,
}
