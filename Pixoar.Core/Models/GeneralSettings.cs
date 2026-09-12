namespace Pixoar.Core.Models;

/// <summary>
/// Stores general application preferences.
/// </summary>
public sealed class GeneralSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether Pixoar should check GitHub releases for updates.
    /// </summary>
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>Gets or sets the default local AI upscale factor.</summary>
    public int DefaultUpscaleScale { get; set; } = 2;
}
