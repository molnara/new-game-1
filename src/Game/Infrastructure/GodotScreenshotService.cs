using Godot;
using Microsoft.Extensions.Logging;
using NewGame1.Autoloads;
using NewGame1.Core.Screenshots;

namespace NewGame1.Infrastructure;

// Game-side IScreenshotService capturing the main viewport's rendered texture (FR-020, FR-023,
// FR-027; research R1). Writes the PNG to a temporary path first and only moves it into place on
// success, so a failed capture never leaves an empty or partial file behind.
public sealed partial class GodotScreenshotService : IScreenshotService
{
    private readonly string _artifactsDirectory;

    public GodotScreenshotService()
        : this(ProjectSettings.GlobalizePath("res://artifacts"))
    {
    }

    internal GodotScreenshotService(string artifactsDirectory)
    {
        _artifactsDirectory = artifactsDirectory;
    }

    public ScreenshotCaptureResult Capture(ScreenshotName name)
    {
        var root = ((SceneTree)Engine.GetMainLoop()).Root;

        // Issue #4: the console must not appear in the capture. It's a top-layer CanvasLayer
        // that stays visible while the `screenshot` command itself is being typed and submitted,
        // so it has to be hidden here and the hidden state force-drawn before the texture is
        // read back — otherwise the viewport still holds the console-open frame that was last
        // rendered. Degrades to a no-op when the autoload is absent (e.g. a future scene without
        // it, or a unit test constructing this service directly).
        var console = root.GetNodeOrNull<DevConsole>("DevConsole");
        var reopenConsoleAfterCapture = console is not null && console.IsOpen;

        if (reopenConsoleAfterCapture)
        {
            console!.Close();
            RenderingServer.ForceDraw();
        }

        try
        {
            var image = root.GetTexture()?.GetImage();
            if (image is null)
            {
                return ScreenshotCaptureResult.Failure(
                    "No viewport texture is available to capture — this happens under --headless, whose " +
                    "dummy renderer has no rasterizer (research R1).");
            }

            try
            {
                Directory.CreateDirectory(_artifactsDirectory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return ScreenshotCaptureResult.Failure($"Could not create or access '{_artifactsDirectory}': {ex.Message}");
            }

            var finalPath = Path.Combine(_artifactsDirectory, name.Value);
            var replaced = File.Exists(finalPath);
            var tempPath = Path.Combine(_artifactsDirectory, $".{name.Value}.{Guid.NewGuid():N}.tmp");

            var saveError = image.SavePng(tempPath);
            if (saveError != Error.Ok)
            {
                TryDelete(tempPath);
                return ScreenshotCaptureResult.Failure($"Failed to encode screenshot: {saveError}.");
            }

            try
            {
                File.Move(tempPath, finalPath, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                TryDelete(tempPath);
                return ScreenshotCaptureResult.Failure($"Failed to move screenshot into place: {ex.Message}");
            }

            return ScreenshotCaptureResult.Success(finalPath, replaced);
        }
        finally
        {
            if (reopenConsoleAfterCapture)
            {
                console!.Open();
            }
        }
    }

    // Best-effort cleanup of the temp file after a capture that already failed. The caller is being
    // told why the capture failed and that reason must survive, so this cannot throw or return —
    // but a leaked '.tmp' in artifacts/ must not vanish silently either (constitution III).
    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var logger = Logging.TryFor<GodotScreenshotService>();
            if (logger is not null)
            {
                LogTempFileDeleteFailed(logger, ex, path);
            }
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Could not delete the temporary screenshot file {Path} after a failed capture; it is left behind")]
    private static partial void LogTempFileDeleteFailed(ILogger logger, Exception ex, string path);
}
