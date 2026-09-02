using NewGame1.Core.Console;

namespace NewGame1.Core.Screenshots;

/// <summary>
/// Registers <c>screenshot [name]</c> against a <see cref="CommandRegistry"/>
/// (contracts/console-commands.md). Stays in Core so argument validation and result formatting are
/// fast-tier testable against a fake <see cref="IScreenshotService"/> (research R8).
/// </summary>
public static class ScreenshotCommand
{
    public static void Register(CommandRegistry registry, IScreenshotService service)
    {
        registry.Register(new CommandDescriptor(
            "screenshot",
            "Capture the current view to artifacts/<name>.png.",
            "screenshot [name]",
            args => Execute(service, args)));
    }

    private static CommandResult Execute(IScreenshotService service, CommandArgs args)
    {
        var raw = args.Positional.Count > 0 ? args.Positional[0] : null;

        if (!ScreenshotName.TryCreate(raw, out var name, out var error) || name is null)
        {
            return CommandResult.Fail(error ?? $"Invalid screenshot name '{raw}'.");
        }

        var result = service.Capture(name);

        if (!result.Succeeded)
        {
            return CommandResult.Fail(result.FailureReason ?? "Screenshot capture failed.");
        }

        var message = result.Replaced
            ? $"Replaced existing screenshot: {result.Path}"
            : $"Wrote screenshot: {result.Path}";

        return CommandResult.Ok(message);
    }
}
