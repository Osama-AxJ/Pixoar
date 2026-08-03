using ImageMagick;
using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.Core.Services;

internal sealed class DdsService(
    IDdsEncoder ddsEncoder,
    ISettingsService settingsService,
    IImageFormatDetector formatDetector,
    IApplicationLogger logger) : IDdsService
{
    public bool IsAvailable => ddsEncoder.IsAvailable;

    public async Task ConvertToDdsAsync(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        await ConvertToDdsAsync(
            inputPath,
            outputPath,
            compressionOverride: null,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task ConvertToDdsAsync(
        string inputPath,
        string outputPath,
        DdsCompressionMode? compressionOverride,
        CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        var ddsSettings = CloneDdsSettings(settings.Dds);
        if (compressionOverride.HasValue)
        {
            ddsSettings.Compression = compressionOverride.Value;
        }

        using var tempDirectory = new TemporaryDirectory(logger);
        var intermediatePath = Path.Combine(tempDirectory.Path, "dds-input.png");
        await Task.Run(
            () => CreateDdsInputPng(
                inputPath,
                intermediatePath,
                settings.Quality),
            cancellationToken).ConfigureAwait(false);

        await ddsEncoder.ConvertToDdsAsync(
            intermediatePath,
            outputPath,
            ddsSettings,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task ConvertFromDdsAsync(
        string inputPath,
        string outputPath,
        ImageFormat format,
        CancellationToken cancellationToken = default)
    {
        if (format == ImageFormat.Dds)
        {
            throw new ArgumentException("DDS decoding requires a non-DDS output format.", nameof(format));
        }

        using var tempDirectory = new TemporaryDirectory(logger);
        var decodedPngPath = Path.Combine(tempDirectory.Path, "decoded.png");
        await ddsEncoder.DecodeToPngAsync(
            inputPath,
            decodedPngPath,
            cancellationToken).ConfigureAwait(false);

        var settings = await settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        await Task.Run(
            () => ConvertDecodedPng(
                decodedPngPath,
                outputPath,
                format,
                settings.Quality),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> CreatePreviewPngAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        var previewPath = Path.Combine(
            Path.GetTempPath(),
            "Pixoar",
            "Previews",
            $"{Path.GetFileNameWithoutExtension(inputPath)}-{Guid.NewGuid():N}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(previewPath)!);

        using var tempDirectory = new TemporaryDirectory(logger);
        var decodedPath = Path.Combine(tempDirectory.Path, "decoded.png");
        await ddsEncoder.DecodeToPngAsync(inputPath, decodedPath, cancellationToken).ConfigureAwait(false);

        var settings = await settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        await Task.Run(
            () => ConvertDecodedPng(
                decodedPath,
                previewPath,
                ImageFormat.Png,
                settings.Quality),
            cancellationToken).ConfigureAwait(false);
        return previewPath;
    }

    public Task<DdsImageInformation> GetInformationAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var info = DdsHeaderReader.ReadBasicInformation(inputPath, formatDetector);
        return Task.FromResult(info.Dds ?? new DdsImageInformation());
    }

    private static DdsSettings CloneDdsSettings(DdsSettings settings)
    {
        return new DdsSettings
        {
            Compression = settings.Compression,
            GenerateMipmaps = settings.GenerateMipmaps,
            PreserveAlpha = settings.PreserveAlpha
        };
    }

    private static void ConvertDecodedPng(
        string inputPath,
        string outputPath,
        ImageFormat format,
        QualitySettings qualitySettings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory);

        var outputCreated = false;
        try
        {
            using var image = new MagickImage(inputPath);
            image.AutoOrient();
            ImageColorManagement.TagSrgbSamplesWithoutTransform(image);
            ImageEncodingSettingsApplier.Apply(image, format, qualitySettings);

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
                try
                {
                    File.Delete(outputPath);
                }
                catch
                {
                    // Preserve the conversion failure.
                }
            }

            throw;
        }
    }

    private static void CreateDdsInputPng(
        string inputPath,
        string outputPath,
        QualitySettings qualitySettings)
    {
        using var image = new MagickImage(inputPath);
        image.AutoOrient();

        ImageColorManagement.NormalizeToSrgb(image);
        ImageEncodingSettingsApplier.Apply(image, ImageFormat.Png, qualitySettings);
        image.Write(outputPath);
    }
}
