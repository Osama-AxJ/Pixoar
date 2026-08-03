using Pixoar.Cli.Arguments;
using System.Reflection;

namespace Pixoar.Cli.Commands;

internal sealed class HelpCommand : ICommand
{
    public string Name => "help";

    public string Description => "Shows command line usage.";

    public bool CanHandle(CommandLineArguments arguments)
    {
        return string.IsNullOrWhiteSpace(arguments.CommandName)
            || string.Equals(arguments.CommandName, "help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(arguments.CommandName, "--help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(arguments.CommandName, "-h", StringComparison.OrdinalIgnoreCase);
    }

    public Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.2.0";
        var message = $"""
        Pixoar CLI {version}

        Usage:
          Pixoar.Cli help
          Pixoar.Cli settings
          Pixoar.Cli convert --format <format> [--compression <mode>] [--output <folder>] [--recursive] <file|folder> [...]
          Pixoar.Cli resize (--preset <percent> | --percentage <number> | --width <px> [--height <px>] | --height <px>) [--mode fit|crop|stretch] [--output <folder>] [--recursive] <file|folder> [...]
          Pixoar.Cli info [--json] [--recursive] <file|folder> [...]
          Pixoar.Cli uninstall [--remove-user-data]
          Pixoar.Cli diagnose-context-menu
          Pixoar.Cli repair-context-menu

        Commands:
          convert   Convert one or more images to PNG, JPG, WEBP, BMP, TIFF, or DDS.
          resize    Resize one or more images by percentage preset or dimensions.
          info      Print image metadata in text or JSON.
          settings  Show shared settings and log locations.
          uninstall Remove Pixoar registry entries and optional user data.
          diagnose-context-menu Diagnose installed Explorer context menu commands.
          repair-context-menu   Rebuild Pixoar Explorer context menu commands.

        DDS compression modes:
          DXT1, DXT3, DXT5, BC7, or Uncompressed. The configured default is used when omitted.

        Examples:
          Pixoar.Cli convert --format jpg ".\Images\image.png"
          Pixoar.Cli convert --format dds ".\Images\image.png"
          Pixoar.Cli convert --format dds --compression BC7 ".\Images\image.png"
          Pixoar.Cli resize --preset 50% ".\Images\image.png"
          Pixoar.Cli resize --preset 75% ".\Images\image.png"
          Pixoar.Cli resize --percentage 50 ".\Images\image.png"
          Pixoar.Cli resize --percentage 75 ".\Images\image.png"
          Pixoar.Cli resize --width 1024 --height 1024 --mode fit ".\Images\image.png"
          Pixoar.Cli info ".\Images\image.dds"
          Pixoar.Cli info ".\Images\image.png" --json
          Pixoar.Cli uninstall
          Pixoar.Cli uninstall --remove-user-data
          Pixoar.Cli diagnose-context-menu
          Pixoar.Cli repair-context-menu

        Exit codes:
          0  Success
          1  Partial success
          2  Failure
          3  Invalid arguments
          4  Missing dependency such as texconv.exe
        """;

        return Task.FromResult(CommandResult.Success(message));
    }
}
