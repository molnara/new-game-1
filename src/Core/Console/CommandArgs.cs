namespace NewGame1.Core.Console;

/// <summary>
/// Parsed tokens for one console invocation, produced by <see cref="CommandLineParser"/>. Quotes
/// are already resolved by the time this exists.
/// </summary>
public sealed record CommandArgs(string CommandName, IReadOnlyList<string> Positional);
