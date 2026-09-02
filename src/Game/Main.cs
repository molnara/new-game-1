using System.Reflection;
using Chickensoft.GoDotTest;
using Godot;

namespace NewGame1;

public partial class Main : Node
{
    public override void _Ready()
    {
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

#if DEBUG
    private void RunTests()
    {
        _ = GoTest.RunTests(Assembly.GetExecutingAssembly(), this, TestEnvironment.From(OS.GetCmdlineUserArgs()));
    }
#endif
}
