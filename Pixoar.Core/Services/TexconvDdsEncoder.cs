using System.Diagnostics;
using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.Core.Services;

internal sealed class TexconvDdsEncoder(
    IDdsDependencyService dependencyService,
    IApplicationLogger logger) : IDdsEncoder
{
    public bool IsAvailable => dependencyService.IsTexconvAvailable();

    public async Task ConvertToDdsAsync(
        string inputPath,
        string outputPath,
        DdsSettings settings,
        CancellationToken cancellationToken = default)
    {
        EnsureAvailable();

        var outputDirectory = Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(outputDirectory);

        using var tempDirectory = new TemporaryDirectory(logger);
        var useLegacyHeader = settings.Compression != DdsCompressionMode.Bc7;
        var compressionFormat = ToTexconvCompression(settings.Compression);
        var arguments = new List<string>
        {
            "-y"
        };

        if (!settings.GenerateMipmaps)
        {
            arguments.Add("-m");
            arguments.Add("1");
        }

        if (settings.PreserveAlpha)
        {
            arguments.Add("-sepalpha");
        }

        if (useLegacyHeader)
        {
            // Legacy DXT/RGBA has no sRGB bit. Keep the canonical sRGB sample
            // values unchanged and use the broadly supported DX9 header.
            arguments.Add("--ignore-srgb");
            arguments.Add("-dx9");
        }
        else
        {
            // BC7 has no legacy DDS representation, so its DX10 header carries
            // the explicit sRGB format.
            arguments.Add("-srgb");
        }

        arguments.Add("-ft");
        arguments.Add("dds");
        arguments.Add("-f");
        arguments.Add(compressionFormat);
        arguments.Add("-o");
        arguments.Add(tempDirectory.Path);
        arguments.Add(inputPath);

        await LogDdsConversionSettingsAsync(settings, compressionFormat, cancellationToken).ConfigureAwait(false);
        await RunTexconvAsync(arguments, $"DDS conversion with {FormatCompressionForLog(settings.Compression)}", cancellationToken).ConfigureAwait(false);
        var generatedPath = FindOutput(tempDirectory.Path, inputPath, "dds", "DDS conversion");
        var header = DdsHeaderReader.ValidateStandardTexture2D(generatedPath);
        await logger.LogInformationAsync(
            $"DDS Header: Texture2D; FourCC={header.FourCcDisplay}; DXGI={header.DxgiFormat?.ToString() ?? "none"}; " +
            $"ResourceDimension={header.ResourceDimension?.ToString() ?? "legacy"}; ArraySize={header.ArraySize?.ToString() ?? "legacy"}; " +
            $"Caps2=0x{header.Caps2:X8}; DepthField={header.Depth}.",
            cancellationToken).ConfigureAwait(false);
        MoveOutput(generatedPath, outputPath, "DDS conversion");
    }

    public async Task DecodeToPngAsync(
        string inputPath,
        string outputPngPath,
        CancellationToken cancellationToken = default)
    {
        EnsureAvailable();

        using var tempDirectory = new TemporaryDirectory(logger);
        var arguments = new[]
        {
            "-y",
            "-ft",
            "png",
            "-o",
            tempDirectory.Path,
            inputPath
        };

        await RunTexconvAsync(arguments, "DDS decoding", cancellationToken).ConfigureAwait(false);
        var generatedPath = FindOutput(tempDirectory.Path, inputPath, "png", "DDS decoding");
        MoveOutput(generatedPath, outputPngPath, "DDS decoding");
    }

    private async Task RunTexconvAsync(
        IEnumerable<string> arguments,
        string operationDescription,
        CancellationToken cancellationToken)
    {
        var texconvPath = dependencyService.ResolveTexconvPath();
        if (texconvPath is null)
        {
            throw new FileNotFoundException(dependencyService.MissingTexconvMessage);
        }

        var argumentList = arguments.ToArray();
        await logger.LogInformationAsync($"Using texconv.exe: {texconvPath}", cancellationToken).ConfigureAwait(false);
        await logger.LogInformationAsync($"Generated texconv arguments: {string.Join(' ', argumentList.Select(QuoteForLog))}", cancellationToken).ConfigureAwait(false);

        var startInfo = new ProcessStartInfo
        {
            FileName = texconvPath,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        foreach (var argument in argumentList)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start texconv.exe.");

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                var canceledOutput = await outputTask.ConfigureAwait(false);
                var canceledError = await errorTask.ConfigureAwait(false);
                await logger.LogWarningAsync(
                    "texconv cancellation requested; the process tree was terminated.",
                    CancellationToken.None).ConfigureAwait(false);
                await LogProcessResultAsync(
                    process.ExitCode,
                    canceledOutput,
                    canceledError,
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception terminationException)
            {
                await logger.LogErrorAsync(
                    "texconv could not be terminated cleanly after cancellation.",
                    terminationException,
                    CancellationToken.None).ConfigureAwait(false);
            }

            throw;
        }

        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        await LogProcessResultAsync(process.ExitCode, output, error, cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{operationDescription} failed. The selected DDS settings may not be supported for this image. Check the Pixoar log for texconv details.");
        }
    }

    private async Task LogProcessResultAsync(
        int exitCode,
        string output,
        string error,
        CancellationToken cancellationToken)
    {
        await logger.LogInformationAsync($"texconv exit code: {exitCode}", cancellationToken).ConfigureAwait(false);
        await logger.LogInformationAsync(
            $"texconv stdout: {(string.IsNullOrWhiteSpace(output) ? "<empty>" : output.Trim())}",
            cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(error))
        {
            await logger.LogInformationAsync("texconv stderr: <empty>", cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await logger.LogWarningAsync($"texconv stderr: {error.Trim()}", cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task LogDdsConversionSettingsAsync(
        DdsSettings settings,
        string compressionFormat,
        CancellationToken cancellationToken)
    {
        await logger.LogInformationAsync($"DDS Compression: {FormatCompressionForLog(settings.Compression)} ({compressionFormat})", cancellationToken).ConfigureAwait(false);
        await logger.LogInformationAsync($"DDS Mipmaps: {settings.GenerateMipmaps}", cancellationToken).ConfigureAwait(false);
        await logger.LogInformationAsync($"DDS Preserve Alpha: {settings.PreserveAlpha}", cancellationToken).ConfigureAwait(false);
        await logger.LogInformationAsync(
            $"DDS Header Mode: {(settings.Compression == DdsCompressionMode.Bc7 ? "DX10 Texture2D" : "Legacy DX9 Texture2D")}",
            cancellationToken).ConfigureAwait(false);
        await logger.LogInformationAsync(
            $"DDS Color Space: {(settings.Compression == DdsCompressionMode.Bc7 ? "Explicit sRGB" : "sRGB samples in legacy color-texture format")}",
            cancellationToken).ConfigureAwait(false);
    }

    private void EnsureAvailable()
    {
        if (!IsAvailable)
        {
            throw new FileNotFoundException(dependencyService.MissingTexconvMessage);
        }
    }

    private static string FindOutput(
        string directory,
        string inputPath,
        string extension,
        string operationDescription)
    {
        var expectedName = $"{Path.GetFileNameWithoutExtension(inputPath)}.{extension}";
        var expectedPath = Path.Combine(directory, expectedName);
        var file = File.Exists(expectedPath)
            ? expectedPath
            : Directory.EnumerateFiles(directory, $"*.{extension}", SearchOption.TopDirectoryOnly).FirstOrDefault();

        if (file is null)
        {
            throw new InvalidOperationException(
                $"{operationDescription} failed. texconv did not create a .{extension} output file.");
        }

        return file;
    }

    private static void MoveOutput(
        string inputPath,
        string outputPath,
        string operationDescription)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory);
        File.Move(inputPath, outputPath, overwrite: false);

        if (!File.Exists(outputPath))
        {
            throw new InvalidOperationException(
                $"{operationDescription} failed. texconv output could not be saved to {outputPath}.");
        }
    }

    private static string ToTexconvCompression(DdsCompressionMode compression)
    {
        return compression switch
        {
            DdsCompressionMode.Dxt1 => "BC1_UNORM",
            DdsCompressionMode.Dxt3 => "BC2_UNORM",
            DdsCompressionMode.Dxt5 => "BC3_UNORM",
            DdsCompressionMode.Bc7 => "BC7_UNORM_SRGB",
            DdsCompressionMode.Uncompressed => "R8G8B8A8_UNORM",
            _ => "BC3_UNORM"
        };
    }

    private static string FormatCompressionForLog(DdsCompressionMode compression)
    {
        return compression switch
        {
            DdsCompressionMode.Dxt1 => "DXT1",
            DdsCompressionMode.Dxt3 => "DXT3",
            DdsCompressionMode.Dxt5 => "DXT5",
            DdsCompressionMode.Bc7 => "BC7",
            DdsCompressionMode.Uncompressed => "Uncompressed",
            _ => compression.ToString()
        };
    }

    private static string QuoteForLog(string value)
    {
        return value.Contains(' ') ? $"\"{value}\"" : value;
    }
}
