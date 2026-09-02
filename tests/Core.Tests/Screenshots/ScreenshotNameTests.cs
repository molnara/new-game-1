using NewGame1.Core.Screenshots;
using Shouldly;

namespace NewGame1.Core.Tests.Screenshots;

public class ScreenshotNameTests
{
    [Fact]
    public void OmittedNameFallsBackToDefaultMain()
    {
        var accepted = ScreenshotName.TryCreate(null, out var name, out var error);

        accepted.ShouldBeTrue();
        error.ShouldBeNull();
        name.ShouldNotBeNull();
        name!.Value.ShouldBe("main.png");
    }

    [Fact]
    public void EmptyNameFallsBackToDefaultMain()
    {
        var accepted = ScreenshotName.TryCreate(string.Empty, out var name, out var error);

        accepted.ShouldBeTrue();
        error.ShouldBeNull();
        name.ShouldNotBeNull();
        name!.Value.ShouldBe("main.png");
    }

    [Theory]
    [InlineData("sub/dir")]
    [InlineData("sub\\dir")]
    [InlineData("../escape")]
    public void NameThatWouldWriteOutsideArtifactsIsRejected(string rawName)
    {
        var accepted = ScreenshotName.TryCreate(rawName, out var name, out var error);

        accepted.ShouldBeFalse();
        name.ShouldBeNull();
        error.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void NameWithCharacterIllegalInAFileNameIsRejectedNamingTheOffendingInput()
    {
        const string rawName = "bad:name";

        var accepted = ScreenshotName.TryCreate(rawName, out var name, out var error);

        accepted.ShouldBeFalse();
        name.ShouldBeNull();
        error.ShouldNotBeNullOrWhiteSpace();
        error.ShouldContain(rawName);
    }

    [Fact]
    public void NameAlreadyEndingInPngIsAcceptedWithoutDoublingTheExtension()
    {
        var accepted = ScreenshotName.TryCreate("shot.png", out var name, out var error);

        accepted.ShouldBeTrue();
        error.ShouldBeNull();
        name.ShouldNotBeNull();
        name!.Value.ShouldBe("shot.png");
    }

    [Fact]
    public void NameWithoutExtensionGetsPngAppended()
    {
        var accepted = ScreenshotName.TryCreate("shot", out var name, out var error);

        accepted.ShouldBeTrue();
        error.ShouldBeNull();
        name.ShouldNotBeNull();
        name!.Value.ShouldBe("shot.png");
    }
}
