using Pixoar.Core.Models;

namespace Pixoar.Core.Services.ImageCodecs;

internal sealed class DdsCodec : ImageCodecBase
{
    public DdsCodec()
        : base(ImageFormat.Dds, "DDS", "dds", "dds")
    {
    }
}
