namespace NewGame1.Core.Screenshots;

/// <summary>
/// Core-declared engine service for capturing the current view (constitution I; research R8).
/// Implemented on the Game side so <see cref="ScreenshotCommand"/> can live in the registry without
/// dragging the engine into Core.
/// </summary>
public interface IScreenshotService
{
    ScreenshotCaptureResult Capture(ScreenshotName name);
}

/// <summary>Outcome of one capture attempt.</summary>
public sealed record ScreenshotCaptureResult
{
    public bool Succeeded { get; }

    public string? Path { get; }

    public bool Replaced { get; }

    public string? FailureReason { get; }

    private ScreenshotCaptureResult(bool succeeded, string? path, bool replaced, string? failureReason)
    {
        Succeeded = succeeded;
        Path = path;
        Replaced = replaced;
        FailureReason = failureReason;
    }

    public static ScreenshotCaptureResult Success(string path, bool replaced) => new(true, path, replaced, null);

    public static ScreenshotCaptureResult Failure(string reason) => new(false, null, false, reason);
}
