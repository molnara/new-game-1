using Godot;
using Microsoft.Extensions.Logging;
using NewGame1.Core.Diagnostics;
using NewGame1.Infrastructure;

namespace NewGame1.Autoloads;

/// <summary>
/// Always-on frame-time sampler and performance overlay (FR-037, FR-039, FR-045, FR-045a).
/// Accumulates every frame's delta into a <see cref="FrameTimeHistogram"/> from startup
/// regardless of overlay visibility, and writes interim statistics records to the session log on
/// a configurable interval (default 30 s, FR-046) plus one final record at shutdown. Interim and
/// final records are distinguishable (FR-046a) and each is flushed to disk as written (FR-046b).
/// The overlay is a child <see cref="CanvasLayer"/>, off by default (FR-038), refreshing about 4
/// times per second with that interval's average and worst frame (FR-039, FR-039a), frames per
/// second derived from the average (research R11), draw calls, and the two separately labelled
/// memory figures (FR-037, FR-047) — each reported as explicitly unavailable rather than zero when
/// the run's environment cannot supply it (FR-041a). Toggling the overlay only affects display;
/// sampling for the logged statistics is unaffected either way (FR-045).
/// </summary>
public partial class PerfMonitor : CanvasLayer
{
    public const double DefaultStatisticsIntervalSeconds = 30.0;
    private const double OverlayRefreshIntervalSeconds = 0.25;

    private readonly FrameTimeHistogram _histogram = new();
    private readonly IPerformanceCounters _counters;

    private ILogger<PerfMonitor> _logger = null!;
    private double _statisticsIntervalSeconds;
    private double _timeSinceLastRecord;
    private bool _finalRecordWritten;

    private ColorRect _overlayPanel = null!;
    private Label _overlayLabel = null!;
    private double _timeSinceOverlayRefresh;
    private double _overlayWindowSumMs;
    private double _overlayWindowWorstMs;
    private long _overlayWindowSampleCount;

    public PerfMonitor()
        : this(new GodotPerformanceCounters())
    {
    }

    internal PerfMonitor(IPerformanceCounters counters)
    {
        _counters = counters;
    }

    /// <summary>Whether the overlay is currently displayed. Off by default (FR-038).</summary>
    public bool IsOverlayVisible => _overlayPanel.Visible;

    /// <summary>Shows or hides the overlay. Sampling continues regardless (FR-045).</summary>
    public void SetOverlayVisible(bool visible)
    {
        _overlayPanel.Visible = visible;
        ResetOverlayWindow();

        if (!visible)
        {
            return;
        }

        _overlayLabel.Text = "Perf overlay: waiting for samples...";
    }

    public override void _Ready()
    {
        Logging.Initialize();
        _logger = Logging.For<PerfMonitor>();
        ProcessMode = ProcessModeEnum.Always;
        Layer = 90;

        _statisticsIntervalSeconds = ResolveStatisticsInterval();

        BuildOverlayUi();
    }

    public override void _Process(double delta)
    {
        var frameMs = delta * 1000.0;
        _histogram.Add(frameMs);

        _timeSinceLastRecord += delta;
        if (_timeSinceLastRecord >= _statisticsIntervalSeconds)
        {
            _timeSinceLastRecord = 0;
            WriteStatistics(FrameTimeStatisticsKind.Interim);
        }

        if (!_overlayPanel.Visible)
        {
            return;
        }

        _overlayWindowSumMs += frameMs;
        _overlayWindowWorstMs = Math.Max(_overlayWindowWorstMs, frameMs);
        _overlayWindowSampleCount++;

        _timeSinceOverlayRefresh += delta;
        if (_timeSinceOverlayRefresh < OverlayRefreshIntervalSeconds)
        {
            return;
        }

        RefreshOverlayText();
        ResetOverlayWindow();
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

    private void BuildOverlayUi()
    {
        // A solid background behind the text keeps it legible over bright, dark or busy scene
        // content (FR-039b) rather than relying on outline or shadow effects alone.
        _overlayPanel = new ColorRect { Color = new Color(0f, 0f, 0f, 0.65f), Visible = false };
        AddChild(_overlayPanel);
        _overlayPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        _overlayPanel.OffsetLeft = -260;
        _overlayPanel.OffsetTop = 8;
        _overlayPanel.OffsetRight = -8;
        _overlayPanel.OffsetBottom = 140;

        _overlayLabel = new Label();
        _overlayLabel.AddThemeColorOverride("font_color", Colors.White);
        _overlayLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _overlayLabel.OffsetLeft = 8;
        _overlayLabel.OffsetTop = 6;
        _overlayLabel.OffsetRight = -8;
        _overlayLabel.OffsetBottom = -6;
        _overlayPanel.AddChild(_overlayLabel);
    }

    private void RefreshOverlayText()
    {
        var averageMs = _overlayWindowSampleCount > 0 ? _overlayWindowSumMs / _overlayWindowSampleCount : 0.0;
        var fps = averageMs > 0.0 ? 1000.0 / averageMs : 0.0;

        _overlayLabel.Text =
            $"Frame time: {averageMs:F2} ms avg / {_overlayWindowWorstMs:F2} ms worst\n" +
            $"FPS: {fps:F1}\n" +
            $"Draw calls: {FormatCount(_counters.DrawCalls)}\n" +
            $"Process memory: {FormatBytes(_counters.ProcessMemoryBytes)}\n" +
            $"Video memory: {FormatBytes(_counters.VideoMemoryBytes)}";
    }

    private void ResetOverlayWindow()
    {
        _timeSinceOverlayRefresh = 0;
        _overlayWindowSumMs = 0;
        _overlayWindowWorstMs = 0;
        _overlayWindowSampleCount = 0;
    }

    private static string FormatCount(long? value) =>
        value.HasValue ? value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "unavailable";

    private static string FormatBytes(long? bytes) =>
        bytes.HasValue ? $"{bytes.Value / (1024.0 * 1024.0):F1} MB" : "unavailable";

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
