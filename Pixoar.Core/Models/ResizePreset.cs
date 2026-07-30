namespace Pixoar.Core.Models;

/// <summary>
/// Represents a quick resize preset.
/// </summary>
public sealed class ResizePreset
{
    /// <summary>
    /// Gets or sets the user-visible preset name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the scale percentage.
    /// </summary>
    public int? Percentage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the preset is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
