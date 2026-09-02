namespace NewGame1.Core.Diagnostics;

/// <summary>
/// Core-declared engine service for the overlay and logged statistics' non-frame-time metrics
/// (FR-037, FR-047; research R11; constitution I). Frame time and FPS are deliberately not on this
/// interface: frame time comes from the engine's per-frame delta and FPS is derived from it as
/// <c>1000 / frameMs</c>, because the engine's own FPS counter reads a frozen 1.0 in short runs
/// (research R11).
/// </summary>
public interface IPerformanceCounters
{
    /// <summary>Draw calls in the current frame, or null when unavailable in this run's environment (FR-041a).</summary>
    long? DrawCalls { get; }

    /// <summary>Video memory in use, in bytes, or null when unavailable (FR-041a).</summary>
    long? VideoMemoryBytes { get; }

    /// <summary>Total process memory (RSS), in bytes, or null when unavailable (FR-041a).</summary>
    long? ProcessMemoryBytes { get; }
}
