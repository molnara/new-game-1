namespace NewGame1.Core.Screenshots;

/// <summary>
/// Core-declared engine service for capturing the current view (constitution I; research R8).
/// Implemented on the Game side so <see cref="ScreenshotCommand"/> can live in the registry without
/// dragging the engine into Core.
/// </summary>
public interface IScreenshotService
{
    /// <summary>Captures the currently rendered view under the given validated <paramref name="name"/>.</summary>
    ScreenshotCaptureResult Capture(ScreenshotName name);
}

/// <summary>Outcome of one capture attempt.</summary>
public sealed record ScreenshotCaptureResult
{
    /// <summary>Whether the capture succeeded.</summary>
    public bool Succeeded { get; }

    /// <summary>The written file's path, when <see cref="Succeeded"/> is true; otherwise null.</summary>
    public string? Path { get; }

    /// <summary>Whether an existing file at <see cref="Path"/> was overwritten.</summary>
    public bool Replaced { get; }

    /// <summary>Why the capture failed, when <see cref="Succeeded"/> is false; otherwise null.</summary>
    public string? FailureReason { get; }

    private ScreenshotCaptureResult(bool succeeded, string? path, bool replaced, string? failureReason)
    {
        Succeeded = succeeded;
        Path = path;
        Replaced = replaced;
        FailureReason = failureReason;
    }

    /// <summary>Builds a successful result, noting whether an existing file at <paramref name="path"/> was replaced.</summary>
    public static ScreenshotCaptureResult Success(string path, bool replaced) => new(true, path, replaced, null);

    /// <summary>Builds a failed result carrying the reason.</summary>
    public static ScreenshotCaptureResult Failure(string reason) => new(false, null, false, reason);
}
