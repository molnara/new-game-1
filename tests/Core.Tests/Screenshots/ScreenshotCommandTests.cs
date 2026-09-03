using NewGame1.Core.Console;
using NewGame1.Core.Screenshots;
using Shouldly;

namespace NewGame1.Core.Tests.Screenshots;

public class ScreenshotCommandTests
{
    private sealed class FakeScreenshotService : IScreenshotService
    {
        public ScreenshotCaptureResult ResultToReturn { get; set; } =
            ScreenshotCaptureResult.Success("artifacts/main.png", replaced: false);

        public bool CaptureWasCalled { get; private set; }

        public ScreenshotCaptureResult Capture(ScreenshotName name)
        {
            CaptureWasCalled = true;
            return ResultToReturn;
        }
    }

    private static (CommandRegistry Registry, FakeScreenshotService Service) RegistryWithScreenshot()
    {
        var service = new FakeScreenshotService();
        var registry = new CommandRegistry();
        ScreenshotCommand.Register(registry, service);
        return (registry, service);
    }

    [Fact]
    public void SuccessReportsTheFullPathWritten()
    {
        var (registry, service) = RegistryWithScreenshot();
        service.ResultToReturn = ScreenshotCaptureResult.Success("artifacts/main.png", replaced: false);

        var result = registry.Execute("screenshot");

        result.Succeeded.ShouldBeTrue();
        result.Message.ShouldContain("artifacts/main.png");
    }

    [Fact]
    public void ReplacingAnExistingFileSaysSoInTheMessage()
    {
        var (registry, service) = RegistryWithScreenshot();
        service.ResultToReturn = ScreenshotCaptureResult.Success("artifacts/shot.png", replaced: true);

        var result = registry.Execute("screenshot shot");

        result.Succeeded.ShouldBeTrue();
        result.Message.ShouldContain("artifacts/shot.png");
        result.Message.ShouldContain("replaced", Case.Insensitive);
    }

    [Fact]
    public void InvalidNameIsRejectedBeforeAnyCaptureIsAttempted()
    {
        var (registry, service) = RegistryWithScreenshot();

        var result = registry.Execute("screenshot ../escape");

        result.Succeeded.ShouldBeFalse();
        result.FailureReason.ShouldNotBeNullOrWhiteSpace();
        service.CaptureWasCalled.ShouldBeFalse();
    }

    [Fact]
    public void ServiceFailureProducesAFailureResultCarryingTheReason()
    {
        var (registry, service) = RegistryWithScreenshot();
        service.ResultToReturn = ScreenshotCaptureResult.Failure("no viewport texture available");

        var result = registry.Execute("screenshot");

        result.Succeeded.ShouldBeFalse();
        result.FailureReason.ShouldNotBeNull().ShouldContain("no viewport texture available");
    }
}
