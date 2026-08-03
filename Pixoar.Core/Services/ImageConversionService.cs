using ImageMagick;
using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.Core.Services;

internal sealed class ImageConversionService(
    IImageFormatDetector formatDetector,
    IOutputFileService outputFileService,
    ISettingsService settingsService,
    IDdsService ddsService,
    IApplicationLogger logger) : IImageConversionService
{
    public async Task<ImageOperationResult> ConvertAsync(
        ImageConversionRequest request,
        CancellationToken cancellationToken = default)
    {
        string? outputPath = null;

        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(request.InputPath);
            var inputFormat = formatDetector.Detect(request.InputPath);
            var output = outputFileService.CreateOutputPath(new OutputFileRequest
            {
                SourcePath = request.InputPath,
                OutputFormat = request.OutputFormat,
                OperationKind = OutputOperationKind.Convert,
                OutputFolder = request.OutputFolder
            });
            outputPath = output.Path;
            if (output.ShouldSkip)
            {
                await logger.LogInformationAsync(
                    $"Convert skipped because the output already exists. Input: {request.InputPath}. Output: {outputPath}.",
                    cancellationToken).ConfigureAwait(false);
                return ImageOperationResult.SkippedExisting(request.InputPath, outputPath);
            }

            using var stagedOutput = new StagedOutputFile(outputPath, logger);

            if (inputFormat == ImageFormat.Dds || request.OutputFormat == ImageFormat.Dds)
            {
                await ConvertWithDdsAsync(
                    request.InputPath,
                    stagedOutput.Path,
                    inputFormat,
                    request.OutputFormat,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await Task.Run(
                    () => ConvertWithMagick(request.InputPath, stagedOutput.Path, request.OutputFormat),
                    cancellationToken).ConfigureAwait(false);
            }

            stagedOutput.Validate();
            cancellationToken.ThrowIfCancellationRequested();
            stagedOutput.Commit(output.AllowOverwrite);
            return ImageOperationResult.Succeeded(request.InputPath, outputPath);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await LogOperationErrorAsync("Convert", request.InputPath, outputPath, ex, cancellationToken).ConfigureAwait(false);
            return ImageOperationResult.Failed("Convert", request.InputPath, outputPath, UserFacingErrorMessage.ForImageOperation(ex));
        }
    }

    public async Task<BatchImageOperationResult> ConvertBatchAsync(
        IEnumerable<ImageConversionRequest> requests,
        IProgress<ImageOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var requestList = requests.ToArray();
        var batchResult = new BatchImageOperationResult();

        for (var index = 0; index < requestList.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = requestList[index];
            progress?.Report(new ImageOperationProgress(index, requestList.Length, request.InputPath, "Converting..."));

            var result = await ConvertAsync(request, cancellationToken).ConfigureAwait(false);
            batchResult.Results.Add(result);

            var status = result.Skipped ? "Skipped" : result.Success ? "Converted" : "Failed";
            progress?.Report(new ImageOperationProgress(index + 1, requestList.Length, request.InputPath, status));
        }

        return batchResult;
    }

    private async Task ConvertWithDdsAsync(
        string inputPath,
        string outputPath,
        ImageFormat inputFormat,
        ImageFormat outputFormat,
        CancellationToken cancellationToken)
    {
        if (inputFormat == ImageFormat.Dds && outputFormat == ImageFormat.Dds)
        {
            CopyFile(inputPath, outputPath);
            return;
        }

        if (outputFormat == ImageFormat.Dds)
        {
            await ddsService.ConvertToDdsAsync(inputPath, outputPath, cancellationToken).ConfigureAwait(false);
            return;
        }

        await ddsService.ConvertFromDdsAsync(inputPath, outputPath, outputFormat, cancellationToken).ConfigureAwait(false);
    }

    private void ConvertWithMagick(string inputPath, string outputPath, ImageFormat outputFormat)
    {
        var outputCreated = false;

        try
        {
            using var image = new MagickImage(inputPath);
            image.AutoOrient();
            ImageColorManagement.NormalizeToSrgb(image);
            ImageEncodingSettingsApplier.Apply(image, outputFormat, settingsService.Current.Quality);
            using var outputStream = new FileStream(
                outputPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            outputCreated = true;
            image.Write(outputStream);
        }
        catch
        {
            if (outputCreated)
            {
                TryDeleteOwnedOutput(outputPath);
            }

            throw;
        }
    }

    private static void CopyFile(string inputPath, string outputPath)
    {
        var outputCreated = false;

        try
        {
            using var inputStream = new FileStream(
                inputPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            using var outputStream = new FileStream(
                outputPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            outputCreated = true;
            inputStream.CopyTo(outputStream);
        }
        catch
        {
            if (outputCreated)
            {
                TryDeleteOwnedOutput(outputPath);
            }

            throw;
        }
    }

    private static void TryDeleteOwnedOutput(string outputPath)
    {
        try
        {
            File.Delete(outputPath);
        }
        catch
        {
            // Preserve the operation failure; partial-output cleanup is best effort.
        }
    }

    private Task LogOperationErrorAsync(
        string operation,
        string inputPath,
        string? outputPath,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var message = $"{operation} failed. Input: {inputPath}. Output: {outputPath ?? "none"}. Error: {exception.Message}";
        return logger.LogErrorAsync(message, exception, cancellationToken);
    }
}
