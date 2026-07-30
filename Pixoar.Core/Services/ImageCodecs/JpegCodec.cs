using Pixoar.Core.Models;

namespace Pixoar.Core.Services.ImageCodecs;

internal sealed class JpegCodec : ImageCodecBase
{
    public JpegCodec()
        : base(ImageFormat.Jpeg, "JPEG", "jpg", "jpg", "jpeg")
    {
    }
}
