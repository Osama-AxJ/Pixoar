namespace Pixoar.Core.Models;

/// <summary>
/// Describes a supported image format and its file extensions.
/// </summary>
public sealed class ImageFormatDescriptor
{
    /// <summary>
    /// Gets or sets the image format.
    /// </summary>
    public ImageFormat Format { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the primary file extension without a leading dot.
    /// </summary>
    public string PrimaryExtension { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets every supported file extension without leading dots.
    /// </summary>
    public IReadOnlyList<string> Extensions { get; set; } = [];
}
