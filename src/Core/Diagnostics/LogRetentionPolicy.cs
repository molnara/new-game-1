using System.Text.RegularExpressions;

namespace NewGame1.Core.Diagnostics;

/// <summary>
/// Decides which session log files to delete (FR-006). Pure function over file names — performs
/// no I/O.
/// </summary>
public static partial class LogRetentionPolicy
{
    /// <summary>Default session logs to keep, per FR-006 ("ten most recent"); configurable per call.</summary>
    public const int DefaultKeep = 10;

    /// <summary>
    /// Returns the session-log file names to delete: the oldest beyond <paramref name="keep"/>,
    /// ordered by the timestamp embedded in the name. Never returns a name that does not match
    /// this project's own session-log pattern (session-&lt;yyyyMMddTHHmmssfff&gt;[-processId].log)
    /// — in particular, never Godot's own godot.log. A file whose embedded process id is still
    /// alive per <paramref name="isProcessAlive"/> is never returned either, even if it is among
    /// the oldest — another running session may still be writing to it.
    /// </summary>
    public static IReadOnlyList<string> SelectForDeletion(
        IReadOnlyList<string> existing, int keep = DefaultKeep, Func<int, bool>? isProcessAlive = null)
    {
        var candidates = existing
            .Select(name => (Name: name, Match: SessionLogPattern().Match(name)))
            .Where(x => x.Match.Success)
            .OrderBy(x => x.Match.Groups["timestamp"].Value, StringComparer.Ordinal)
            .ToList();

        var excess = candidates.Count - keep;
        if (excess <= 0)
        {
            return [];
        }

        return candidates
            .Take(excess)
            .Where(x => !IsHeldOpen(x.Match, isProcessAlive))
            .Select(x => x.Name)
            .ToList();
    }

    private static bool IsHeldOpen(Match match, Func<int, bool>? isProcessAlive)
    {
        if (isProcessAlive is null || !match.Groups["pid"].Success)
        {
            return false;
        }

        return int.TryParse(match.Groups["pid"].Value, out var pid) && isProcessAlive(pid);
    }

    [GeneratedRegex(@"^session-(?<timestamp>\d{8}T\d{9})(-(?<pid>\d+))?\.log$")]
    private static partial Regex SessionLogPattern();
}
