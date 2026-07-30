using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.Core.Services.ImageCodecs;

internal abstract class ImageCodecBase : IImageCodec
{
    protected ImageCodecBase(
        ImageFormat format,
        string displayName,
        string primaryExtension,
        params string[] extensions)
    {
        Format = format;
        DisplayName = displayName;
        PrimaryExtension = primaryExtension;
        Extensions = extensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public ImageFormat Format { get; }

    public IReadOnlySet<string> Extensions { get; }

    public string PrimaryExtension { get; }

    public string DisplayName { get; }
}
