using System.Diagnostics;
using ImageMagick;
using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.Core.Services;

/// <summary>Runs the bundled Real-ESRGAN NCNN/Vulkan executable without exposing its process details to the UI.</summary>
internal sealed class RealEsrganNcnnUpscaleService(
    IImageFormatDetector formatDetector,
    IOutputFileService outputFileService,
    IRealEsrganDependencyService dependencyService,
    IApplicationLogger logger) : IImageUpscaleService
{
    private const string ModelName = "realesrgan-x4plus";
    private const int ModelNativeScale = 4;

    public async Task<ImageOperationResult> UpscaleAsync(UpscaleRequest request, CancellationToken cancellationToken = default)
    {
        string? outputPath = null;
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(request.InputPath);
            if (request.Scale is not 2 and not 4)
            {
                throw new ArgumentOutOfRangeException(nameof(request.Scale), "Upscale supports 2x or 4x.");
            }

            var format = formatDetector.Detect(request.InputPath);
            if (format == ImageFormat.Dds)
            {
                throw new NotSupportedException("DDS files are not supported for AI upscaling.");
            }

            var executable = dependencyService.ResolveExecutablePath()
                ?? throw new FileNotFoundException(dependencyService.MissingBackendMessage);
            var modelsDirectory = dependencyService.ResolveModelsDirectory()
                ?? throw new FileNotFoundException("The bundled AI upscale model is missing. Reinstall Pixoar.");

            var output = outputFileService.CreateOutputPath(new OutputFileRequest
            {
                SourcePath = request.InputPath,
                OutputFormat = ImageFormat.Png,
                OperationKind = OutputOperationKind.Upscale,
                OutputFolder = request.OutputFolder
            });
            outputPath = output.Path;
            if (output.ShouldSkip)
            {
                return ImageOperationResult.SkippedExisting(request.InputPath, outputPath);
            }

            using var stagedOutput = new StagedOutputFile(outputPath, logger);
            using var temporaryDirectory = new TemporaryDirectory(logger);
            var preparedInput = Path.Combine(temporaryDirectory.Path, "input.png");
            var alphaMask = Path.Combine(temporaryDirectory.Path, "alpha.png");
            var hasAlpha = await PrepareInputAsync(request.InputPath, preparedInput, alphaMask, cancellationToken).ConfigureAwait(false);
            var nativeOutput = Path.Combine(temporaryDirectory.Path, "upscaled.png");

            // realesrgan-x4plus is a native 4x model. With the bundled NCNN build,
            // requesting -s 2 produces a valid-sized canvas but can shift/crop its RGB
            // pixels. Infer at the model's native scale and, for 2x, downsample the AI
            // result with Lanczos. This keeps the full image while retaining AI detail.
            await RunBackendAsync(executable, modelsDirectory, preparedInput, nativeOutput, ModelNativeScale, cancellationToken).ConfigureAwait(false);
            if (!File.Exists(nativeOutput) || new FileInfo(nativeOutput).Length == 0)
            {
                throw new InvalidDataException("The local AI upscaler did not create an output image.");
            }

            ValidateDimensions(request.InputPath, nativeOutput, ModelNativeScale);
            await WriteFinalOutputAsync(nativeOutput, alphaMask, hasAlpha, stagedOutput.Path, request.Scale, cancellationToken).ConfigureAwait(false);
            ValidateDimensions(request.InputPath, stagedOutput.Path, request.Scale);
            stagedOutput.Validate();
            cancellationToken.ThrowIfCancellationRequested();
            stagedOutput.Commit(output.AllowOverwrite);
            await logger.LogInformationAsync($"AI upscale completed. Input: {request.InputPath}. Output: {outputPath}. Scale: {request.Scale}x.", CancellationToken.None).ConfigureAwait(false);
            return ImageOperationResult.Succeeded(request.InputPath, outputPath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync($"AI upscale failed. Input: {request.InputPath}. Output: {outputPath ?? "none"}. Error: {ex.Message}", ex, CancellationToken.None).ConfigureAwait(false);
            return ImageOperationResult.Failed("Upscale", request.InputPath, outputPath, UserFacingErrorMessage.ForImageOperation(ex));
        }
    }

    public async Task<BatchImageOperationResult> UpscaleBatchAsync(IEnumerable<UpscaleRequest> requests, IProgress<ImageOperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var requestList = requests.ToArray();
        var batch = new BatchImageOperationResult();
        for (var index = 0; index < requestList.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = requestList[index];
            progress?.Report(new ImageOperationProgress(index, requestList.Length, request.InputPath, "Upscaling..."));
            var result = await UpscaleAsync(request, cancellationToken).ConfigureAwait(false);
            batch.Results.Add(result);
            progress?.Report(new ImageOperationProgress(index + 1, requestList.Length, request.InputPath, result.Skipped ? "Skipped" : result.Success ? "Upscaled" : "Failed"));
        }

        return batch;
    }

    private async Task RunBackendAsync(string executable, string modelsDirectory, string inputPath, string outputPath, int scale, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = Path.GetDirectoryName(executable)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[] { "-i", inputPath, "-o", outputPath, "-s", scale.ToString(), "-t", "0", "-m", modelsDirectory, "-n", ModelName, "-f", "png" })
        {
            startInfo.ArgumentList.Add(argument);
        }

        await logger.LogInformationAsync($"Using Real-ESRGAN NCNN backend: {executable}. Model: {ModelName}. Scale: {scale}x.", cancellationToken).ConfigureAwait(false);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start the local AI upscaler.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }

            await logger.LogWarningAsync("AI upscale cancellation requested; the native process tree was terminated.", CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        await logger.LogInformationAsync($"Real-ESRGAN exit code: {process.ExitCode}. stdout: {(string.IsNullOrWhiteSpace(stdout) ? "<empty>" : stdout.Trim())}", cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            await logger.LogWarningAsync($"Real-ESRGAN stderr: {stderr.Trim()}", cancellationToken).ConfigureAwait(false);
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("AI upscaling requires a Vulkan-compatible GPU and graphics driver. Check the Pixoar log for backend details.");
        }
    }

    private static Task<bool> PrepareInputAsync(string inputPath, string outputPath, string alphaPath, CancellationToken cancellationToken) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var image = new MagickImage(inputPath);
        image.AutoOrient();
        ImageColorManagement.NormalizeToSrgb(image);
        var rgba = image.GetPixels().ToByteArray(PixelMapping.RGBA) ?? throw new InvalidDataException("The image pixels could not be read.");
        var alpha = rgba.Where((_, index) => index % 4 == 3).ToArray();
        var hasAlpha = alpha.Any(value => value != byte.MaxValue);
        if (hasAlpha)
        {
            using var alphaImage = new MagickImage(alpha, new PixelReadSettings(image.Width, image.Height, StorageType.Char, "R"));
            alphaImage.Write(alphaPath);
        }

        image.Alpha(AlphaOption.Off);
        image.ColorType = ColorType.TrueColor;
        image.Format = MagickFormat.Png;
        image.Write(outputPath);
        return hasAlpha;
    }, cancellationToken);

    private static Task WriteFinalOutputAsync(string nativeOutput, string alphaPath, bool hasAlpha, string finalOutput, int requestedScale, CancellationToken cancellationToken) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var image = new MagickImage(nativeOutput);
        if (requestedScale != ModelNativeScale)
        {
            image.FilterType = FilterType.Lanczos;
            image.Resize(new MagickGeometry(image.Width / (uint)ModelNativeScale * (uint)requestedScale,
                image.Height / (uint)ModelNativeScale * (uint)requestedScale)
            {
                IgnoreAspectRatio = true
            });
        }

        if (hasAlpha)
        {
            using var alpha = new MagickImage(alphaPath);
            alpha.FilterType = FilterType.Lanczos;
            alpha.Resize(new MagickGeometry(image.Width, image.Height) { IgnoreAspectRatio = true });
            image.Alpha(AlphaOption.Set);
            image.Composite(alpha, CompositeOperator.CopyAlpha);
            image.ColorType = ColorType.TrueColorAlpha;
        }

        image.Format = MagickFormat.Png;
        image.Write(finalOutput);
    }, cancellationToken);

    private static void ValidateDimensions(string inputPath, string outputPath, int scale)
    {
        using var input = new MagickImage(inputPath);
        using var output = new MagickImage(outputPath);
        if (output.Width != input.Width * scale || output.Height != input.Height * scale)
        {
            throw new InvalidDataException($"The local AI upscaler created {output.Width}x{output.Height}; expected {input.Width * scale}x{input.Height * scale}.");
        }
    }
}
