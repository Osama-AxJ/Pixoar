using Pixoar.Cli.Arguments;
using Pixoar.Cli.Execution;
using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.Cli.Commands;

internal sealed class ConvertCommand(
    IImageConversionService conversionService,
    IImageFormatDetector formatDetector,
    IDdsService ddsService,
    IDdsDependencyService ddsDependencyService,
    InputPathResolver inputPathResolver,
    IApplicationLogger logger) : ICommand
{
    public string Name => "convert";

    public string Description => "Converts images to a target format.";

    public bool CanHandle(CommandLineArguments arguments)
    {
        return string.Equals(arguments.CommandName, Name, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var options = CommandLineParser.Parse(context.Arguments.Values.Skip(1).ToArray());
        var formatValue = options.GetOption("format");

        if (string.IsNullOrWhiteSpace(formatValue))
        {
            return CommandResult.Failure("Missing required option: --format", CliExitCodes.InvalidArguments);
        }

        DdsCompressionMode? compressionOverride = null;
        ImageFormat outputFormat;
        if (CliFormatParser.TryParseExplorerDdsFormat(formatValue, out var explorerCompression))
        {
            outputFormat = ImageFormat.Dds;
            compressionOverride = explorerCompression;
        }
        else if (!CliFormatParser.TryParseImageFormat(formatValue, out outputFormat))
        {
            return CommandResult.Failure($"Unsupported output format: {formatValue}", CliExitCodes.InvalidArguments);
        }

        if (options.HasOption("compression"))
        {
            var compressionValue = options.GetOption("compression");
            if (outputFormat != ImageFormat.Dds)
            {
                return CommandResult.Failure(
                    "--compression can only be used with --format dds.",
                    CliExitCodes.InvalidArguments);
            }

            if (string.IsNullOrWhiteSpace(compressionValue) ||
                !CliFormatParser.TryParseDdsCompression(compressionValue, out var parsedCompression))
            {
                return CommandResult.Failure(
                    $"Unsupported DDS compression: {compressionValue ?? "<missing>"}. Use DXT1, DXT3, DXT5, BC7, or Uncompressed.",
                    CliExitCodes.InvalidArguments);
            }

            compressionOverride = parsedCompression;
        }

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
            await logger.LogErrorAsync("Convert argument resolution failed.", ex, cancellationToken);
            return CommandResult.Failure(ex.Message, CliExitCodes.InvalidArguments);
        }

        if (inputPaths.Count == 0)
        {
            return CommandResult.Failure("No supported input images were found.", CliExitCodes.InvalidArguments);
        }

        if (RequiresDds(inputPaths, outputFormat) && !ddsService.IsAvailable)
        {
            var message = ddsDependencyService.MissingTexconvMessage;
            await logger.LogErrorAsync(message, null, cancellationToken);
            return CommandResult.Failure(message, CliExitCodes.MissingDependency);
        }

        var quiet = options.HasOption("quiet");
        if (!quiet)
        {
            Console.WriteLine($"Converting {inputPaths.Count} image{(inputPaths.Count == 1 ? string.Empty : "s")} to {formatDetector.GetDisplayName(outputFormat)}...");
        }

        var requests = inputPaths.Select(path => new ImageConversionRequest
        {
            InputPath = path,
            OutputFormat = outputFormat,
            DdsCompression = compressionOverride,
            OutputFolder = options.GetOption("output")
        });

        var progress = quiet
            ? null
            : new Progress<ImageOperationProgress>(value =>
            {
                Console.WriteLine($"[{value.Completed}/{value.Total}] {value.Status}: {Path.GetFileName(value.CurrentFile)}");
            });

        var result = await conversionService.ConvertBatchAsync(requests, progress, cancellationToken);
        if (quiet && result.ErrorCount == 0)
        {
            return new CommandResult(CommandResultFormatter.ExitCodeFor(result));
        }

        return new CommandResult(
            CommandResultFormatter.ExitCodeFor(result),
            CommandResultFormatter.FormatBatchSummary("Convert", result));
    }

    private bool RequiresDds(IEnumerable<string> inputPaths, ImageFormat outputFormat)
    {
        return outputFormat == ImageFormat.Dds
            || inputPaths.Any(path => formatDetector.TryDetect(path, out var format) && format == ImageFormat.Dds);
    }
}
