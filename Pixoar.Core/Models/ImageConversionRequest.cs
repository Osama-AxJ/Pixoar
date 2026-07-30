namespace Pixoar.Core.Models;

/// <summary>
/// Defines a single image conversion request.
/// </summary>
public sealed class ImageConversionRequest
{
    /// <summary>
    /// Gets or sets the input file path.
    /// </summary>
    public string InputPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target image format.
    /// </summary>
    public ImageFormat OutputFormat { get; set; }

    /// <summary>
    /// Gets or sets an optional explicit output folder.
    /// </summary>
    public string? OutputFolder { get; set; }
}
