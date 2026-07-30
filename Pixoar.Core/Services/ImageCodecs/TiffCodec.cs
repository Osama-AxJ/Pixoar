using Pixoar.Core.Models;

namespace Pixoar.Core.Services.ImageCodecs;

internal sealed class TiffCodec : ImageCodecBase
{
    public TiffCodec()
        : base(ImageFormat.Tiff, "TIFF", "tiff", "tiff", "tif")
    {
    }
}
