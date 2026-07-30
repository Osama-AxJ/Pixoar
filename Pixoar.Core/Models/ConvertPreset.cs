namespace Pixoar.Core.Models;

/// <summary>
/// Represents a quick conversion preset.
/// </summary>
public sealed class ConvertPreset
{
    /// <summary>
    /// Gets or sets the user-visible preset name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target image format extension without a leading dot.
    /// </summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the preset is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
