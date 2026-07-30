namespace Pixoar.Core.Models;

/// <summary>
/// Represents the complete persisted Pixoar user settings document.
/// </summary>
public sealed class PixoarSettings
{
    /// <summary>
    /// Gets or sets the settings schema version.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// Gets or sets general application settings.
    /// </summary>
    public GeneralSettings General { get; set; } = new();

    /// <summary>
    /// Gets or sets DDS-related settings.
    /// </summary>
    public DdsSettings Dds { get; set; } = new();

    /// <summary>
    /// Gets or sets standard image quality settings.
    /// </summary>
    public QualitySettings Quality { get; set; } = new();

    /// <summary>
    /// Gets or sets output location and overwrite settings.
    /// </summary>
    public OutputSettings Output { get; set; } = new();

    /// <summary>
    /// Gets or sets resize quick action presets.
    /// </summary>
    public List<ResizePreset> ResizePresets { get; set; } =
    [
        new ResizePreset { Name = "50%", Percentage = 50 },
        new ResizePreset { Name = "75%", Percentage = 75 }
    ];

    /// <summary>
    /// Gets or sets conversion quick action presets.
    /// </summary>
    public List<ConvertPreset> ConvertPresets { get; set; } =
    [
        new ConvertPreset { Name = "PNG", Format = "png" },
        new ConvertPreset { Name = "JPG", Format = "jpg" },
        new ConvertPreset { Name = "WEBP", Format = "webp" },
        new ConvertPreset { Name = "DDS", Format = "dds" }
    ];

    /// <summary>
    /// Gets or sets Windows context menu settings.
    /// </summary>
    public ContextMenuSettings ContextMenu { get; set; } = new();
}
