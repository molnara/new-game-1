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

/// <summary>
/// The static <see cref="For{T}"/> entry point (FR-004) over a Serilog pipeline: a file sink
/// (buffered, flushed at least once per second and immediately on Warning/Error via
/// <see cref="WarnErrorFlushSink"/>, FR-005), a <see cref="GodotSink"/> alongside it (FR-007), and
/// a configurable minimum level defaulting to Information (FR-003). Also establishes the
/// <c>--log-level &lt;level&gt;</c> launch-flag convention (research R5, R14) reused by console
/// history (FR-019) and the statistics interval (FR-046).
/// </summary>
public static class Logging
{
    private const string LogLineTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    private static readonly string[] ValidLevelNames = ["debug", "information", "warning", "error"];

    private static readonly LoggingLevelSwitch LevelSwitch = new(LogEventLevel.Information);

    private static ILoggerFactory? _factory;
    private static FileSink? _fileSink;
    private static Timer? _flushTimer;
    private static bool _shutDown;

    /// <summary>The minimum severity currently in effect (FR-003); adjustable at runtime.</summary>
    public static LogEventLevel MinimumLevel
    {
        get => LevelSwitch.MinimumLevel;
        set => LevelSwitch.MinimumLevel = value;
    }

    /// <summary>
    /// Builds the Serilog pipeline and prunes old sessions. Safe to call more than once — later
    /// calls are ignored. Must run before anything else so startup itself is in the record.
    /// </summary>
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
            _fileSink = new FileSink(resolution.FilePath!, formatter, fileSizeLimitBytes: null, Encoding.UTF8, buffered: true);
            config = config.WriteTo.Sink(new WarnErrorFlushSink(_fileSink));

            _flushTimer = new Timer(_ => _fileSink?.FlushToDisk(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        Log.Logger = config.CreateLogger();
        _factory = new SerilogLoggerFactory(Log.Logger, dispose: false);

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    /// <summary>Obtains a logger labelled with <typeparamref name="T"/>'s own name (FR-004).</summary>
    public static ILogger<T> For<T>() => Factory.CreateLogger<T>();

    /// <summary>
    /// Registers the <c>loglevel</c> command (FR-003) — every system here exposes at least one
    /// console command (constitution III), and logging otherwise has none.
    /// </summary>
    public static void RegisterCommands(CommandRegistry registry)
    {
        registry.Register(new CommandDescriptor(
            "loglevel",
            "Show or set the minimum log severity for this session.",
            "loglevel [debug|information|warning|error]",
            HandleLogLevel));
    }

    /// <summary>Flushes and closes the pipeline so the final batch reaches disk (FR-005).</summary>
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
            .LogCritical(e.ExceptionObject as Exception, "Unhandled exception (terminating: {IsTerminating})", e.IsTerminating);
        Shutdown();
    }

    private static void PruneOldSessions(string directory)
    {
        var existing = Directory.GetFiles(directory).Select(Path.GetFileName).Cast<string>().ToList();
        foreach (var name in LogRetentionPolicy.SelectForDeletion(existing))
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
