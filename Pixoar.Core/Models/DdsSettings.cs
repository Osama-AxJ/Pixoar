using System.Text.Json.Serialization;

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
    /// Gets or sets whether mipmaps use the complete chain or a custom count.
    /// </summary>
    public DdsMipmapMode MipmapMode { get; set; } = DdsMipmapMode.FullChain;

    /// <summary>
    /// Gets or sets the number of mip levels when <see cref="MipmapMode"/> is custom.
    /// </summary>
    public int CustomMipCount { get; set; } = 1;

    /// <summary>
    /// Gets or sets the target smallest mip dimension in pixels. A null value
    /// retains a legacy <see cref="CustomMipCount"/> setting when present.
    /// </summary>
    public int? SmallestMipSize { get; set; }

    /// <summary>
    /// Gets or sets the filter used while generating mipmaps.
    /// </summary>
    [JsonConverter(typeof(DdsMipmapFilterJsonConverter))]
    public DdsMipmapFilter MipmapFilter { get; set; } = DdsMipmapFilter.Fant;

    /// <summary>
    /// Gets or sets a value indicating whether alpha data should be preserved.
    /// </summary>
    public bool PreserveAlpha { get; set; } = true;
}
