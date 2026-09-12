namespace Pixoar.Core.Models;

/// <summary>
/// Stores Windows Explorer context menu preferences.
/// </summary>
public sealed class ContextMenuSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether Pixoar should register Explorer context menu entries.
    /// </summary>
    public bool EnableContextMenu { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether resize presets should appear in the context menu.
    /// </summary>
    public bool EnableResizePresets { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether convert presets should appear in the context menu.
    /// </summary>
    public bool EnableConvertPresets { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the Image Information action should appear.
    /// </summary>
    public bool EnableImageInformation { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the Open in Pixoar action should appear.
    /// </summary>
    public bool EnableOpenInPixoar { get; set; } = true;

    /// <summary>Gets or sets whether the Upscale submenu appears in the context menu.</summary>
    public bool EnableUpscale { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the DXT1 DDS compression shortcut appears in the context menu.
    /// </summary>
    public bool EnableDdsDxt1Shortcut { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the DXT3 DDS compression shortcut appears in the context menu.
    /// </summary>
    public bool EnableDdsDxt3Shortcut { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the DXT5 DDS compression shortcut appears in the context menu.
    /// </summary>
    public bool EnableDdsDxt5Shortcut { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the BC7 DDS compression shortcut appears in the context menu.
    /// </summary>
    public bool EnableDdsBc7Shortcut { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the uncompressed DDS shortcut appears in the context menu.
    /// </summary>
    public bool EnableDdsUncompressedShortcut { get; set; }
}
