using Godot;
using NewGame1.Core.Diagnostics;

namespace NewGame1.Infrastructure;

// Game-side IPerformanceCounters reading Godot's render monitors and the OS process table
// (FR-047; research R11). ProcessMemoryBytes parses /proc/self/status VmRSS — not
// OS.GetMemoryInfo, which reports system RAM, and not Performance.MEMORY_STATIC, which
// under-reported the real figure by 11x in the spike. DrawCalls and VideoMemoryBytes report null
// under Godot's dummy (--headless) display server, which has no rasterizer to measure (FR-041a).
// Every member reads live state on each access; ProcessMemoryBytes is a file read, so callers must
// poll it at the overlay's 4 Hz refresh, never per frame.
public sealed class GodotPerformanceCounters : IPerformanceCounters
{
    private const string ProcStatusPath = "/proc/self/status";

    public long? DrawCalls =>
        IsRenderingAvailable
            ? (long)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)
            : null;

    public long? VideoMemoryBytes =>
        IsRenderingAvailable
            ? (long)Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed)
            : null;

    public long? ProcessMemoryBytes => ReadVmRssBytes();

    private static bool IsRenderingAvailable => DisplayServer.GetName() != "headless";

    private static long? ReadVmRssBytes()
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(ProcStatusPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        foreach (var line in lines)
        {
            if (!line.StartsWith("VmRSS:", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 && long.TryParse(parts[1], out var kilobytes)
                ? kilobytes * 1024
                : null;
        }

        return null;
    }
}
