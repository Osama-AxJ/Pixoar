using Pixoar.Cli.Arguments;
using Pixoar.Core.Interfaces;

namespace Pixoar.Cli.Commands;

internal sealed class CommandContext(
    CommandLineArguments arguments,
    IApplicationPathProvider pathProvider,
    ISettingsService settingsService)
{
    public CommandLineArguments Arguments { get; } = arguments;

    public IApplicationPathProvider PathProvider { get; } = pathProvider;

    public ISettingsService SettingsService { get; } = settingsService;
}
