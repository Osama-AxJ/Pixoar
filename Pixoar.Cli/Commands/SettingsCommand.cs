using Pixoar.Cli.Arguments;
using Pixoar.Core.Interfaces;
using System.Reflection;

namespace Pixoar.Cli.Commands;

internal sealed class SettingsCommand(IDdsDependencyService ddsDependencyService) : ICommand
{
    public string Name => "settings";

    public string Description => "Shows shared settings and log locations.";

    public bool CanHandle(CommandLineArguments arguments)
    {
        return string.Equals(arguments.CommandName, Name, StringComparison.OrdinalIgnoreCase);
    }

    public Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var settings = context.SettingsService.Current;
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";
        var resolvedTexconvPath = ddsDependencyService.ResolveTexconvPath();
        var texconvStatus = resolvedTexconvPath is null ? "Not found" : resolvedTexconvPath;
        var enabledResizePresets = settings.ResizePresets
            .Where(preset => preset.IsEnabled)
            .Select(preset => preset.Name)
            .ToArray();
        var resizePresets = enabledResizePresets.Length == 0
            ? "None"
            : string.Join(", ", enabledResizePresets);
        var message = $"""
        Pixoar settings

        Version:       {version}

        Settings file: {context.PathProvider.SettingsFilePath}
        Logs folder:  {context.PathProvider.LogsDirectory}
        texconv.exe:  {texconvStatus}

        Current settings:
          Resize presets:  {resizePresets}
          Convert presets: {settings.ConvertPresets.Count}
        """;

        return Task.FromResult(CommandResult.Success(message));
    }
}
