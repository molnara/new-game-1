using Godot;
using Serilog.Core;
using Serilog.Events;

namespace NewGame1.Infrastructure;

// Serilog sink bridging entries to GD.Print/GD.PushError so they also appear on the terminal in a
// headless run (FR-007). One of only two files permitted to call those (constitution III);
// ProcessOutput is the other.
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
