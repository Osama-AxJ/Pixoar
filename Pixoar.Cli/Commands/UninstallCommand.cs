using Pixoar.Cli.Arguments;
using Pixoar.Cli.Execution;
using Pixoar.Core.Interfaces;

namespace Pixoar.Cli.Commands;

internal sealed class UninstallCommand(IPixoarCleanupService cleanupService) : ICommand
{
    public string Name => "uninstall";

    public string Description => "Removes Pixoar-owned registry entries and optional user data.";

    public bool CanHandle(CommandLineArguments arguments)
    {
        return string.Equals(arguments.CommandName, Name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(arguments.CommandName, "cleanup", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var options = CommandLineParser.Parse(context.Arguments.Values.Skip(1).ToArray());
        var removeUserData = options.HasOption("remove-user-data");
        var result = await cleanupService.CleanupAsync(removeUserData, cancellationToken);

        var lines = new List<string>
        {
            removeUserData ? "Pixoar uninstall with user data removal" : "Pixoar uninstall",
            string.Empty,
            "Removed:"
        };

        lines.AddRange(result.Actions.Select(action => $"  - {action}"));

        if (!result.Actions.Any())
        {
            lines.Add("  - Nothing needed to be removed.");
        }

        if (!removeUserData)
        {
            lines.Add(string.Empty);
            lines.Add("User settings and logs were kept. Use --remove-user-data to remove %AppData%\\Pixoar.");
        }

        if (result.Errors.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Issues:");
            lines.AddRange(result.Errors.Select(error => $"  - {error}"));
        }

        return new CommandResult(
            result.Success ? CliExitCodes.Success : CliExitCodes.PartialSuccess,
            string.Join(Environment.NewLine, lines));
    }
}
