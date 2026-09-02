using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.File;

namespace NewGame1.Infrastructure;

/// <summary>
/// Decorates the file sink, forcing an immediate disk flush via <see cref="IFlushableFileSink"/>
/// when an event is Warning or above, so warnings and errors survive an abrupt kill (FR-005;
/// research R7). No-op if the wrapped sink does not implement the interface — Logging.cs configures
/// the file sink (buffered, no <c>flushToDiskInterval</c>) so that it does; Serilog otherwise wraps
/// it in an internal <c>PeriodicFlushToDiskSink</c> that hides the flushable sink behind a private
/// field, which is the "awkward in practice" case research R7 anticipated.
/// </summary>
public sealed class WarnErrorFlushSink : ILogEventSink
{
    private readonly ILogEventSink _inner;
    private readonly IFlushableFileSink? _flushable;

    public WarnErrorFlushSink(ILogEventSink inner)
    {
        _inner = inner;
        _flushable = inner as IFlushableFileSink;
    }

    public void Emit(LogEvent logEvent)
    {
        _inner.Emit(logEvent);

        if (logEvent.Level >= LogEventLevel.Warning)
        {
            _flushable?.FlushToDisk();
        }
    }
}
