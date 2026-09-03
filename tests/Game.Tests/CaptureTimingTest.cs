using Chickensoft.GoDotTest;
using Godot;
using NewGame1.Autoloads;
using NewGame1.Core.Screenshots;
using Shouldly;

namespace NewGame1.Tests;

public class CaptureTimingTest : TestClass
{
    public CaptureTimingTest(Node testScene) : base(testScene) { }

    [Test]
    public async Task WaitsTheConfiguredFrameCountBeforeCapturing()
    {
        const int frameDelay = 3;
        var service = new RecordingScreenshotService();
        var harness = new ScreenshotHarness(service, frameDelay);
        TestScene.AddChild(harness);

        try
        {
            var runTask = harness.RunAsync(ScreenshotName.DefaultName, frameDelay);

            for (var i = 0; i < frameDelay; i++)
            {
                service.CaptureCallCount.ShouldBe(
                    0, $"capture must not happen before {frameDelay} frames have elapsed (frame {i})");
                await WaitFrame();
            }

            // Reaching this point proves the wait was frame-counted rather than skipped: the fake
            // service throws the moment it is invoked, which also stops the harness short of its
            // GetTree().Quit() call — calling that for real would tear down the process running this
            // very test suite.
            await runTask.ShouldThrowAsync<CaptureObserved>();
            service.CaptureCallCount.ShouldBe(1);
        }
        finally
        {
            harness.QueueFree();
        }
    }

    [Test]
    public async Task CapturesWithoutWaitingAFrameWhenTheDelayIsZero()
    {
        var service = new RecordingScreenshotService();
        var harness = new ScreenshotHarness(service, 0);
        TestScene.AddChild(harness);

        try
        {
            await harness.RunAsync(ScreenshotName.DefaultName, 0).ShouldThrowAsync<CaptureObserved>();
            service.CaptureCallCount.ShouldBe(1);
        }
        finally
        {
            harness.QueueFree();
        }
    }

    private async Task WaitFrame() => await TestScene.ToSignal(TestScene.GetTree(), SceneTree.SignalName.ProcessFrame);

    // Records that a capture happened, then throws instead of returning a result — the frame-count
    // loop under test has no other externally observable seam, and letting the real success path run
    // would reach ScreenshotHarness.RunAsync's GetTree().Quit(), which would quit the test process.
    private sealed class RecordingScreenshotService : IScreenshotService
    {
        public int CaptureCallCount { get; private set; }

        public ScreenshotCaptureResult Capture(ScreenshotName name)
        {
            CaptureCallCount++;
            throw new CaptureObserved();
        }
    }

    private sealed class CaptureObserved : Exception { }
}
