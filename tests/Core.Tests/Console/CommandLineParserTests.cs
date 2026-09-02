using NewGame1.Core.Console;
using Shouldly;

namespace NewGame1.Core.Tests.Console;

public class CommandLineParserTests
{
    [Fact]
    public void SplitsWhitespaceSeparatedTokens()
    {
        CommandLineParser.TryParse("help topics", out var args).ShouldBeTrue();
        args!.CommandName.ShouldBe("help");
        args.Positional.ShouldBe(new[] { "topics" });
    }

    [Fact]
    public void CollapsesRunsOfWhitespace()
    {
        CommandLineParser.TryParse("help   topics    extra", out var args).ShouldBeTrue();
        args!.CommandName.ShouldBe("help");
        args.Positional.ShouldBe(new[] { "topics", "extra" });
    }

    [Fact]
    public void HoldsDoubleQuotedTokensContainingSpacesTogether()
    {
        CommandLineParser.TryParse("screenshot \"my shot\"", out var args).ShouldBeTrue();
        args!.CommandName.ShouldBe("screenshot");
        args.Positional.ShouldBe(new[] { "my shot" });
    }

    [Fact]
    public void UnterminatedQuoteIsAParseFailureThatRunsNothing()
    {
        CommandLineParser.TryParse("screenshot \"unterminated", out var args).ShouldBeFalse();
        args.ShouldBeNull();
    }

    [Fact]
    public void CommandNameAloneHasNoPositionalArgs()
    {
        CommandLineParser.TryParse("help", out var args).ShouldBeTrue();
        args!.CommandName.ShouldBe("help");
        args.Positional.ShouldBeEmpty();
    }
}
