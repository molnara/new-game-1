using NewGame1.Core.Console;
using Shouldly;

namespace NewGame1.Core.Tests.Console;

public class CommandRegistryTests
{
    private static readonly string[] ExpectedNamesInOrder = ["alpha", "mid", "zeta"];

    private static CommandDescriptor Descriptor(string name, Func<CommandArgs, CommandResult>? handler = null) =>
        new(name, $"{name} summary.", name, handler ?? (_ => CommandResult.Ok($"{name} ran")));

    [Fact]
    public void RegistersAndResolvesCaseInsensitively()
    {
        var registry = new CommandRegistry();
        registry.Register(Descriptor("help"));

        registry.TryResolve("HELP", out var resolved).ShouldBeTrue();
        resolved!.Name.ShouldBe("help");
    }

    [Fact]
    public void DuplicateRegistrationIsRejectedAndFirstIsRetained()
    {
        var registry = new CommandRegistry();
        registry.Register(Descriptor("help", _ => CommandResult.Ok("first")));

        Should.Throw<DuplicateCommandException>(() => registry.Register(Descriptor("help", _ => CommandResult.Ok("second"))));

        registry.TryResolve("help", out var resolved).ShouldBeTrue();
        resolved!.Handler(new CommandArgs("help", Array.Empty<string>())).Message.ShouldBe("first");
    }

    [Fact]
    public void TryRegisterReportsCollisionWithoutThrowingAndRetainsTheFirst()
    {
        var registry = new CommandRegistry();
        registry.TryRegister(Descriptor("help", _ => CommandResult.Ok("first"))).ShouldBeTrue();

        registry.TryRegister(Descriptor("help", _ => CommandResult.Ok("second"))).ShouldBeFalse();

        registry.TryResolve("help", out var resolved).ShouldBeTrue();
        resolved!.Handler(new CommandArgs("help", Array.Empty<string>())).Message.ShouldBe("first");
    }

    [Fact]
    public void UnrecognizedNameYieldsFailureNamingInputAndPointingAtHelp()
    {
        var registry = new CommandRegistry();

        var result = registry.Execute("nosuchcommand");

        result.Succeeded.ShouldBeFalse();
        result.FailureReason.ShouldNotBeNull().ShouldContain("nosuchcommand");
        result.FailureReason.ShouldNotBeNull().ShouldContain("help");
    }

    [Fact]
    public void HandlerThatThrowsIsCaughtAndConvertedToFailureCarryingDetail()
    {
        var registry = new CommandRegistry();
        registry.Register(Descriptor("boom", _ => throw new InvalidOperationException("kaboom")));

        var result = registry.Execute("boom");

        result.Succeeded.ShouldBeFalse();
        result.FailureReason.ShouldNotBeNull().ShouldContain("kaboom");
    }

    [Fact]
    public void AllIsOrderedByName()
    {
        var registry = new CommandRegistry();
        registry.Register(Descriptor("zeta"));
        registry.Register(Descriptor("alpha"));
        registry.Register(Descriptor("mid"));

        registry.All.Select(d => d.Name).ShouldBe(ExpectedNamesInOrder);
    }

    [Fact]
    public void UnterminatedQuoteFailsExecutionWithoutRunningAnything()
    {
        var registry = new CommandRegistry();
        var ran = false;
        registry.Register(Descriptor("echo", _ =>
        {
            ran = true;
            return CommandResult.Ok(string.Empty);
        }));

        var result = registry.Execute("echo \"unterminated");

        result.Succeeded.ShouldBeFalse();
        ran.ShouldBeFalse();
    }
}
