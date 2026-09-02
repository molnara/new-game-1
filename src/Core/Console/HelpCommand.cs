using System.Text;

namespace NewGame1.Core.Console;

/// <summary>Registers <c>help</c> and <c>help &lt;command&gt;</c> against a <see cref="CommandRegistry"/> (FR-012).</summary>
public static class HelpCommand
{
    public static void Register(CommandRegistry registry)
    {
        registry.Register(new CommandDescriptor(
            "help",
            "List available commands, or explain one.",
            "help [command]",
            args => Execute(registry, args)));
    }

    private static CommandResult Execute(CommandRegistry registry, CommandArgs args)
    {
        if (args.Positional.Count == 0)
        {
            return CommandResult.Ok(ListAll(registry));
        }

        var name = args.Positional[0];
        if (!registry.TryResolve(name, out var descriptor) || descriptor is null)
        {
            return CommandResult.Fail($"Unknown command '{name}'. Type 'help' to list available commands.");
        }

        return CommandResult.Ok($"{descriptor.Usage}\n{descriptor.Summary}");
    }

    private static string ListAll(CommandRegistry registry)
    {
        var builder = new StringBuilder();
        foreach (var descriptor in registry.All)
        {
            builder.Append(descriptor.Name).Append(" — ").Append(descriptor.Summary).Append('\n');
        }

        return builder.ToString();
    }
}
