namespace Pixoar.Core.Models;

/// <summary>
/// Represents DDS-specific metadata when available.
/// </summary>
public sealed class DdsImageInformation
{
    /// <summary>
    /// Gets or sets the DDS compression type.
    /// </summary>
    public string CompressionType { get; set; } = "Unknown";

    /// <summary>
    /// Gets or sets the number of mipmaps.
    /// </summary>
    public int? MipmapCount { get; set; }
}
