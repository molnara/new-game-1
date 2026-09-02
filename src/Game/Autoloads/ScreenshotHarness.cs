using Godot;
using Microsoft.Extensions.Logging;
using NewGame1.Core.Screenshots;
using NewGame1.Infrastructure;

namespace NewGame1.Autoloads;

// Cmdline-activated capture (FR-020..FR-027; research R5). Inert unless --screenshot <name>
// arrives via OS.GetCmdlineUserArgs — a normal play session pays no cost. When active, waits a
// fixed, configurable number of fully rendered frames (never a wall-clock delay, FR-026) before
// capturing through IScreenshotService and quitting with a status.
public partial class ScreenshotHarness : Node
{
    public const int DefaultFrameDelay = 10;

    private readonly IScreenshotService _service;
    private readonly int _defaultFrameDelay;

    private ILogger<ScreenshotHarness>? _logger;

    public ScreenshotHarness()
        : this(new GodotScreenshotService(), DefaultFrameDelay)
    {
    }

    internal ScreenshotHarness(IScreenshotService service, int defaultFrameDelay)
    {
        _service = service;
        _defaultFrameDelay = defaultFrameDelay;
    }

    public override void _Ready()
    {
        var userArgs = OS.GetCmdlineUserArgs();
        if (!TryGetFlagValue(userArgs, "--screenshot", out var name))
        {
            return;
        }

        Logging.Initialize();
        _logger = Logging.For<ScreenshotHarness>();

        var frameDelay = _defaultFrameDelay;
        if (TryGetFlagValue(userArgs, "--screenshot-frames", out var frameArg)
            && int.TryParse(frameArg, out var parsedDelay) && parsedDelay >= 0)
        {
            frameDelay = parsedDelay;
        }

        _ = RunAsync(name, frameDelay);
    }

    // Waits frameDelay rendered frames, then captures and quits.
    internal async System.Threading.Tasks.Task RunAsync(string name, int frameDelay)
    {
        for (var i = 0; i < frameDelay; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        if (!ScreenshotName.TryCreate(name, out var screenshotName, out var nameError) || screenshotName is null)
        {
            Fail($"Invalid screenshot name '{name}': {nameError}");
            return;
        }

        var result = _service.Capture(screenshotName);
        if (!result.Succeeded)
        {
            Fail(result.FailureReason ?? "Screenshot capture failed.");
            return;
        }

        if (result.Path is null)
        {
            // A success carrying no path cannot be reported to the caller, and reporting nothing
            // would be exactly the silent failure SC-009 forbids.
            Fail("Screenshot capture reported success without a path.");
            return;
        }

        if (result.Replaced)
        {
            _logger?.LogInformation("Screenshot harness replaced existing screenshot {Path}", result.Path);
        }
        else
        {
            _logger?.LogInformation("Screenshot harness wrote {Path}", result.Path);
        }

        ProcessOutput.WriteLine(result.Path);
        GetTree().Quit(0);
    }

    private void Fail(string reason)
    {
        _logger?.LogError("Screenshot harness capture failed: {Reason}", reason);
        ProcessOutput.WriteErrorLine($"screenshot failed: {reason}");
        GetTree().Quit(1);
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
