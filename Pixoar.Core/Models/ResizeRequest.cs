namespace Pixoar.Core.Models;

/// <summary>
/// Defines shared resize options used by the desktop app, CLI, and shell integration.
/// </summary>
public class ResizeRequest
{
    /// <summary>
    /// Gets or sets the resize calculation method.
    /// </summary>
    public ResizeMethod ResizeMethod { get; set; } = ResizeMethod.Dimensions;

    /// <summary>
    /// Gets or sets the requested width in pixels for dimension-based resize.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Gets or sets the requested height in pixels for dimension-based resize.
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Gets or sets the requested scale percentage for percentage-based resize.
    /// </summary>
    public int? Percentage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether aspect ratio should be preserved.
    /// </summary>
    public bool KeepAspectRatio { get; set; } = true;

    /// <summary>
    /// Gets or sets the resize behavior used for dimension-based resize.
    /// </summary>
    public ResizeMode Mode { get; set; } = ResizeMode.Fit;
}
