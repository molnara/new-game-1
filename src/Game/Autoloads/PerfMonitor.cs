using Godot;
using Microsoft.Extensions.Logging;
using NewGame1.Core.Diagnostics;
using NewGame1.Infrastructure;

namespace NewGame1.Autoloads;

/// <summary>
/// Always-on frame-time sampler (FR-045, FR-045a). Accumulates every frame's delta into a
/// <see cref="FrameTimeHistogram"/> from startup regardless of overlay visibility, and writes
/// interim statistics records to the session log on a configurable interval (default 30 s,
/// FR-046) plus one final record at shutdown. Interim and final records are distinguishable
/// (FR-046a) and each is flushed to disk as written (FR-046b). The overlay itself is added in a
/// later task.
/// </summary>
public partial class PerfMonitor : Node
{
    public const double DefaultStatisticsIntervalSeconds = 30.0;

    private readonly FrameTimeHistogram _histogram = new();

    private ILogger<PerfMonitor> _logger = null!;
    private double _statisticsIntervalSeconds;
    private double _timeSinceLastRecord;
    private bool _finalRecordWritten;

    public override void _Ready()
    {
        Logging.Initialize();
        _logger = Logging.For<PerfMonitor>();
        ProcessMode = ProcessModeEnum.Always;

        _statisticsIntervalSeconds = ResolveStatisticsInterval();
    }

    public override void _Process(double delta)
    {
        _histogram.Add(delta * 1000.0);

        _timeSinceLastRecord += delta;
        if (_timeSinceLastRecord < _statisticsIntervalSeconds)
        {
            return;
        }

        _timeSinceLastRecord = 0;
        WriteStatistics(FrameTimeStatisticsKind.Interim);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            WriteFinalRecordOnce();
        }
    }

    public override void _ExitTree()
    {
        WriteFinalRecordOnce();
    }

    private void WriteFinalRecordOnce()
    {
        if (_finalRecordWritten)
        {
            return;
        }

        _finalRecordWritten = true;
        WriteStatistics(FrameTimeStatisticsKind.Final);
    }

    private void WriteStatistics(FrameTimeStatisticsKind kind)
    {
        var stats = _histogram.Snapshot(kind);

        _logger.LogInformation(
            "Frame time statistics ({Kind}): average={AverageMs:F3}ms p95={P95} p99={P99} worst={WorstMs:F3}ms samples={SampleCount} lowConfidence={IsLowConfidence}",
            kind,
            stats.AverageMs,
            FormatPercentile(stats.P95Ms),
            FormatPercentile(stats.P99Ms),
            stats.WorstMs,
            stats.SampleCount,
            stats.IsLowConfidence);

        Logging.FlushNow();
    }

    private static string FormatPercentile(double? valueMs) =>
        valueMs.HasValue ? $"{valueMs.Value:F3}ms" : "unavailable";

    private static double ResolveStatisticsInterval()
    {
        var userArgs = OS.GetCmdlineUserArgs();
        if (TryGetFlagValue(userArgs, "--stats-interval", out var value)
            && double.TryParse(value, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        return DefaultStatisticsIntervalSeconds;
    }

    private static bool TryGetFlagValue(string[] args, string flag, out string value)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith(flag + "=", StringComparison.Ordinal))
            {
                value = args[i][(flag.Length + 1)..];
                return true;
            }

            if (args[i] == flag && i + 1 < args.Length)
            {
                value = args[i + 1];
                return true;
            }
        }

        value = "";
        return false;
    }
}
