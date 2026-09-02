using Chickensoft.GoDotTest;
using Godot;
using NewGame1.Autoloads;
using NewGame1.Core.Screenshots;
using NewGame1.Infrastructure;
using Shouldly;

namespace NewGame1.Tests;

// Issue #4: the `screenshot` command must close the dev console before capturing (so golden-image
// comparisons see the game, not the console overlay) and restore the console's prior open state
// afterward.
public class ScreenshotConsoleTest : TestClass
{
    private readonly DevConsole _console;

    public ScreenshotConsoleTest(Node testScene) : base(testScene)
    {
        _console = testScene.GetNode<DevConsole>("/root/DevConsole");
    }

    [Test]
    public void CaptureHidesTheOpenConsoleAndRestoresItAfterward()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"screenshot-console-test-{Guid.NewGuid():N}");
        var service = new GodotScreenshotService(tempDirectory);

        _console.Open();
        _console.IsOpen.ShouldBeTrue("the test must start from an open console to exercise the hide/restore path");

        try
        {
            var result = service.Capture(DefaultScreenshotName());

            result.Succeeded.ShouldBeTrue(result.FailureReason);
            _console.IsOpen.ShouldBeTrue("the screenshot command must not have the side effect of leaving the console closed");

            using var image = Image.LoadFromFile(result.Path!);

            // The console panel is a full-width ColorRect(0,0,0,0.85) covering y in [0, 320); the
            // scene's own background is Color(0.129412, 0.145098, 0.192157) (scenes/Main.tscn). A
            // pixel sampled inside that region is far darker with the console composited over the
            // background than the background alone, so this distinguishes "console was visible at
            // capture time" from "console was hidden" without a golden-image compare.
            var pixel = image.GetPixel(10, 10);
            var brightness = (pixel.R + pixel.G + pixel.B) / 3.0;
            brightness.ShouldBeGreaterThan(
                0.05, "the captured pixel at (10,10) is inside the console panel's region and is too dark — " +
                "the console appears to still have been visible when the frame was captured (issue #4)");
        }
        finally
        {
            _console.Close();
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Test]
    public void CaptureLeavesAnAlreadyClosedConsoleClosed()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"screenshot-console-test-{Guid.NewGuid():N}");
        var service = new GodotScreenshotService(tempDirectory);

        _console.Close();
        _console.IsOpen.ShouldBeFalse();

        try
        {
            var result = service.Capture(DefaultScreenshotName());

            result.Succeeded.ShouldBeTrue(result.FailureReason);
            _console.IsOpen.ShouldBeFalse("capturing must not open a console that was already closed");
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private static ScreenshotName DefaultScreenshotName()
    {
        ScreenshotName.TryCreate(null, out var name, out _).ShouldBeTrue();
        return name!;
    }
}
