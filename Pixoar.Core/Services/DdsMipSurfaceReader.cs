using System.Buffers.Binary;

namespace Pixoar.Core.Services;

/// <summary>
/// Locates stored mip surfaces in ordinary 2D DDS files and makes each one
/// independently decodable without resampling any pixel data.
/// </summary>
internal static class DdsMipSurfaceReader
{
    private const uint DdsdMipMapCount = 0x00020000;
    private const uint DdsdPitch = 0x00000008;
    private const uint DdsdLinearSize = 0x00080000;
    private const uint DdsCapsComplex = 0x00000008;
    private const uint DdsCapsTexture = 0x00001000;
    private const uint DdsCapsMipMap = 0x00400000;

    public static DdsMipChain Read(string path)
    {
        var header = DdsHeaderReader.ReadHeader(path);
        ValidateTexture(header);
        if (header.Width < 1 || header.Height < 1)
        {
            throw new InvalidDataException("The DDS has invalid dimensions.");
        }

        var bytes = File.ReadAllBytes(path);
        var headerLength = header.HasDx10Header ? 148 : 128;
        if (bytes.Length < headerLength)
        {
            throw new InvalidDataException("The DDS header is truncated.");
        }

        var requestedMipCount = header.MipmapCount == 0 ? 1 : checked((int)header.MipmapCount);
        var surfaces = new List<DdsMipSurface>(requestedMipCount);
        var offset = headerLength;
        var width = header.Width;
        var height = header.Height;
        for (var level = 0; level < requestedMipCount; level++)
        {
            var length = GetSurfaceLength(header, width, height);
            if (offset > bytes.Length - length)
            {
                throw new InvalidDataException($"The DDS data for mip {level} is truncated.");
            }

            surfaces.Add(new DdsMipSurface(level, width, height, offset, length));
            offset += length;
            width = Math.Max(1, width / 2);
            height = Math.Max(1, height / 2);
        }

        return new DdsMipChain(header, bytes, headerLength, surfaces);
    }

    public static byte[] CreateSingleMipDds(DdsMipChain chain, int mipLevel)
    {
        var surface = chain.Surfaces.FirstOrDefault(candidate => candidate.Level == mipLevel);
        if (surface == default)
        {
            throw new ArgumentOutOfRangeException(nameof(mipLevel), "The requested DDS mip does not exist.");
        }

        var output = new byte[chain.HeaderLength + surface.Length];
        Buffer.BlockCopy(chain.Bytes, 0, output, 0, chain.HeaderLength);
        Buffer.BlockCopy(chain.Bytes, surface.Offset, output, chain.HeaderLength, surface.Length);

        // Convert the copied header into a valid one-surface DDS. The pixel
        // format (including DX10 metadata) is preserved exactly.
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(12), surface.Height);
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(16), surface.Width);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(20), checked((uint)GetPitchOrLinearSize(chain.Header, surface)));
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(28), 1);
        var flags = BinaryPrimitives.ReadUInt32LittleEndian(output.AsSpan(8));
        flags &= ~DdsdMipMapCount;
        flags = GetBytesPerBlock(chain.Header) is null
            ? (flags | DdsdPitch) & ~DdsdLinearSize
            : (flags | DdsdLinearSize) & ~DdsdPitch;
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(8), flags);
        var caps = BinaryPrimitives.ReadUInt32LittleEndian(output.AsSpan(108));
        BinaryPrimitives.WriteUInt32LittleEndian(
            output.AsSpan(108),
            (caps & ~(DdsCapsComplex | DdsCapsMipMap)) | DdsCapsTexture);
        return output;
    }

    private static void ValidateTexture(DdsHeaderMetadata header)
    {
        if (header.Depth > 1 || (header.Flags & 0x00800000) != 0 || (header.Caps2 & 0x00200000) != 0)
        {
            throw new NotSupportedException("DDS mip preview supports only 2D textures.");
        }

        if ((header.Caps2 & 0x0000FE00) != 0 || (header.HasDx10Header && (header.MiscFlag.GetValueOrDefault() & 0x4) != 0))
        {
            throw new NotSupportedException("DDS mip preview does not support cubemap textures.");
        }

        if (header.HasDx10Header && (header.ResourceDimension != 3 || header.ArraySize != 1))
        {
            throw new NotSupportedException("DDS mip preview supports only single 2D texture resources.");
        }
    }

    private static int GetSurfaceLength(DdsHeaderMetadata header, int width, int height)
    {
        var bytesPerBlock = GetBytesPerBlock(header);
        if (bytesPerBlock is not null)
        {
            return checked(Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * bytesPerBlock.Value);
        }

        if (header.RgbBitCount == 0 || header.RgbBitCount % 8 != 0)
        {
            throw new NotSupportedException("The DDS uncompressed pixel layout is not byte-aligned.");
        }

        return checked(width * height * checked((int)(header.RgbBitCount / 8)));
    }

    private static int GetPitchOrLinearSize(DdsHeaderMetadata header, DdsMipSurface surface)
    {
        var bytesPerBlock = GetBytesPerBlock(header);
        if ((header.Flags & DdsdLinearSize) != 0 && bytesPerBlock is not null)
        {
            return surface.Length;
        }

        if ((header.Flags & DdsdPitch) != 0 || bytesPerBlock is null)
        {
            return checked(surface.Width * checked((int)(header.RgbBitCount / 8)));
        }

        return surface.Length;
    }

    private static int? GetBytesPerBlock(DdsHeaderMetadata header)
    {
        if (!header.HasDx10Header)
        {
            return header.FourCc switch
            {
                0x31545844 => 8,  // DXT1
                0x33545844 or 0x32545844 or 0x35545844 or 0x34545844 => 16, // DXT2-5
                0 => null,
                _ => throw new NotSupportedException($"DDS compression {header.FourCcDisplay} is not supported for mip preview.")
            };
        }

        return header.DxgiFormat switch
        {
            71 or 72 => 8, // BC1
            74 or 75 or 77 or 78 or 98 or 99 => 16, // BC2, BC3, BC7
            28 or 29 => null, // R8G8B8A8
            _ => throw new NotSupportedException($"DXGI format {header.DxgiFormat} is not supported for DDS mip preview.")
        };
    }
}

internal sealed record DdsMipChain(
    DdsHeaderMetadata Header,
    byte[] Bytes,
    int HeaderLength,
    IReadOnlyList<DdsMipSurface> Surfaces);

internal readonly record struct DdsMipSurface(int Level, int Width, int Height, int Offset, int Length);
