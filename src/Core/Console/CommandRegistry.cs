using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace NewGame1.Core.Console;

/// <summary>
/// The set of registered developer commands; what <c>help</c> enumerates and the console resolves
/// against. Registration is append-only within a session — commands are never unregistered.
/// </summary>
public sealed class CommandRegistry(ILogger<CommandRegistry>? logger = null)
{
    private readonly Dictionary<string, CommandDescriptor> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<CommandRegistry> _logger = logger ?? NullLogger<CommandRegistry>.Instance;

    /// <summary>Registered commands ordered by name, for <c>help</c> output.</summary>
    public IReadOnlyList<CommandDescriptor> All =>
        _commands.Values.OrderBy(d => d.Name, StringComparer.Ordinal).ToList();

    /// <summary>Adds a command. Throws <see cref="DuplicateCommandException"/> if the name is taken (FR-014).</summary>
    public void Register(CommandDescriptor descriptor)
    {
        if (!_commands.TryAdd(descriptor.Name, descriptor))
        {
            throw new DuplicateCommandException(descriptor.Name);
        }
    }

    /// <summary>Case-insensitive lookup by name.</summary>
    public bool TryResolve(string name, out CommandDescriptor? descriptor) => _commands.TryGetValue(name, out descriptor);

    /// <summary>
    /// Parses, resolves and invokes <paramref name="line"/>. Never throws (FR-016): a parse
    /// failure, an unknown command name (FR-015), and a handler exception all become a failed
    /// <see cref="CommandResult"/> instead. A handler exception is logged with its detail before
    /// being converted (constitution III).
    /// </summary>
    public CommandResult Execute(string line)
    {
        if (!CommandLineParser.TryParse(line, out var args) || args is null)
        {
            return CommandResult.Fail("Unable to parse command line: check for an unterminated quote.");
        }

        if (!TryResolve(args.CommandName, out var descriptor) || descriptor is null)
        {
            return CommandResult.Fail($"Unknown command '{args.CommandName}'. Type 'help' to list available commands.");
        }

        try
        {
            return descriptor.Handler(args);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Command '{Command}' threw an exception", args.CommandName);
            return CommandResult.Fail(ex.Message);
        }
    }
}
