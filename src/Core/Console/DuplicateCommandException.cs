namespace NewGame1.Core.Console;

/// <summary>Thrown by <see cref="CommandRegistry.Register"/> when a name is already taken (FR-014).</summary>
public sealed class DuplicateCommandException(string name)
    : Exception($"A command named '{name}' is already registered.")
{
    public string Name { get; } = name;
}
