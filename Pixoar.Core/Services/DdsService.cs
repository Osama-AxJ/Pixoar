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
    private readonly object _mipCacheLock = new();
    private DdsMipPreviewCache? _mipPreviewCache;
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

    public async Task<IReadOnlyList<DdsMipLevel>> GetMipLevelsAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        var chain = await GetMipChainAsync(inputPath, cancellationToken).ConfigureAwait(false);
        return chain.Surfaces
            .Select(surface => new DdsMipLevel
            {
                Level = surface.Level,
                Width = surface.Width,
                Height = surface.Height
            })
            .ToArray();
    }

    public async Task<ImagePreviewResult> LoadMipPreviewAsync(
        string inputPath,
        int mipLevel,
        int maxPixelSize,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return new ImagePreviewResult
            {
                IsPlaceholder = true,
                Message = "DDS preview requires bundled texconv.exe."
            };
        }

        try
        {
            var chain = await GetMipChainAsync(inputPath, cancellationToken).ConfigureAwait(false);
            lock (_mipCacheLock)
            {
                if (_mipPreviewCache?.Path == Path.GetFullPath(inputPath) &&
                    _mipPreviewCache.PreviewBytes.TryGetValue(mipLevel, out var cachedBytes))
                {
                    return new ImagePreviewResult { PngBytes = cachedBytes, Message = "DDS mip preview loaded." };
                }
            }

            using var tempDirectory = new TemporaryDirectory(logger);
            var mipDdsPath = Path.Combine(tempDirectory.Path, $"mip-{mipLevel}.dds");
            var decodedPngPath = Path.Combine(tempDirectory.Path, $"mip-{mipLevel}.png");
            await File.WriteAllBytesAsync(
                mipDdsPath,
                DdsMipSurfaceReader.CreateSingleMipDds(chain, mipLevel),
                cancellationToken).ConfigureAwait(false);
            await ddsEncoder.DecodeToPngAsync(mipDdsPath, decodedPngPath, cancellationToken).ConfigureAwait(false);
            var bytes = await Task.Run(
                () => CreatePreviewBytes(decodedPngPath, maxPixelSize),
                cancellationToken).ConfigureAwait(false);

            lock (_mipCacheLock)
            {
                if (_mipPreviewCache?.Path == Path.GetFullPath(inputPath))
                {
                    _mipPreviewCache.PreviewBytes[mipLevel] = bytes;
                }
            }

            return new ImagePreviewResult { PngBytes = bytes, Message = "DDS mip preview loaded." };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await logger.LogErrorAsync($"DDS mip preview failed. Input: {inputPath}. Mip: {mipLevel}. Error: {ex.Message}", ex, cancellationToken).ConfigureAwait(false);
            return new ImagePreviewResult { IsPlaceholder = true, Message = UserFacingErrorMessage.ForImageLoad(ex) };
        }
    }

    private async Task<DdsMipChain> GetMipChainAsync(string inputPath, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(inputPath);
        var file = new FileInfo(fullPath);
        lock (_mipCacheLock)
        {
            if (_mipPreviewCache is { } cached &&
                cached.Path == fullPath &&
                cached.Length == file.Length &&
                cached.LastWriteTimeUtc == file.LastWriteTimeUtc)
            {
                return cached.Chain;
            }
        }

        var chain = await Task.Run(() => DdsMipSurfaceReader.Read(fullPath), cancellationToken).ConfigureAwait(false);
        lock (_mipCacheLock)
        {
            _mipPreviewCache = new DdsMipPreviewCache(fullPath, file.Length, file.LastWriteTimeUtc, chain);
        }

        return chain;
    }

    private static byte[] CreatePreviewBytes(string path, int maxPixelSize)
    {
        using var image = new MagickImage(path);
        image.AutoOrient();
        // DDS samples decoded by texconv are already the stored color values.
        // In particular, legacy DDS headers do not carry a transfer-function
        // flag. Transforming Magick's inferred RGB color space here changes
        // those values and makes the mip preview appear washed out.
        ImageColorManagement.TagSrgbSamplesWithoutTransform(image);
        if (image.Width > (uint)maxPixelSize || image.Height > (uint)maxPixelSize)
        {
            image.Thumbnail(new MagickGeometry((uint)maxPixelSize, (uint)maxPixelSize));
        }
        image.Format = MagickFormat.Png;
        return image.ToByteArray();
    }

    private static DdsSettings CloneDdsSettings(DdsSettings settings)
    {
        return new DdsSettings
        {
            Compression = settings.Compression,
            GenerateMipmaps = settings.GenerateMipmaps,
            MipmapMode = settings.MipmapMode,
            CustomMipCount = settings.CustomMipCount,
            SmallestMipSize = settings.SmallestMipSize,
            MipmapFilter = settings.MipmapFilter,
            PreserveAlpha = settings.PreserveAlpha
        };
    }

    private sealed class DdsMipPreviewCache(
        string path,
        long length,
        DateTime lastWriteTimeUtc,
        DdsMipChain chain)
    {
        public string Path { get; } = path;
        public long Length { get; } = length;
        public DateTime LastWriteTimeUtc { get; } = lastWriteTimeUtc;
        public DdsMipChain Chain { get; } = chain;
        public Dictionary<int, byte[]> PreviewBytes { get; } = [];
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
