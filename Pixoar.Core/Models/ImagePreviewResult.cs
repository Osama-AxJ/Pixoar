namespace Pixoar.Core.Models;

/// <summary>
/// Contains preview image data for the desktop app.
/// </summary>
public sealed class ImagePreviewResult
{
    /// <summary>
    /// Gets or sets PNG-encoded preview bytes.
    /// </summary>
    public byte[]? PngBytes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the preview is a fallback placeholder.
    /// </summary>
    public bool IsPlaceholder { get; set; }

    /// <summary>
    /// Gets or sets the preview status message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
