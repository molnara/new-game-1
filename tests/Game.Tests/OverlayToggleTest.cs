using Chickensoft.GoDotTest;
using Godot;
using NewGame1.Autoloads;
using NewGame1.Core.Console;
using Shouldly;

namespace NewGame1.Tests;

public class OverlayToggleTest : TestClass
{
    private readonly PerfMonitor _perfMonitor;

    public OverlayToggleTest(Node testScene) : base(testScene)
    {
        _perfMonitor = testScene.GetNode<PerfMonitor>("/root/PerfMonitor");
    }

    [Test]
    public async Task OverlayTogglesOnAndOffAcrossTheNodeLifecycle()
    {
        _perfMonitor.SetOverlayVisible(false);
        _perfMonitor.IsOverlayVisible.ShouldBeFalse();

        _perfMonitor.SetOverlayVisible(true);
        await WaitFrame();
        _perfMonitor.IsOverlayVisible.ShouldBeTrue();

        _perfMonitor.SetOverlayVisible(false);
        await WaitFrame();
        _perfMonitor.IsOverlayVisible.ShouldBeFalse();

        _perfMonitor.SetOverlayVisible(true);
        await WaitFrame();
        _perfMonitor.IsOverlayVisible.ShouldBeTrue();

        _perfMonitor.SetOverlayVisible(false);
    }

    [Test]
    public async Task SamplingContinuesRegardlessOfOverlayVisibility()
    {
        var registry = new CommandRegistry();
        _perfMonitor.RegisterCommands(registry);

        try
        {
            _perfMonitor.SetOverlayVisible(false);
            var hiddenBefore = SampleCount(registry);
            await WaitFrames(3);
            var hiddenAfter = SampleCount(registry);
            hiddenAfter.ShouldBeGreaterThan(hiddenBefore, "sampling must continue while the overlay is hidden (FR-045)");

            _perfMonitor.SetOverlayVisible(true);
            var visibleBefore = SampleCount(registry);
            await WaitFrames(3);
            var visibleAfter = SampleCount(registry);
            visibleAfter.ShouldBeGreaterThan(visibleBefore, "sampling must continue while the overlay is visible");
        }
        finally
        {
            _perfMonitor.SetOverlayVisible(false);
        }
    }

    // The `perfstats` command is the only public seam onto the histogram (FR-043) — PerfMonitor
    // deliberately does not expose sample count as a property.
    private static long SampleCount(CommandRegistry registry)
    {
        var result = registry.Execute("perfstats");
        result.Succeeded.ShouldBeTrue();

        if (result.Message.Contains("No samples yet"))
        {
            return 0;
        }

        var match = System.Text.RegularExpressions.Regex.Match(result.Message, @"samples=(\d+)");
        match.Success.ShouldBeTrue($"expected a samples=<n> figure in '{result.Message}'");
        return long.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task WaitFrame() => await TestScene.ToSignal(TestScene.GetTree(), SceneTree.SignalName.ProcessFrame);

    private async Task WaitFrames(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await WaitFrame();
        }
    }
}
