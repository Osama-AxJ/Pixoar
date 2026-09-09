namespace Pixoar.Core.Models;

/// <summary>
/// Describes one stored two-dimensional DDS mip surface.
/// </summary>
public sealed class DdsMipLevel
{
    /// <summary>Gets or sets the zero-based mip index.</summary>
    public int Level { get; init; }

    /// <summary>Gets or sets the stored surface width.</summary>
    public int Width { get; init; }

    /// <summary>Gets or sets the stored surface height.</summary>
    public int Height { get; init; }
}
