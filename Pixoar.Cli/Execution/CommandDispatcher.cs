using Pixoar.Cli.Arguments;
using Pixoar.Cli.Commands;

namespace Pixoar.Cli.Execution;

internal sealed class CommandDispatcher(IEnumerable<ICommand> commands)
{
    private readonly IReadOnlyList<ICommand> _commands = commands.ToArray();

    public Task<CommandResult> DispatchAsync(
        CommandLineArguments arguments,
        CommandContext context,
        CancellationToken cancellationToken)
    {
        var command = _commands.FirstOrDefault(candidate => candidate.CanHandle(arguments));

        if (command is null)
        {
            return Task.FromResult(CommandResult.Failure(
                $"Unknown command '{arguments.CommandName}'. Run 'Pixoar.Cli help' for usage.",
                CliExitCodes.InvalidArguments));
        }

        return command.ExecuteAsync(context, cancellationToken);
    }
}
