namespace NewGame1.Core.Console;

/// <summary>
/// Outcome of running a console command. Never an exception at the console boundary (FR-016) — a
/// handler that throws is caught by <see cref="CommandRegistry"/> and converted to a
/// <see cref="Fail"/> carrying the exception detail.
/// </summary>
public sealed record CommandResult
{
    public bool Succeeded { get; }

    public string Message { get; }

    public string? FailureReason { get; }

    private CommandResult(bool succeeded, string message, string? failureReason)
    {
        Succeeded = succeeded;
        Message = message;
        FailureReason = failureReason;
    }

    public static CommandResult Ok(string message) => new(true, message, null);

    public static CommandResult Fail(string reason) => new(false, string.Empty, reason);
}
