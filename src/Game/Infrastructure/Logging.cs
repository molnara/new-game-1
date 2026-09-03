using System.Text;
using Godot;
using Microsoft.Extensions.Logging;
using NewGame1.Core.Console;
using NewGame1.Core.Diagnostics;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Serilog.Formatting.Display;
using Serilog.Sinks.File;

using Timer = System.Threading.Timer;

namespace NewGame1.Infrastructure;

// The static For<T>() entry point (FR-004) over a Serilog pipeline: a file sink (buffered, flushed
// on a configurable interval and immediately on Warning/Error via WarnErrorFlushSink, FR-005), a
// GodotSink alongside it (FR-007), and a configurable minimum level defaulting to Information
// (FR-003). Also establishes the --log-level <level> launch-flag convention (research R5, R14)
// reused by console history (FR-019) and the statistics interval (FR-046).
public static partial class Logging
{
    // Default debug/information flush interval in seconds (FR-005); configurable via --flush-interval.
    public const double DefaultFlushIntervalSeconds = 1.0;

    private const string LogLineTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    private static readonly string[] ValidLevelNames = ["debug", "information", "warning", "error"];

    private static readonly LoggingLevelSwitch LevelSwitch = new(LogEventLevel.Information);

    private static ILoggerFactory? _factory;
    private static FileSink? _fileSink;
    private static Timer? _flushTimer;
    private static bool _shutDown;

    // The minimum severity currently in effect (FR-003); adjustable at runtime.
    public static LogEventLevel MinimumLevel
    {
        get => LevelSwitch.MinimumLevel;
        set => LevelSwitch.MinimumLevel = value;
    }

    // Safe to call more than once — later calls are ignored. Must run before anything else so
    // startup itself is in the record.
    public static void Initialize()
    {
        if (_factory is not null)
        {
            return;
        }

        var userArgs = OS.GetCmdlineUserArgs();
        if (TryGetFlagValue(userArgs, "--log-level", out var levelArg)
            && Enum.TryParse<LogEventLevel>(levelArg, ignoreCase: true, out var parsedLevel))
        {
            LevelSwitch.MinimumLevel = parsedLevel;
        }

        var config = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(LevelSwitch)
            .Enrich.FromLogContext()
            .WriteTo.Sink(new GodotSink());

        var resolution = LogPaths.Resolve();
        if (resolution.Success)
        {
            PruneOldSessions(resolution.Directory!);

            var formatter = new MessageTemplateTextFormatter(LogLineTemplate);
            // Serilog.Sinks.File 7.0.0 has no public, non-obsolete way to construct a directly
            // flushable buffered file sink: the only other FileSink constructor is internal, and
            // WriteTo.File() doesn't hand back the sink instance FlushNow()/WarnErrorFlushSink need.
#pragma warning disable CS0618 // FileSink(string, ITextFormatter, long?, Encoding?, bool) is obsolete
            _fileSink = new FileSink(resolution.FilePath!, formatter, fileSizeLimitBytes: null, Encoding.UTF8, buffered: true);
#pragma warning restore CS0618
            config = config.WriteTo.Sink(new WarnErrorFlushSink(_fileSink));

            var flushInterval = ResolveFlushInterval(userArgs);
            _flushTimer = new Timer(_ => _fileSink?.FlushToDisk(), null, flushInterval, flushInterval);
        }

        Log.Logger = config.CreateLogger();
        _factory = new SerilogLoggerFactory(Log.Logger, dispose: false);

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    public static ILogger<T> For<T>() => Factory.CreateLogger<T>();

    // Like For<T>, but yields null instead of throwing when Initialize has not run. For callers on
    // a cleanup or failure path, where a missing logger must not become a second, louder failure
    // than the one being reported.
    public static ILogger<T>? TryFor<T>() => _factory?.CreateLogger<T>();

    public static void RegisterCommands(CommandRegistry registry)
    {
        registry.TryRegister(new CommandDescriptor(
            "loglevel",
            "Show or set the minimum log severity for this session.",
            "loglevel [debug|information|warning|error]",
            HandleLogLevel));
    }

    // Forces the file sink to disk immediately, for callers whose durability requirement is
    // per-write rather than per-interval — an Information-level entry otherwise waits for the
    // periodic flush or a Warning-triggered one (FR-046b; see WarnErrorFlushSink for the general
    // Warning+ policy, FR-005).
    public static void FlushNow() => _fileSink?.FlushToDisk();

    public static void Shutdown()
    {
        if (_shutDown)
        {
            return;
        }

        _shutDown = true;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        _flushTimer?.Dispose();
        _fileSink?.FlushToDisk();
        Log.CloseAndFlush();
    }

    private static ILoggerFactory Factory =>
        _factory ?? throw new InvalidOperationException($"{nameof(Logging)}.{nameof(Initialize)}() has not been called.");

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Factory.CreateLogger("UnhandledException")
            .LogUnhandledException(e.ExceptionObject as Exception, e.IsTerminating);
        Shutdown();
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "Unhandled exception (terminating: {IsTerminating})")]
    private static partial void LogUnhandledException(this Microsoft.Extensions.Logging.ILogger logger, Exception? exception, bool isTerminating);

    private static void PruneOldSessions(string directory)
    {
        var existing = Directory.GetFiles(directory).Select(Path.GetFileName).Cast<string>().ToList();

        // keep - 1: this runs before the new session's own file is created, so keeping the full
        // default here would leave keep + 1 files once that file exists (FR-006).
        var toDelete = LogRetentionPolicy.SelectForDeletion(
            existing, keep: LogRetentionPolicy.DefaultKeep - 1, isProcessAlive: IsProcessAlive);

        foreach (var name in toDelete)
        {
            try
            {
                File.Delete(Path.Combine(directory, name));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"[Logging] Could not prune old session log '{name}': {ex.Message}");
            }
        }
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static TimeSpan ResolveFlushInterval(string[] userArgs)
    {
        if (TryGetFlagValue(userArgs, "--flush-interval", out var value)
            && double.TryParse(value, out var seconds) && seconds > 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return TimeSpan.FromSeconds(DefaultFlushIntervalSeconds);
    }

    private static CommandResult HandleLogLevel(CommandArgs args)
    {
        if (args.Positional.Count == 0)
        {
            return CommandResult.Ok(MinimumLevel.ToString());
        }

        var requested = args.Positional[0];
        if (!ValidLevelNames.Contains(requested, StringComparer.OrdinalIgnoreCase)
            || !Enum.TryParse<LogEventLevel>(requested, ignoreCase: true, out var parsed))
        {
            return CommandResult.Fail($"Unrecognized log level '{requested}'. Valid levels: {string.Join(", ", ValidLevelNames)}.");
        }

        MinimumLevel = parsed;
        return CommandResult.Ok($"Minimum log level set to {parsed}.");
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
