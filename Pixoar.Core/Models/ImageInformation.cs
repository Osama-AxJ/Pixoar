namespace Pixoar.Core.Models;

/// <summary>
/// Represents extracted image file information.
/// </summary>
public sealed class ImageInformation
{
    /// <summary>
    /// Gets or sets the file name.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full file path.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file extension.
    /// </summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the formatted file size.
    /// </summary>
    public string FileSize { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the detected image format.
    /// </summary>
    public ImageFormat Format { get; set; }

    /// <summary>
    /// Gets or sets the display image format.
    /// </summary>
    public string FormatDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the image width in pixels.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the image height in pixels.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Gets or sets the formatted aspect ratio.
    /// </summary>
    public string AspectRatio { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file creation date.
    /// </summary>
    public DateTimeOffset CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets the file modification date.
    /// </summary>
    public DateTimeOffset LastModifiedDate { get; set; }

    /// <summary>
    /// Gets or sets the color depth text.
    /// </summary>
    public string ColorDepth { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the image has an alpha channel.
    /// </summary>
    public bool HasAlpha { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether transparency was detected.
    /// </summary>
    public bool HasTransparency { get; set; }

    /// <summary>
    /// Gets or sets DDS-specific information.
    /// </summary>
    public DdsImageInformation? Dds { get; set; }
}
