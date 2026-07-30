using ImageMagick;
using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.Core.Services;

internal sealed class ImagePreviewService(
    IImageFormatDetector formatDetector,
    IDdsService ddsService,
    IApplicationLogger logger) : IImagePreviewService
{
    public async Task<ImagePreviewResult> LoadPreviewAsync(
        string path,
        int maxPixelSize,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var format = formatDetector.Detect(path);
            if (format == ImageFormat.Dds)
            {
                return await LoadDdsPreviewAsync(path, maxPixelSize, cancellationToken).ConfigureAwait(false);
            }

            var bytes = await Task.Run(
                () => CreatePreviewBytes(path, maxPixelSize),
                cancellationToken).ConfigureAwait(false);

            return new ImagePreviewResult
            {
                PngBytes = bytes,
                Message = "Preview loaded."
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await logger.LogErrorAsync($"Preview failed. Input: {path}. Error: {ex.Message}", ex, cancellationToken).ConfigureAwait(false);
            return new ImagePreviewResult
            {
                IsPlaceholder = true,
                Message = UserFacingErrorMessage.ForImageLoad(ex)
            };
        }
    }

    private async Task<ImagePreviewResult> LoadDdsPreviewAsync(
        string path,
        int maxPixelSize,
        CancellationToken cancellationToken)
    {
        if (!ddsService.IsAvailable)
        {
            return new ImagePreviewResult
            {
                IsPlaceholder = true,
                Message = "DDS preview requires bundled texconv.exe."
            };
        }

        var previewPath = await ddsService.CreatePreviewPngAsync(path, cancellationToken).ConfigureAwait(false);
        try
        {
            var bytes = await Task.Run(
                () => CreatePreviewBytes(previewPath, maxPixelSize),
                cancellationToken).ConfigureAwait(false);

            return new ImagePreviewResult
            {
                PngBytes = bytes,
                Message = "DDS preview loaded."
            };
        }
        finally
        {
            if (File.Exists(previewPath))
            {
                File.Delete(previewPath);
            }
        }
    }

    private static byte[] CreatePreviewBytes(string path, int maxPixelSize)
    {
        using var image = new MagickImage(path);
        image.AutoOrient();
        ImageColorManagement.NormalizeToSrgb(image);
        image.Thumbnail(new MagickGeometry((uint)maxPixelSize, (uint)maxPixelSize));
        image.Format = MagickFormat.Png;
        return image.ToByteArray();
    }
}
