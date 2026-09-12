using Pixoar.Cli.Arguments;
using Pixoar.Cli.Execution;
using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.Cli.Commands;

/// <summary>Upscales images locally with the bundled Real-ESRGAN NCNN/Vulkan backend.</summary>
internal sealed class UpscaleCommand(
    IImageUpscaleService upscaleService,
    InputPathResolver inputPathResolver,
    IApplicationLogger logger) : ICommand
{
    public string Name => "upscale";

    public string Description => "Upscales images locally with Real-ESRGAN.";

    public bool CanHandle(CommandLineArguments arguments) =>
        string.Equals(arguments.CommandName, Name, StringComparison.OrdinalIgnoreCase);

    public async Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var options = CommandLineParser.Parse(context.Arguments.Values.Skip(1).ToArray());
        if (options.Values.Count == 0)
        {
            return CommandResult.Failure("No input files or folders were provided.", CliExitCodes.InvalidArguments);
        }

        var scaleText = options.GetOption("scale") ?? "2";
        if (!int.TryParse(scaleText, out var scale) || scale is not 2 and not 4)
        {
            return CommandResult.Failure("Upscale requires --scale 2 or --scale 4.", CliExitCodes.InvalidArguments);
        }

        IReadOnlyList<string> inputPaths;
        try
        {
            inputPaths = inputPathResolver.Resolve(options.Values, options.HasOption("recursive"));
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync("Upscale argument resolution failed.", ex, cancellationToken);
            return CommandResult.Failure(ex.Message, CliExitCodes.InvalidArguments);
        }

        if (inputPaths.Count == 0)
        {
            return CommandResult.Failure("No supported input images were found.", CliExitCodes.InvalidArguments);
        }

        var quiet = options.HasOption("quiet");
        var requests = inputPaths.Select(path => new UpscaleRequest { InputPath = path, Scale = scale, OutputFolder = options.GetOption("output") });
        var progress = quiet ? null : new Progress<ImageOperationProgress>(value =>
            Console.WriteLine($"[{value.Completed}/{value.Total}] {value.Status}: {Path.GetFileName(value.CurrentFile)}"));
        var result = await upscaleService.UpscaleBatchAsync(requests, progress, cancellationToken);
        await logger.LogInformationAsync($"Upscale command completed. Successful: {result.SuccessCount}. Failed: {result.ErrorCount}.", cancellationToken);
        return quiet && result.ErrorCount == 0
            ? new CommandResult(CommandResultFormatter.ExitCodeFor(result))
            : new CommandResult(CommandResultFormatter.ExitCodeFor(result), CommandResultFormatter.FormatBatchSummary("Upscale", result));
    }
}
