using System.Text;
using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.Core.Services;

internal static class DdsHeaderReader
{
    private const uint DdsDepthFlag = 0x00800000;
    private const uint DdsCaps2CubeMapMask = 0x0000FE00;
    private const uint DdsCaps2Volume = 0x00200000;
    private const uint Dx10FourCc = 0x30315844;
    private const uint Dx10TextureCube = 0x00000004;
    private const uint ResourceDimensionTexture2D = 3;

    public static ImageInformation ReadBasicInformation(
        string path,
        IImageFormatDetector formatDetector)
    {
        var header = ReadHeader(path);
        var file = new FileInfo(path);
        var compression = GetCompressionType(header);

        return new ImageInformation
        {
            FileName = file.Name,
            FilePath = file.FullName,
            Extension = file.Extension,
            FileSizeBytes = file.Length,
            FileSize = FormatFileSize(file.Length),
            Format = ImageFormat.Dds,
            FormatDisplayName = formatDetector.GetDisplayName(ImageFormat.Dds),
            Width = header.Width,
            Height = header.Height,
            AspectRatio = FormatAspectRatio(header.Width, header.Height),
            CreatedDate = file.CreationTime,
            LastModifiedDate = file.LastWriteTime,
            ColorDepth = "DDS texture",
            HasAlpha = CompressionMayContainAlpha(compression),
            HasTransparency = CompressionMayContainAlpha(compression),
            Dds = new DdsImageInformation
            {
                CompressionType = compression,
                MipmapCount = header.MipmapCount == 0 ? 1 : (int)header.MipmapCount
            }
        };
    }

    internal static DdsHeaderMetadata ValidateStandardTexture2D(string path)
    {
        var header = ReadHeader(path);
        var invalidReasons = new List<string>();

        if ((header.Flags & DdsDepthFlag) != 0)
        {
            invalidReasons.Add("DDSD_DEPTH is set");
        }

        if ((header.Caps2 & DdsCaps2Volume) != 0)
        {
            invalidReasons.Add("DDSCAPS2_VOLUME is set");
        }

        if ((header.Caps2 & DdsCaps2CubeMapMask) != 0)
        {
            invalidReasons.Add("cubemap flags are set");
        }

        if (header.HasDx10Header)
        {
            if (header.ResourceDimension != ResourceDimensionTexture2D)
            {
                invalidReasons.Add(
                    $"DX10 resource dimension is {header.ResourceDimension?.ToString() ?? "missing"}, not Texture2D");
            }

            if (header.ArraySize != 1)
            {
                invalidReasons.Add(
                    $"DX10 array size is {header.ArraySize?.ToString() ?? "missing"}, not 1");
            }

            if ((header.MiscFlag.GetValueOrDefault() & Dx10TextureCube) != 0)
            {
                invalidReasons.Add("DX10 texture-cube flag is set");
            }
        }

        if (invalidReasons.Count > 0)
        {
            throw new InvalidDataException(
                $"texconv created a DDS that is not an ordinary Texture2D: {string.Join("; ", invalidReasons)}.");
        }

        return header;
    }

    internal static DdsHeaderMetadata ReadHeader(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream, Encoding.ASCII);

        if (stream.Length < 128 || reader.ReadUInt32() != 0x20534444)
        {
            throw new InvalidDataException("The file is not a valid DDS file.");
        }

        if (reader.ReadUInt32() != 124)
        {
            throw new InvalidDataException("The DDS header is invalid.");
        }

        var flags = reader.ReadUInt32();
        var height = reader.ReadInt32();
        var width = reader.ReadInt32();
        var pitchOrLinearSize = reader.ReadUInt32();
        var depth = reader.ReadUInt32();
        var mipMapCount = reader.ReadUInt32();

        stream.Position = 76;
        if (reader.ReadUInt32() != 32)
        {
            throw new InvalidDataException("The DDS pixel-format header is invalid.");
        }

        var pixelFormatFlags = reader.ReadUInt32();
        var fourCc = reader.ReadUInt32();
        var rgbBitCount = reader.ReadUInt32();
        var redMask = reader.ReadUInt32();
        var greenMask = reader.ReadUInt32();
        var blueMask = reader.ReadUInt32();
        var alphaMask = reader.ReadUInt32();
        var caps = reader.ReadUInt32();
        var caps2 = reader.ReadUInt32();
        var caps3 = reader.ReadUInt32();
        var caps4 = reader.ReadUInt32();
        _ = reader.ReadUInt32();

        uint? dxgiFormat = null;
        uint? resourceDimension = null;
        uint? miscFlag = null;
        uint? arraySize = null;
        uint? miscFlags2 = null;

        if (fourCc == Dx10FourCc)
        {
            if (stream.Length < 148)
            {
                throw new InvalidDataException("The DDS DX10 header is truncated.");
            }

            dxgiFormat = reader.ReadUInt32();
            resourceDimension = reader.ReadUInt32();
            miscFlag = reader.ReadUInt32();
            arraySize = reader.ReadUInt32();
            miscFlags2 = reader.ReadUInt32();
        }

        return new DdsHeaderMetadata(
            flags,
            height,
            width,
            pitchOrLinearSize,
            depth,
            mipMapCount,
            pixelFormatFlags,
            fourCc,
            rgbBitCount,
            redMask,
            greenMask,
            blueMask,
            alphaMask,
            caps,
            caps2,
            caps3,
            caps4,
            dxgiFormat,
            resourceDimension,
            miscFlag,
            arraySize,
            miscFlags2);
    }

    private static string GetCompressionType(DdsHeaderMetadata header)
    {
        if (!header.HasDx10Header)
        {
            return header.FourCc == 0 ? "Uncompressed" : header.FourCcDisplay;
        }

        return header.DxgiFormat switch
        {
            28 => "R8G8B8A8_UNORM",
            29 => "R8G8B8A8_UNORM_SRGB",
            71 => "BC1_UNORM",
            72 => "BC1_UNORM_SRGB",
            74 => "BC2_UNORM",
            75 => "BC2_UNORM_SRGB",
            77 => "BC3_UNORM",
            78 => "BC3_UNORM_SRGB",
            98 => "BC7_UNORM",
            99 => "BC7_UNORM_SRGB",
            null => "DX10 (missing format)",
            _ => $"DXGI {header.DxgiFormat}"
        };
    }

    private static bool CompressionMayContainAlpha(string compression)
    {
        return compression.Contains("DXT3", StringComparison.OrdinalIgnoreCase)
            || compression.Contains("DXT5", StringComparison.OrdinalIgnoreCase)
            || compression.Contains("BC2", StringComparison.OrdinalIgnoreCase)
            || compression.Contains("BC3", StringComparison.OrdinalIgnoreCase)
            || compression.Contains("BC7", StringComparison.OrdinalIgnoreCase)
            || compression.Contains("Uncompressed", StringComparison.OrdinalIgnoreCase)
            || compression.Contains("R8G8B8A8", StringComparison.OrdinalIgnoreCase);
    }

    public static string FormatAspectRatio(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return "Unknown";
        }

        var divisor = GreatestCommonDivisor(width, height);
        return $"{width / divisor}:{height / divisor}";
    }

    public static string FormatFileSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB"];
        var size = (double)bytes;
        var suffixIndex = 0;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return $"{size:0.#} {suffixes[suffixIndex]}";
    }

    private static int GreatestCommonDivisor(int left, int right)
    {
        left = Math.Abs(left);
        right = Math.Abs(right);

        while (right != 0)
        {
            var remainder = left % right;
            left = right;
            right = remainder;
        }

        return left == 0 ? 1 : left;
    }
}

internal readonly record struct DdsHeaderMetadata(
    uint Flags,
    int Height,
    int Width,
    uint PitchOrLinearSize,
    uint Depth,
    uint MipmapCount,
    uint PixelFormatFlags,
    uint FourCc,
    uint RgbBitCount,
    uint RedMask,
    uint GreenMask,
    uint BlueMask,
    uint AlphaMask,
    uint Caps,
    uint Caps2,
    uint Caps3,
    uint Caps4,
    uint? DxgiFormat,
    uint? ResourceDimension,
    uint? MiscFlag,
    uint? ArraySize,
    uint? MiscFlags2)
{
    public bool HasDx10Header => FourCc == 0x30315844;

    public string FourCcDisplay
    {
        get
        {
            if (FourCc == 0)
            {
                return "none";
            }

            var text = Encoding.ASCII.GetString(BitConverter.GetBytes(FourCc)).TrimEnd('\0', ' ');
            return string.IsNullOrWhiteSpace(text) ? $"0x{FourCc:X8}" : text;
        }
    }
}
