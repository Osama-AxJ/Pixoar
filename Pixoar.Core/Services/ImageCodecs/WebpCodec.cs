using Pixoar.Core.Models;

namespace Pixoar.Core.Services.ImageCodecs;

internal sealed class WebpCodec : ImageCodecBase
{
    public WebpCodec()
        : base(ImageFormat.Webp, "WEBP", "webp", "webp")
    {
    }
}
