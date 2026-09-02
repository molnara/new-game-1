using System.Text.RegularExpressions;

namespace NewGame1.Core.Diagnostics;

/// <summary>
/// Decides which session log files to delete (FR-006). Pure function over file names — performs
/// no I/O.
/// </summary>
public static partial class LogRetentionPolicy
{
    /// <summary>
    /// Returns the session-log file names to delete: the oldest beyond <paramref name="keep"/>,
    /// ordered by the timestamp embedded in the name. Never returns a name that does not match
    /// this project's own session-log pattern (session-&lt;yyyyMMddTHHmmssfff&gt;.log) — in
    /// particular, never Godot's own godot.log.
    /// </summary>
    public static IReadOnlyList<string> SelectForDeletion(IReadOnlyList<string> existing, int keep = 10)
    {
        var candidates = existing
            .Select(name => (Name: name, Match: SessionLogPattern().Match(name)))
            .Where(x => x.Match.Success)
            .OrderBy(x => x.Match.Groups["timestamp"].Value, StringComparer.Ordinal)
            .Select(x => x.Name)
            .ToList();

        var excess = candidates.Count - keep;
        return excess <= 0 ? [] : candidates.Take(excess).ToList();
    }

    [GeneratedRegex(@"^session-(?<timestamp>\d{8}T\d{9})\.log$")]
    private static partial Regex SessionLogPattern();
}
