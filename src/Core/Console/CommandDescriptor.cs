namespace NewGame1.Core.Console;

/// <summary>
/// One registered developer command (FR-013). Immutable; construction validates so an invalid
/// descriptor can never exist.
/// </summary>
public sealed record CommandDescriptor
{
    public string Name { get; }

    public string Summary { get; }

    public string Usage { get; }

    public Func<CommandArgs, CommandResult> Handler { get; }

    public CommandDescriptor(string name, string summary, string usage, Func<CommandArgs, CommandResult> handler)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Command name must not be empty.", nameof(name));
        }

        if (name.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("Command name must not contain whitespace.", nameof(name));
        }

        if (string.IsNullOrEmpty(summary))
        {
            throw new ArgumentException("Command summary must not be empty.", nameof(summary));
        }

        Name = name;
        Summary = summary;
        Usage = usage;
        Handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }
}
