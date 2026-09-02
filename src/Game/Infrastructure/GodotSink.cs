using Godot;
using Serilog.Core;
using Serilog.Events;

namespace NewGame1.Infrastructure;

/// <summary>
/// Serilog sink bridging entries to <c>GD.Print</c>/<c>GD.PushError</c> so they also appear on
/// the terminal in a headless run (FR-007). The only file in the repository permitted to call
/// those (constitution III).
/// </summary>
public sealed class GodotSink : ILogEventSink
{
    private readonly IFormatProvider? _formatProvider;

    public GodotSink(IFormatProvider? formatProvider = null)
    {
        _formatProvider = formatProvider;
    }

    public void Emit(LogEvent logEvent)
    {
        var sourceContext = logEvent.Properties.TryGetValue("SourceContext", out var value)
            ? value.ToString().Trim('"')
            : "General";

        var line = $"{logEvent.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{logEvent.Level}] {sourceContext}: {logEvent.RenderMessage(_formatProvider)}";

        if (logEvent.Exception is not null)
        {
            line += System.Environment.NewLine + logEvent.Exception;
        }

        if (logEvent.Level >= LogEventLevel.Error)
        {
            GD.PushError(line);
        }
        else
        {
            GD.Print(line);
        }
    }
}
