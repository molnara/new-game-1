namespace NewGame1.Core.Screenshots;

/// <summary>
/// A validated, safe bare file name for a screenshot capture (FR-025). Core never touches the
/// filesystem or joins paths — that happens on the Game side.
/// </summary>
public sealed class ScreenshotName
{
    public const string DefaultName = "main.png";

    private static readonly char[] InvalidFileNameChars = BuildInvalidFileNameChars();

    public string Value { get; }

    private ScreenshotName(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Validates <paramref name="raw"/> into a safe screenshot file name. Never throws — an invalid
    /// name is reported through <paramref name="error"/> rather than an exception (FR-025).
    /// </summary>
    public static bool TryCreate(string? raw, out ScreenshotName? name, out string? error)
    {
        if (string.IsNullOrEmpty(raw))
        {
            name = new ScreenshotName(DefaultName);
            error = null;
            return true;
        }

        if (raw.Contains('/') || raw.Contains('\\') || raw.Contains(".."))
        {
            name = null;
            error = $"Screenshot name '{raw}' must not contain a path separator or '..' — it must stay inside artifacts/.";
            return false;
        }

        if (raw.IndexOfAny(InvalidFileNameChars) >= 0)
        {
            name = null;
            error = $"Screenshot name '{raw}' contains a character that is not allowed in a file name.";
            return false;
        }

        name = new ScreenshotName(raw.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? raw : raw + ".png");
        error = null;
        return true;
    }

    // Portable illegal-file-name set: punctuation Windows forbids (so a name that is safe here is
    // also safe if the artifact is ever viewed or copied on that platform) plus all ASCII control
    // characters. Path.GetInvalidFileNameChars() is not used because on Linux it only reports '\0'
    // and '/', which would let ':' and friends through.
    private static char[] BuildInvalidFileNameChars()
    {
        var punctuation = new[] { '"', '<', '>', '|', ':', '*', '?' };
        var controls = Enumerable.Range(0, 32).Select(c => (char)c);
        return punctuation.Concat(controls).ToArray();
    }
}
