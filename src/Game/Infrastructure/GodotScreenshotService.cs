using Godot;
using NewGame1.Core.Screenshots;

namespace NewGame1.Infrastructure;

/// <summary>
/// Game-side <see cref="IScreenshotService"/> capturing the main viewport's rendered texture
/// (FR-020, FR-023, FR-027; research R1). Writes the PNG to a temporary path first and only moves
/// it into place on success, so a failed capture never leaves an empty or partial file behind.
/// </summary>
public sealed class GodotScreenshotService : IScreenshotService
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
        var image = ((SceneTree)Engine.GetMainLoop()).Root.GetTexture()?.GetImage();
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
        }
    }
}
