using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.Core.Services;

internal sealed class ImageFormatDetector(IEnumerable<IImageCodec> codecs) : IImageFormatDetector
{
    private readonly IReadOnlyDictionary<ImageFormat, IImageCodec> _codecsByFormat =
        codecs.ToDictionary(codec => codec.Format);

    private readonly IReadOnlyDictionary<string, IImageCodec> _codecsByExtension =
        codecs
            .SelectMany(codec => codec.Extensions.Select(extension => new { Extension = extension, Codec = codec }))
            .ToDictionary(item => item.Extension, item => item.Codec, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ImageFormatDescriptor> SupportedFormats => _codecsByFormat
        .Values
        .OrderBy(codec => codec.DisplayName)
        .Select(codec => new ImageFormatDescriptor
        {
            Format = codec.Format,
            DisplayName = codec.DisplayName,
            PrimaryExtension = codec.PrimaryExtension,
            Extensions = codec.Extensions.ToArray()
        })
        .ToArray();

    public bool IsSupported(string path)
    {
        return TryDetect(path, out _);
    }

    public ImageFormat Detect(string path)
    {
        if (TryDetect(path, out var format))
        {
            return format;
        }

        throw new NotSupportedException($"Unsupported image format: {path}");
    }

    public bool TryDetect(string path, out ImageFormat format)
    {
        var extension = Path.GetExtension(path).TrimStart('.');
        if (_codecsByExtension.TryGetValue(extension, out var codec))
        {
            format = codec.Format;
            return true;
        }

        format = default;
        return false;
    }

    public string GetPrimaryExtension(ImageFormat format)
    {
        return GetCodec(format).PrimaryExtension;
    }

    public string GetDisplayName(ImageFormat format)
    {
        return GetCodec(format).DisplayName;
    }

    private IImageCodec GetCodec(ImageFormat format)
    {
        return _codecsByFormat.TryGetValue(format, out var codec)
            ? codec
            : throw new NotSupportedException($"Unsupported image format: {format}");
    }
}
