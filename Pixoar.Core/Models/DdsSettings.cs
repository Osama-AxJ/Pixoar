namespace Pixoar.Core.Models;

/// <summary>
/// Stores user preferences for DDS conversion.
/// </summary>
public sealed class DdsSettings
{
    /// <summary>
    /// Gets or sets the selected DDS compression mode.
    /// </summary>
    public DdsCompressionMode Compression { get; set; } = DdsCompressionMode.Dxt5;

    /// <summary>
    /// Gets or sets a value indicating whether mipmaps should be generated.
    /// </summary>
    public bool GenerateMipmaps { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether alpha data should be preserved.
    /// </summary>
    public bool PreserveAlpha { get; set; } = true;
}
