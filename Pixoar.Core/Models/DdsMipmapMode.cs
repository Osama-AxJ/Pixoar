namespace Pixoar.Core.Models;

/// <summary>
/// Defines how texconv determines the number of DDS mip levels.
/// </summary>
public enum DdsMipmapMode
{
    /// <summary>
    /// Generates every mip level down to 1x1.
    /// </summary>
    FullChain,

    /// <summary>
    /// Generates the configured number of mip levels.
    /// </summary>
    CustomCount
}
