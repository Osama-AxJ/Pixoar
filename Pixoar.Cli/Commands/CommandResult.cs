namespace Pixoar.Cli.Commands;

internal sealed record CommandResult(int ExitCode, string? Message = null)
{
    public static CommandResult Success(string? message = null)
    {
        return new CommandResult(0, message);
    }

    public static CommandResult Failure(string message, int exitCode = 1)
    {
        return new CommandResult(exitCode, message);
    }
}
