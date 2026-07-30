using Pixoar.Core.Models;

namespace Pixoar.Core.Services.ImageCodecs;

internal sealed class BmpCodec : ImageCodecBase
{
    public BmpCodec()
        : base(ImageFormat.Bmp, "BMP", "bmp", "bmp")
    {
    }
}
