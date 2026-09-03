using System.Reflection;
using Chickensoft.GoDotTest;
using Godot;
using Microsoft.Extensions.Logging;
using NewGame1.Autoloads;
using NewGame1.Core.Screenshots;
using NewGame1.Infrastructure;

namespace NewGame1;

public partial class Main : Node
{
    private ILogger<Main>? _logger;

    public override void _Ready()
    {
        Logging.Initialize();
        _logger = Logging.For<Main>();
        LogStartupReady(_logger);

        var registry = GetNode<DevConsole>("/root/DevConsole").Registry;
        Logging.RegisterCommands(registry);
        ScreenshotCommand.Register(registry, new GodotScreenshotService());
        GetNode<PerfMonitor>("/root/PerfMonitor").RegisterCommands(registry);

        var userArgs = OS.GetCmdlineUserArgs();

#if DEBUG
        if (TestEnvironment.From(userArgs).ShouldRunTests)
        {
            CallDeferred(nameof(RunTests));
            return;
        }
#endif

        // A `--screenshot <name>` argument is handled independently by the
        // ScreenshotHarness autoload's own _Ready (research R5) — nothing to do here.
        // Otherwise this is the placeholder scene and there is nothing else to do yet.
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            if (_logger is not null)
            {
                LogShutdownCloseRequested(_logger);
            }

            Logging.Shutdown();
        }
    }

    public override void _ExitTree()
    {
        if (_logger is not null)
        {
            LogShutdownExitingTree(_logger);
        }

        Logging.Shutdown();
    }

#if DEBUG
    private void RunTests()
    {
        _ = GoTest.RunTests(Assembly.GetExecutingAssembly(), this, TestEnvironment.From(OS.GetCmdlineUserArgs()));
    }
#endif

    [LoggerMessage(Level = LogLevel.Information, Message = "Startup: Main ready")]
    private static partial void LogStartupReady(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Shutdown: close requested")]
    private static partial void LogShutdownCloseRequested(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Shutdown: exiting tree")]
    private static partial void LogShutdownExitingTree(ILogger logger);
}
