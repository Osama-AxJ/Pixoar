namespace Pixoar.Core.Models;

/// <summary>
/// Lists the supported texconv filters used when generating DDS mipmaps.
/// </summary>
public enum DdsMipmapFilter
{
    /// <summary>
    /// Uses Fant filtering, which is texconv's default mipmap filter.
    /// </summary>
    Fant = 1,

    /// <summary>
    /// Uses the linear filter.
    /// </summary>
    Linear = 2,

    /// <summary>
    /// Uses the cubic filter.
    /// </summary>
    Cubic = 3,

    /// <summary>
    /// Uses the triangle filter.
    /// </summary>
    Triangle = 4
}
