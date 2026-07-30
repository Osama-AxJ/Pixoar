using Pixoar.Cli.Arguments;

namespace Pixoar.Cli.Commands;

internal interface ICommand
{
    string Name { get; }

    string Description { get; }

    bool CanHandle(CommandLineArguments arguments);

    Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken cancellationToken);
}
