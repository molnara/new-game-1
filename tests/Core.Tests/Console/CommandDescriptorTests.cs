using NewGame1.Core.Console;
using Shouldly;

namespace NewGame1.Core.Tests.Console;

public class CommandDescriptorTests
{
    private static CommandResult Handler(CommandArgs args) => CommandResult.Ok(string.Empty);

    [Fact]
    public void ConstructsWithValidFields()
    {
        var descriptor = new CommandDescriptor("help", "List commands.", "help [command]", Handler);

        descriptor.Name.ShouldBe("help");
        descriptor.Summary.ShouldBe("List commands.");
        descriptor.Usage.ShouldBe("help [command]");
    }

    [Fact]
    public void ThrowsOnEmptyName()
    {
        Should.Throw<ArgumentException>(() => new CommandDescriptor("", "Summary.", "usage", Handler));
    }

    [Fact]
    public void ThrowsOnEmptySummary()
    {
        Should.Throw<ArgumentException>(() => new CommandDescriptor("help", "", "usage", Handler));
    }

    [Fact]
    public void ThrowsOnNameContainingWhitespace()
    {
        Should.Throw<ArgumentException>(() => new CommandDescriptor("help me", "Summary.", "usage", Handler));
    }
}
