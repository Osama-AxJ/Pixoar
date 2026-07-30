namespace Pixoar.Core.Models;

/// <summary>
/// Lists resize behaviors used by image operations.
/// </summary>
public enum ResizeMode
{
    /// <summary>
    /// Preserve aspect ratio and fit inside the requested bounds.
    /// </summary>
    Fit,

    /// <summary>
    /// Stretch to the requested bounds.
    /// </summary>
    Stretch,

    /// <summary>
    /// Crop to fill the requested bounds.
    /// </summary>
    Crop
}
