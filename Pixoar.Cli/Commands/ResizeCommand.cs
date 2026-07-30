using Pixoar.Cli.Arguments;
using Pixoar.Cli.Execution;
using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.Cli.Commands;

internal sealed class ResizeCommand(
    IImageResizeService resizeService,
    InputPathResolver inputPathResolver,
    IApplicationLogger logger) : ICommand
{
    public string Name => "resize";

    public string Description => "Resizes images by percentage preset or custom dimensions.";

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

        if (!TryCreateResizeTemplate(options, out var template, out var validationError))
        {
            return CommandResult.Failure(validationError, CliExitCodes.InvalidArguments);
        }

        var quiet = options.HasOption("quiet");
        IReadOnlyList<string> inputPaths;
        try
        {
            inputPaths = inputPathResolver.Resolve(options.Values, options.HasOption("recursive"));
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync("Resize argument resolution failed.", ex, cancellationToken);
            return CommandResult.Failure(ex.Message, CliExitCodes.InvalidArguments);
        }

        if (inputPaths.Count == 0)
        {
            return CommandResult.Failure("No supported input images were found.", CliExitCodes.InvalidArguments);
        }

        if (!quiet)
        {
            Console.WriteLine($"Resizing {inputPaths.Count} image{(inputPaths.Count == 1 ? string.Empty : "s")}...");
        }

        var requests = inputPaths.Select(path => new ImageResizeRequest
        {
            InputPath = path,
            ResizeMethod = template.ResizeMethod,
            Width = template.Width,
            Height = template.Height,
            Percentage = template.Percentage,
            KeepAspectRatio = template.KeepAspectRatio,
            Mode = template.Mode,
            OutputFolder = options.GetOption("output")
        });

        var progress = quiet
            ? null
            : new Progress<ImageOperationProgress>(value =>
            {
                Console.WriteLine($"[{value.Completed}/{value.Total}] {value.Status}: {Path.GetFileName(value.CurrentFile)}");
            });

        var result = await resizeService.ResizeBatchAsync(requests, progress, cancellationToken);
        await logger.LogInformationAsync(
            $"Resize command completed. Successful: {result.SuccessCount}. Failed: {result.ErrorCount}.",
            cancellationToken).ConfigureAwait(false);

        if (quiet && result.ErrorCount == 0)
        {
            return new CommandResult(CommandResultFormatter.ExitCodeFor(result));
        }

        return new CommandResult(
            CommandResultFormatter.ExitCodeFor(result),
            CommandResultFormatter.FormatBatchSummary("Resize", result));
    }

    private bool TryCreateResizeTemplate(
        ParsedCommandOptions options,
        out ImageResizeRequest request,
        out string error)
    {
        request = new ImageResizeRequest();
        error = string.Empty;

        var modeValue = options.GetOption("mode") ?? "fit";
        if (!CliFormatParser.TryParseResizeMode(modeValue, out var mode))
        {
            error = $"Unsupported resize mode: {modeValue}. Use fit, crop, or stretch.";
            return false;
        }

        request.Mode = mode;
        request.KeepAspectRatio = mode != ResizeMode.Stretch;

        var preset = options.GetOption("preset");
        if (!string.IsNullOrWhiteSpace(preset))
        {
            return TryApplyPreset(preset, request, out error);
        }

        if (options.HasOption("percentage"))
        {
            return TryApplyPercentage(options.GetOption("percentage"), request, out error);
        }

        if (!TryParseOptionalInt(options.GetOption("width"), "width", out var width, out error) ||
            !TryParseOptionalInt(options.GetOption("height"), "height", out var height, out error))
        {
            return false;
        }

        if (width is null && height is null)
        {
            error = "Resize requires --preset or at least one of --width/--height.";
            return false;
        }

        request.Width = width;
        request.Height = height;
        request.ResizeMethod = ResizeMethod.Dimensions;
        return true;
    }

    private static bool TryApplyPreset(string preset, ImageResizeRequest request, out string error)
    {
        error = string.Empty;
        var normalized = preset.Trim().ToLowerInvariant();

        if (normalized.EndsWith('%'))
        {
            return TryApplyPercentage(normalized.TrimEnd('%'), request, out error);
        }

        error = $"Invalid resize preset: {preset}. Use percentage presets like 50% or 75%.";
        return false;
    }

    private static bool TryApplyPercentage(string? value, ImageResizeRequest request, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(value) ||
            !int.TryParse(value.Trim().TrimEnd('%'), out var percentage) ||
            percentage <= 0)
        {
            error = $"Invalid --percentage value: {value ?? "<missing>"}";
            return false;
        }

        request.ResizeMethod = ResizeMethod.Percentage;
        request.Percentage = percentage;
        request.Width = null;
        request.Height = null;
        request.KeepAspectRatio = true;
        request.Mode = ResizeMode.Fit;
        return true;
    }

    private static bool TryParseOptionalInt(
        string? value,
        string optionName,
        out int? parsed,
        out string error)
    {
        parsed = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!int.TryParse(value, out var number) || number <= 0)
        {
            error = $"Invalid --{optionName} value: {value}";
            return false;
        }

        parsed = number;
        return true;
    }

}
