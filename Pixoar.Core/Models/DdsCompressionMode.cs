namespace Pixoar.Core.Models;

/// <summary>
/// Lists DDS compression modes supported by planned DDS conversion workflows.
/// </summary>
public enum DdsCompressionMode
{
    /// <summary>
    /// DXT1 compression.
    /// </summary>
    Dxt1,

    /// <summary>
    /// DXT3 compression.
    /// </summary>
    Dxt3,

    /// <summary>
    /// DXT5 compression.
    /// </summary>
    Dxt5,

    /// <summary>
    /// BC7 compression.
    /// </summary>
    Bc7,

    /// <summary>
    /// Uncompressed DDS output.
    /// </summary>
    Uncompressed
}
