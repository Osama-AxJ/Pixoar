using Pixoar.Cli.Arguments;
using Pixoar.Cli.Execution;
using Pixoar.Core.Interfaces;

namespace Pixoar.Cli.Commands;

internal sealed class RepairContextMenuCommand(IContextMenuService contextMenuService) : ICommand
{
    public string Name => "repair-context-menu";

    public string Description => "Rebuilds Pixoar-owned Windows Explorer context menu entries.";

    public bool CanHandle(CommandLineArguments arguments)
    {
        return string.Equals(arguments.CommandName, Name, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        await context.SettingsService.UpdateAsync(settings => settings.ContextMenu.EnableContextMenu = true).ConfigureAwait(false);
        var report = await contextMenuService.RepairAsync(cancellationToken).ConfigureAwait(false);

        return new CommandResult(
            report.IsValid ? CliExitCodes.Success : CliExitCodes.Failure,
            report.ToDisplayText());
    }
}
