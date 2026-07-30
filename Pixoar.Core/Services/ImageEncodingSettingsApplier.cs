using ImageMagick;
using Pixoar.Core.Models;

namespace Pixoar.Core.Services;

internal static class ImageEncodingSettingsApplier
{
    public static void Apply(MagickImage image, ImageFormat outputFormat, QualitySettings qualitySettings)
    {
        image.Format = ImageMagickFormatMapper.ToMagickFormat(outputFormat);

        switch (outputFormat)
        {
            case ImageFormat.Jpeg:
                image.Quality = ClampQuality(qualitySettings.JpegQuality);
                image.BackgroundColor = MagickColors.White;
                image.Alpha(AlphaOption.Remove);
                break;
            case ImageFormat.Webp:
                image.Quality = ClampQuality(qualitySettings.WebpQuality);
                break;
            case ImageFormat.Png:
                image.Settings.SetDefine(MagickFormat.Png, "compression-level", ClampPngCompression(qualitySettings.PngCompressionLevel).ToString());
                break;
        }
    }

    private static uint ClampQuality(int value)
    {
        return (uint)Math.Clamp(value, 1, 100);
    }

    private static int ClampPngCompression(int value)
    {
        return Math.Clamp(value, 0, 9);
    }
}
