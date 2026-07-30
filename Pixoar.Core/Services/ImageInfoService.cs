using ImageMagick;
using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.Core.Services;

internal sealed class ImageInfoService(
    IImageFormatDetector formatDetector,
    IApplicationLogger logger) : IImageInfoService
{
    public async Task<ImageInformation> GetInformationAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var format = formatDetector.Detect(path);
            if (format == ImageFormat.Dds)
            {
                return DdsHeaderReader.ReadBasicInformation(path, formatDetector);
            }

            return await Task.Run(
                () => ReadStandardInformation(path, format),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await logger.LogErrorAsync($"Image information failed. Input: {path}. Error: {ex.Message}", ex, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(UserFacingErrorMessage.ForImageLoad(ex), ex);
        }
    }

    private ImageInformation ReadStandardInformation(string path, ImageFormat format)
    {
        var file = new FileInfo(path);
        var magickInfo = new MagickImageInfo(path);

        using var image = new MagickImage(path);
        var hasAlpha = image.HasAlpha;
        var hasTransparency = hasAlpha && image.IsOpaque == false;

        return new ImageInformation
        {
            FileName = file.Name,
            FilePath = file.FullName,
            Extension = file.Extension,
            FileSizeBytes = file.Length,
            FileSize = DdsHeaderReader.FormatFileSize(file.Length),
            Format = format,
            FormatDisplayName = formatDetector.GetDisplayName(format),
            Width = (int)magickInfo.Width,
            Height = (int)magickInfo.Height,
            AspectRatio = DdsHeaderReader.FormatAspectRatio((int)magickInfo.Width, (int)magickInfo.Height),
            CreatedDate = file.CreationTime,
            LastModifiedDate = file.LastWriteTime,
            ColorDepth = $"{image.Depth}-bit",
            HasAlpha = hasAlpha,
            HasTransparency = hasTransparency
        };
    }
}
