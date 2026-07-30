namespace Pixoar.Core.Models;

/// <summary>
/// Defines a single image resize request.
/// </summary>
public sealed class ImageResizeRequest : ResizeRequest
{
    /// <summary>
    /// Gets or sets the input file path.
    /// </summary>
    public string InputPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target output format. When null, the source format is used.
    /// </summary>
    public ImageFormat? OutputFormat { get; set; }

    /// <summary>
    /// Gets or sets an optional explicit output folder.
    /// </summary>
    public string? OutputFolder { get; set; }
}
