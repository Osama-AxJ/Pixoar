using Pixoar.Core.Models;

namespace Pixoar.Cli.Execution;

internal static class CliFormatParser
{
    public static bool TryParseImageFormat(string value, out ImageFormat format)
    {
        switch (value.Trim().ToUpperInvariant())
        {
            case "PNG":
                format = ImageFormat.Png;
                return true;
            case "JPG":
            case "JPEG":
                format = ImageFormat.Jpeg;
                return true;
            case "WEBP":
                format = ImageFormat.Webp;
                return true;
            case "BMP":
                format = ImageFormat.Bmp;
                return true;
            case "TIFF":
            case "TIF":
                format = ImageFormat.Tiff;
                return true;
            case "DDS":
                format = ImageFormat.Dds;
                return true;
            default:
                format = default;
                return false;
        }
    }

    public static bool TryParseResizeMode(string value, out ResizeMode mode)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "stretch":
                mode = ResizeMode.Stretch;
                return true;
            case "crop":
                mode = ResizeMode.Crop;
                return true;
            case "fit":
                mode = ResizeMode.Fit;
                return true;
            default:
                mode = default;
                return false;
        }
    }
}
