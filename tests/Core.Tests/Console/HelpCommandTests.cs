using NewGame1.Core.Console;
using Shouldly;

namespace NewGame1.Core.Tests.Console;

public class HelpCommandTests
{
    private static CommandRegistry RegistryWithHelp()
    {
        var registry = new CommandRegistry();
        HelpCommand.Register(registry);
        return registry;
    }

    [Fact]
    public void BareHelpListsEveryRegisteredCommandOrderedByName()
    {
        var registry = RegistryWithHelp();
        registry.Register(new CommandDescriptor("zeta", "Does zeta things.", "zeta", _ => CommandResult.Ok(string.Empty)));
        registry.Register(new CommandDescriptor("alpha", "Does alpha things.", "alpha", _ => CommandResult.Ok(string.Empty)));

        var result = registry.Execute("help");

        result.Succeeded.ShouldBeTrue();
        var lines = result.Message.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.ShouldContain("alpha — Does alpha things.");
        lines.ShouldContain("zeta — Does zeta things.");
        Array.IndexOf(lines, "alpha — Does alpha things.").ShouldBeLessThan(Array.IndexOf(lines, "zeta — Does zeta things."));
    }

    [Fact]
    public void HelpReflectsWhateverIsRegisteredAtTheMomentItRuns()
    {
        var registry = RegistryWithHelp();

        registry.Execute("help").Message.ShouldNotContain("lateCommand");

        registry.Register(new CommandDescriptor("lateCommand", "Registered late.", "lateCommand", _ => CommandResult.Ok(string.Empty)));

        registry.Execute("help").Message.ShouldContain("lateCommand");
    }

    [Fact]
    public void HelpWithCommandNameShowsSummaryAndUsage()
    {
        var registry = RegistryWithHelp();
        registry.Register(new CommandDescriptor("screenshot", "Capture the view.", "screenshot [name]", _ => CommandResult.Ok(string.Empty)));

        var result = registry.Execute("help screenshot");

        result.Succeeded.ShouldBeTrue();
        result.Message.ShouldContain("Capture the view.");
        result.Message.ShouldContain("screenshot [name]");
    }

    [Fact]
    public void HelpWithUnknownCommandFailsNamingItAndPointingBackAtBareHelp()
    {
        var registry = RegistryWithHelp();

        var result = registry.Execute("help nosuchcommand");

        result.Succeeded.ShouldBeFalse();
        result.FailureReason.ShouldNotBeNull().ShouldContain("nosuchcommand");
        result.FailureReason.ShouldNotBeNull().ShouldContain("help");
    }
}
