using Godot;

namespace NewGame1.Infrastructure;

// Resolves the session log directory under user://logs/ and mints a per-session file name
// (FR-001, FR-001b; research R6).
public static class LogPaths
{
    public readonly record struct Resolution(bool Success, string? FilePath, string? Directory);

    // Creates the logs directory if needed and mints a file name for this session, sortable by
    // start time and unique per process so two concurrent runs never share a file. If the
    // directory cannot be created or written, reports the problem on stdout and returns a
    // failed result rather than throwing, so the game still starts (FR-001a).
    public static Resolution Resolve() => Resolve(OS.GetUserDataDir(), DateTime.UtcNow, System.Environment.ProcessId);

    internal static Resolution Resolve(string userDataDir, DateTime startTimeUtc, int processId)
    {
        var directory = Path.Combine(userDataDir, "logs");

        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"[LogPaths] Could not create or access the logs directory '{directory}': {ex.Message}");
            return new Resolution(false, null, null);
        }

        var fileName = $"session-{startTimeUtc:yyyyMMddTHHmmssfff}-{processId}.log";
        return new Resolution(true, Path.Combine(directory, fileName), directory);
    }
}
