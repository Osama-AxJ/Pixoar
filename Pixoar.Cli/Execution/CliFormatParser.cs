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

    public static bool TryParseDdsCompression(string value, out DdsCompressionMode compression)
    {
        switch (value.Trim().ToUpperInvariant())
        {
            case "DXT1":
                compression = DdsCompressionMode.Dxt1;
                return true;
            case "DXT3":
                compression = DdsCompressionMode.Dxt3;
                return true;
            case "DXT5":
                compression = DdsCompressionMode.Dxt5;
                return true;
            case "BC7":
                compression = DdsCompressionMode.Bc7;
                return true;
            case "UNCOMPRESSED":
                compression = DdsCompressionMode.Uncompressed;
                return true;
            default:
                compression = default;
                return false;
        }
    }

    public static bool TryParseExplorerDdsFormat(
        string value,
        out DdsCompressionMode compression)
    {
        const string prefix = "dds-";
        var normalized = value.Trim();
        if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return TryParseDdsCompression(normalized[prefix.Length..], out compression);
        }

        compression = default;
        return false;
    }
}
