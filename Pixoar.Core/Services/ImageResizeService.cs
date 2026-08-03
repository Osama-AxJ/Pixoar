using ImageMagick;
using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.Core.Services;

internal sealed class ImageResizeService(
    IImageFormatDetector formatDetector,
    IOutputFileService outputFileService,
    ISettingsService settingsService,
    IDdsService ddsService,
    IApplicationLogger logger) : IImageResizeService
{
    public async Task<ImageOperationResult> ResizeAsync(
        ImageResizeRequest request,
        CancellationToken cancellationToken = default)
    {
        string? outputPath = null;
        var operationId = Guid.NewGuid().ToString("N");

        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(request.InputPath);
            NormalizeResizeRequest(request);
            var inputFormat = formatDetector.Detect(request.InputPath);
            var outputFormat = request.OutputFormat ?? inputFormat;
            var originalDimensions = await ReadDimensionsAsync(
                request.InputPath,
                inputFormat,
                cancellationToken).ConfigureAwait(false);
            var targetDimensions = ResolveTargetSize(
                originalDimensions.Width,
                originalDimensions.Height,
                request);

            var output = outputFileService.CreateOutputPath(new OutputFileRequest
            {
                SourcePath = request.InputPath,
                OutputFormat = outputFormat,
                OperationKind = OutputOperationKind.Resize,
                OutputFolder = request.OutputFolder
            });
            outputPath = output.Path;

            if (output.ShouldSkip)
            {
                await logger.LogInformationAsync(
                    $"Resize skipped because the output already exists. Input: {request.InputPath}. Output: {outputPath}.",
                    cancellationToken).ConfigureAwait(false);
                return ImageOperationResult.SkippedExisting(request.InputPath, outputPath);
            }

            EnsureOutputDoesNotReplaceInput(request.InputPath, outputPath);
            using var stagedOutput = new StagedOutputFile(outputPath, logger);
            await LogResizePlanAsync(
                operationId,
                request,
                inputFormat,
                outputFormat,
                originalDimensions,
                targetDimensions,
                outputPath,
                cancellationToken).ConfigureAwait(false);

            ImageDimensions actualDimensions;
            if (inputFormat == ImageFormat.Dds || outputFormat == ImageFormat.Dds)
            {
                actualDimensions = await ResizeWithDdsPipelineAsync(
                    operationId,
                    request,
                    stagedOutput.Path,
                    inputFormat,
                    outputFormat,
                    targetDimensions,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                actualDimensions = await ResizeWithMagickAsync(
                    request.InputPath,
                    stagedOutput.Path,
                    outputFormat,
                    request,
                    targetDimensions,
                    cancellationToken).ConfigureAwait(false);
            }

            stagedOutput.Validate();
            await logger.LogInformationAsync(
                $"Resize [{operationId}] calculated output dimensions: {actualDimensions}.",
                cancellationToken).ConfigureAwait(false);
            await ValidateOutputAsync(
                operationId,
                stagedOutput.Path,
                outputFormat,
                actualDimensions,
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            stagedOutput.Commit(output.AllowOverwrite);
            await logger.LogInformationAsync(
                $"Resize [{operationId}] completed. Final output path: {outputPath}. Validated dimensions: {actualDimensions}.",
                CancellationToken.None).ConfigureAwait(false);

            return ImageOperationResult.Succeeded(request.InputPath, outputPath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await LogOperationErrorAsync(
                operationId,
                "Resize",
                request.InputPath,
                outputPath,
                ex).ConfigureAwait(false);
            return ImageOperationResult.Failed("Resize", request.InputPath, outputPath, UserFacingErrorMessage.ForImageOperation(ex));
        }
    }

    public async Task<BatchImageOperationResult> ResizeBatchAsync(
        IEnumerable<ImageResizeRequest> requests,
        IProgress<ImageOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var requestList = requests.ToArray();
        var batchResult = new BatchImageOperationResult();

        for (var index = 0; index < requestList.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = requestList[index];
            progress?.Report(new ImageOperationProgress(index, requestList.Length, request.InputPath, "Resizing..."));

            var result = await ResizeAsync(request, cancellationToken).ConfigureAwait(false);
            batchResult.Results.Add(result);

            var status = result.Skipped ? "Skipped" : result.Success ? "Resized" : "Failed";
            progress?.Report(new ImageOperationProgress(index + 1, requestList.Length, request.InputPath, status));
        }

        return batchResult;
    }

    private async Task<ImageDimensions> ResizeWithDdsPipelineAsync(
        string operationId,
        ImageResizeRequest request,
        string outputPath,
        ImageFormat inputFormat,
        ImageFormat outputFormat,
        ImageDimensions targetDimensions,
        CancellationToken cancellationToken)
    {
        using var tempDirectory = new TemporaryDirectory(logger);
        var resizeInputPath = request.InputPath;

        if (inputFormat == ImageFormat.Dds)
        {
            resizeInputPath = Path.Combine(tempDirectory.Path, "decoded.png");
            await logger.LogInformationAsync(
                $"Resize [{operationId}] temporary decoded DDS intermediate path: {resizeInputPath}.",
                cancellationToken).ConfigureAwait(false);
            await ddsService.ConvertFromDdsAsync(
                request.InputPath,
                resizeInputPath,
                ImageFormat.Png,
                cancellationToken).ConfigureAwait(false);
        }

        if (outputFormat != ImageFormat.Dds)
        {
            return await ResizeWithMagickAsync(
                resizeInputPath,
                outputPath,
                outputFormat,
                request,
                targetDimensions,
                cancellationToken).ConfigureAwait(false);
        }

        var resizedPngPath = Path.Combine(tempDirectory.Path, "resized.png");
        await logger.LogInformationAsync(
            $"Resize [{operationId}] temporary resized lossless intermediate path: {resizedPngPath}.",
            cancellationToken).ConfigureAwait(false);
        var actualDimensions = await ResizeWithMagickAsync(
            resizeInputPath,
            resizedPngPath,
            ImageFormat.Png,
            request,
            targetDimensions,
            cancellationToken).ConfigureAwait(false);
        await logger.LogInformationAsync(
            $"Resize [{operationId}] calculated DDS output dimensions before encoding: {actualDimensions}.",
            cancellationToken).ConfigureAwait(false);
        await ddsService.ConvertToDdsAsync(resizedPngPath, outputPath, cancellationToken).ConfigureAwait(false);
        return actualDimensions;
    }

    private Task<ImageDimensions> ResizeWithMagickAsync(
        string inputPath,
        string outputPath,
        ImageFormat outputFormat,
        ImageResizeRequest request,
        ImageDimensions targetDimensions,
        CancellationToken cancellationToken)
    {
        return Task.Run(
            () => ResizeWithMagick(inputPath, outputPath, outputFormat, request, targetDimensions),
            cancellationToken);
    }

    private ImageDimensions ResizeWithMagick(
        string inputPath,
        string outputPath,
        ImageFormat outputFormat,
        ImageResizeRequest request,
        ImageDimensions targetDimensions)
    {
        var outputCreated = false;

        try
        {
            using var image = new MagickImage(inputPath);
            image.AutoOrient();
            ImageColorManagement.NormalizeToSrgb(image);

            ApplyResize(image, targetDimensions.Width, targetDimensions.Height, request);
            ImageEncodingSettingsApplier.Apply(image, outputFormat, settingsService.Current.Quality);

            using var outputStream = new FileStream(
                outputPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            outputCreated = true;
            image.Write(outputStream);
            return new ImageDimensions(image.Width, image.Height);
        }
        catch
        {
            if (outputCreated)
            {
                TryDeleteFile(outputPath);
            }

            throw;
        }
    }

    private static ImageDimensions ResolveTargetSize(
        uint originalWidth,
        uint originalHeight,
        ImageResizeRequest request)
    {
        if (IsPercentageResize(request))
        {
            var percentage = request.Percentage.GetValueOrDefault() / 100d;
            return new ImageDimensions(
                Math.Max(1, (uint)Math.Round(originalWidth * percentage)),
                Math.Max(1, (uint)Math.Round(originalHeight * percentage)));
        }

        var width = request.Width is > 0 ? (uint)request.Width.Value : originalWidth;
        var height = request.Height is > 0 ? (uint)request.Height.Value : originalHeight;
        return new ImageDimensions(width, height);
    }

    private static void ApplyResize(MagickImage image, uint width, uint height, ImageResizeRequest request)
    {
        if (IsPercentageResize(request))
        {
            image.Resize(new MagickGeometry(width, height) { IgnoreAspectRatio = true });
            return;
        }

        if (!request.KeepAspectRatio || request.Mode == ResizeMode.Stretch)
        {
            image.Resize(new MagickGeometry(width, height) { IgnoreAspectRatio = true });
            return;
        }

        if (request.Mode == ResizeMode.Crop)
        {
            image.Resize(new MagickGeometry(width, height) { FillArea = true });
            image.Crop(width, height, Gravity.Center);
            return;
        }

        image.Resize(new MagickGeometry(width, height));
    }

    private static void NormalizeResizeRequest(ImageResizeRequest request)
    {
        if (request.ResizeMethod == ResizeMethod.Percentage || request.Percentage is > 0)
        {
            if (request.Percentage is not > 0)
            {
                throw new InvalidOperationException("Choose a valid resize percentage.");
            }

            request.ResizeMethod = ResizeMethod.Percentage;
            request.Width = null;
            request.Height = null;
            request.KeepAspectRatio = true;
            request.Mode = ResizeMode.Fit;
            return;
        }

        request.ResizeMethod = ResizeMethod.Dimensions;
        if (request.Width is not > 0 && request.Height is not > 0)
        {
            throw new InvalidOperationException("Enter a width, height, or percentage before resizing.");
        }
    }

    private static bool IsPercentageResize(ImageResizeRequest request)
    {
        return request.ResizeMethod == ResizeMethod.Percentage && request.Percentage is > 0;
    }

    private Task<ImageDimensions> ReadDimensionsAsync(
        string inputPath,
        ImageFormat format,
        CancellationToken cancellationToken)
    {
        if (format == ImageFormat.Dds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var information = DdsHeaderReader.ReadBasicInformation(inputPath, formatDetector);
            if (information.Width <= 0 || information.Height <= 0)
            {
                throw new InvalidDataException("The DDS file does not contain valid image dimensions.");
            }

            return Task.FromResult(new ImageDimensions((uint)information.Width, (uint)information.Height));
        }

        return Task.Run(
            () =>
            {
                using var image = new MagickImage(inputPath);
                image.AutoOrient();
                return new ImageDimensions(image.Width, image.Height);
            },
            cancellationToken);
    }

    private async Task ValidateOutputAsync(
        string operationId,
        string outputPath,
        ImageFormat outputFormat,
        ImageDimensions expectedDimensions,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
        {
            throw CreateValidationException(outputFormat, $"The final output file was not created: {outputPath}");
        }

        var detectedFormat = formatDetector.Detect(outputPath);
        if (detectedFormat != outputFormat)
        {
            throw CreateValidationException(
                outputFormat,
                $"The final output format was {detectedFormat}, but {outputFormat} was requested.");
        }

        ImageDimensions actualDimensions;
        if (outputFormat == ImageFormat.Dds)
        {
            using var validationDirectory = new TemporaryDirectory(logger);
            var validationPngPath = Path.Combine(validationDirectory.Path, "validated.png");
            await logger.LogInformationAsync(
                $"Resize [{operationId}] temporary DDS read-back intermediate path: {validationPngPath}.",
                cancellationToken).ConfigureAwait(false);
            await ddsService.ConvertFromDdsAsync(
                outputPath,
                validationPngPath,
                ImageFormat.Png,
                cancellationToken).ConfigureAwait(false);
            actualDimensions = await ReadDimensionsAsync(
                validationPngPath,
                ImageFormat.Png,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            actualDimensions = await ReadDimensionsAsync(
                outputPath,
                outputFormat,
                cancellationToken).ConfigureAwait(false);
        }

        if (actualDimensions != expectedDimensions)
        {
            throw CreateValidationException(
                outputFormat,
                $"The final output dimensions were {actualDimensions}, but {expectedDimensions} were expected.");
        }
    }

    private async Task LogResizePlanAsync(
        string operationId,
        ImageResizeRequest request,
        ImageFormat inputFormat,
        ImageFormat outputFormat,
        ImageDimensions originalDimensions,
        ImageDimensions targetDimensions,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var requestDescription = IsPercentageResize(request)
            ? $"percentage {request.Percentage.GetValueOrDefault()}%"
            : $"dimensions {request.Width?.ToString() ?? "original"}x{request.Height?.ToString() ?? "original"}";
        var extension = Path.GetExtension(request.InputPath);

        await logger.LogInformationAsync(
            $"Resize [{operationId}] input path: {request.InputPath}. Original format: {inputFormat} ({extension}). Original dimensions: {originalDimensions}.",
            cancellationToken).ConfigureAwait(false);
        await logger.LogInformationAsync(
            $"Resize [{operationId}] requested resize: {requestDescription}. Mode: {request.Mode}. Keep aspect ratio: {request.KeepAspectRatio}. Calculated output bounds: {targetDimensions}.",
            cancellationToken).ConfigureAwait(false);
        await logger.LogInformationAsync(
            $"Resize [{operationId}] requested output format: {outputFormat}. Final output path: {outputPath}.",
            cancellationToken).ConfigureAwait(false);

        if (outputFormat == ImageFormat.Dds)
        {
            var dds = settingsService.Current.Dds;
            await logger.LogInformationAsync(
                $"Resize [{operationId}] DDS settings: Compression={dds.Compression}; Generate mipmaps={dds.GenerateMipmaps}; Preserve alpha={dds.PreserveAlpha}.",
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static InvalidOperationException CreateValidationException(ImageFormat outputFormat, string message)
    {
        var prefix = outputFormat == ImageFormat.Dds ? "DDS resize validation failed." : "Resize validation failed.";
        return new InvalidOperationException($"{prefix} {message}");
    }

    private static void EnsureOutputDoesNotReplaceInput(string inputPath, string outputPath)
    {
        if (string.Equals(
            Path.GetFullPath(inputPath),
            Path.GetFullPath(outputPath),
            StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("Resize output cannot overwrite the original image.");
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Preserve the resize failure; partial-output cleanup is best effort.
        }
    }

    private Task LogOperationErrorAsync(
        string operationId,
        string operation,
        string inputPath,
        string? outputPath,
        Exception exception)
    {
        var message = $"{operation} [{operationId}] failed. Input: {inputPath}. Final output: {outputPath ?? "none"}. Error: {exception.Message}";
        return logger.LogErrorAsync(message, exception, CancellationToken.None);
    }

    private readonly record struct ImageDimensions(uint Width, uint Height)
    {
        public override string ToString()
        {
            return $"{Width}x{Height}";
        }
    }
}
