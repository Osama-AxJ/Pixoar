namespace Pixoar.Core.Models;

/// <summary>
/// Stores quality settings for image encoding operations.
/// </summary>
public sealed class QualitySettings
{
    /// <summary>
    /// Gets or sets JPEG quality from 1 to 100.
    /// </summary>
    public int JpegQuality { get; set; } = 90;

    /// <summary>
    /// Gets or sets WebP quality from 1 to 100.
    /// </summary>
    public int WebpQuality { get; set; } = 90;

    /// <summary>
    /// Gets or sets PNG compression level from 0 to 9.
    /// </summary>
    public int PngCompressionLevel { get; set; } = 6;
}
