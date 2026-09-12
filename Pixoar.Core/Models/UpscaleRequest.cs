namespace Pixoar.Core.Models;

/// <summary>
/// Defines one local Real-ESRGAN image-upscaling operation.
/// </summary>
public sealed class UpscaleRequest
{
    /// <summary>Gets or sets the source image path.</summary>
    public string InputPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the supported output scale factor.</summary>
    public int Scale { get; set; } = 2;

    /// <summary>Gets or sets an optional explicit output folder.</summary>
    public string? OutputFolder { get; set; }
}
