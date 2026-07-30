namespace Pixoar.Core.Models;

/// <summary>
/// Describes how a resize request should calculate its target dimensions.
/// </summary>
public enum ResizeMethod
{
    /// <summary>
    /// Resize from explicit width and/or height values.
    /// </summary>
    Dimensions,

    /// <summary>
    /// Resize by scaling the original dimensions by a percentage.
    /// </summary>
    Percentage
}
