namespace NewGame1.Core.Diagnostics;

/// <summary>
/// Distinguishes a mid-session <see cref="FrameTimeStatistics"/> snapshot from the end-of-session
/// record (FR-046a).
/// </summary>
public enum FrameTimeStatisticsKind
{
    Interim,
    Final,
}

/// <summary>
/// Immutable snapshot of a <see cref="FrameTimeHistogram"/> (FR-041, FR-042, FR-044, FR-046a). An
/// empty histogram still produces a constructible, low-confidence snapshot with absent percentiles
/// rather than throwing or reporting 0.
/// </summary>
public sealed record FrameTimeStatistics(
    double AverageMs,
    double? P95Ms,
    double? P99Ms,
    double WorstMs,
    long SampleCount,
    FrameTimeStatisticsKind Kind = FrameTimeStatisticsKind.Interim)
{
    /// <summary>True when <see cref="SampleCount"/> is below the 1000-sample confidence threshold (FR-044).</summary>
    public bool IsLowConfidence => SampleCount < 1000;
}
