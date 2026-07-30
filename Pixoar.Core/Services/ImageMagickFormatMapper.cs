using ImageMagick;
using Pixoar.Core.Models;

namespace Pixoar.Core.Services;

internal static class ImageMagickFormatMapper
{
    public static MagickFormat ToMagickFormat(ImageFormat format)
    {
        return format switch
        {
            ImageFormat.Png => MagickFormat.Png,
            ImageFormat.Jpeg => MagickFormat.Jpeg,
            ImageFormat.Webp => MagickFormat.WebP,
            ImageFormat.Bmp => MagickFormat.Bmp,
            ImageFormat.Tiff => MagickFormat.Tiff,
            _ => throw new NotSupportedException($"Format {format} is not handled by Magick.NET.")
        };
    }
}
