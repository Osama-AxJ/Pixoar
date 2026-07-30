using Microsoft.Extensions.DependencyInjection;
using Pixoar.Cli.Commands;

namespace Pixoar.Cli.Execution;

internal static class CliServiceCollectionExtensions
{
    public static IServiceCollection AddPixoarCli(this IServiceCollection services)
    {
        services.AddSingleton<ICommand, HelpCommand>();
        services.AddSingleton<ICommand, SettingsCommand>();
        services.AddSingleton<ICommand, ConvertCommand>();
        services.AddSingleton<ICommand, ResizeCommand>();
        services.AddSingleton<ICommand, InfoCommand>();
        services.AddSingleton<ICommand, UninstallCommand>();
        services.AddSingleton<ICommand, DiagnoseContextMenuCommand>();
        services.AddSingleton<ICommand, RepairContextMenuCommand>();
        services.AddSingleton<InputPathResolver>();
        services.AddSingleton<CommandDispatcher>();

        return services;
    }
}
