using System.Text.Json;
using System.Text.Json.Serialization;
using Pixoar.Cli.Arguments;
using Pixoar.Cli.Execution;
using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.Cli.Commands;

internal sealed class InfoCommand(
    IImageInfoService imageInfoService,
    InputPathResolver inputPathResolver,
    IApplicationLogger logger) : ICommand
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public string Name => "info";

    public string Description => "Prints image information.";

    public bool CanHandle(CommandLineArguments arguments)
    {
        return string.Equals(arguments.CommandName, Name, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var options = CommandLineParser.Parse(context.Arguments.Values.Skip(1).ToArray());
        if (options.Values.Count == 0)
        {
            return CommandResult.Failure("No input files or folders were provided.", CliExitCodes.InvalidArguments);
        }

        IReadOnlyList<string> inputPaths;
        try
        {
            inputPaths = inputPathResolver.Resolve(options.Values, options.HasOption("recursive"));
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync("Info argument resolution failed.", ex, cancellationToken);
            return CommandResult.Failure(ex.Message, CliExitCodes.InvalidArguments);
        }

        if (inputPaths.Count == 0)
        {
            return CommandResult.Failure("No supported input images were found.", CliExitCodes.InvalidArguments);
        }

        var infos = new List<ImageInformation>();
        var errors = new List<ImageOperationError>();

        foreach (var path in inputPaths)
        {
            try
            {
                infos.Add(await imageInfoService.GetInformationAsync(path, cancellationToken));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await logger.LogErrorAsync($"Info failed. Input: {path}. Error: {ex.Message}", ex, cancellationToken);
                errors.Add(new ImageOperationError
                {
                    Operation = "Info",
                    InputPath = path,
                    Message = ex.Message,
                    Timestamp = DateTimeOffset.Now
                });
            }
        }

        var output = options.HasOption("json")
            ? FormatJson(infos, errors)
            : FormatText(infos, errors);

        var exitCode = errors.Count == 0
            ? CliExitCodes.Success
            : infos.Count > 0 ? CliExitCodes.PartialSuccess : CliExitCodes.Failure;

        return new CommandResult(exitCode, output);
    }

    private static string FormatJson(
        IReadOnlyCollection<ImageInformation> infos,
        IReadOnlyCollection<ImageOperationError> errors)
    {
        var payload = new
        {
            images = infos,
            errors
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static string FormatText(
        IReadOnlyCollection<ImageInformation> infos,
        IReadOnlyCollection<ImageOperationError> errors)
    {
        var lines = new List<string>();

        foreach (var info in infos)
        {
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add(info.FileName);
            lines.Add($"  Path:          {info.FilePath}");
            lines.Add($"  Extension:     {info.Extension}");
            lines.Add($"  Size:          {info.FileSize}");
            lines.Add($"  Format:        {info.FormatDisplayName}");
            lines.Add($"  Dimensions:    {info.Width}x{info.Height}");
            lines.Add($"  Aspect ratio:  {info.AspectRatio}");
            lines.Add($"  Created:       {info.CreatedDate:g}");
            lines.Add($"  Modified:      {info.LastModifiedDate:g}");
            lines.Add($"  Color depth:   {info.ColorDepth}");
            lines.Add($"  Alpha channel: {FormatYesNo(info.HasAlpha)}");
            lines.Add($"  Transparency:  {FormatYesNo(info.HasTransparency)}");

            if (info.Dds is not null)
            {
                lines.Add("  DDS:");
                lines.Add($"    Compression: {info.Dds.CompressionType}");
                lines.Add($"    Mipmaps:     {info.Dds.MipmapCount?.ToString() ?? "Unknown"}");
            }
        }

        foreach (var error in errors)
        {
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add($"ERROR: {error.InputPath}");
            lines.Add($"  {error.Message}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatYesNo(bool value)
    {
        return value ? "Yes" : "No";
    }
}
