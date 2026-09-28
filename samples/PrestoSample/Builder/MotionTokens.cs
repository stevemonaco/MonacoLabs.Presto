using Monaco.Presto;

namespace PrestoSample.Builder;

/// <summary>A motion system's named durations, eases and travel; recipes read these instead of literals, so one set restyles them all.</summary>
public sealed record MotionTokens(
    double Short,
    double Medium,
    double Long,
    EaseValue Standard,
    EaseValue Emphasized,
    double Distance,
    double StaggerStep);
