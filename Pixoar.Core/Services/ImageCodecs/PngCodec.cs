using Pixoar.Core.Models;

namespace Pixoar.Core.Services.ImageCodecs;

internal sealed class PngCodec : ImageCodecBase
{
    public PngCodec()
        : base(ImageFormat.Png, "PNG", "png", "png")
    {
    }
}
