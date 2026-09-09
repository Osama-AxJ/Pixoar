using System.Windows.Media;
using Pixoar.App.Models;

namespace Pixoar.App.Services;

/// <summary>
/// Opens application windows and dialogs for view models.
/// </summary>
public interface IWindowService
{
    /// <summary>
    /// Opens the settings window using the supplied image context for contextual controls.
    /// </summary>
    /// <param name="images">The current source images, if available.</param>
    void ShowSettingsWindow(IReadOnlyCollection<ImageFileItem> images);

    /// <summary>
    /// Opens the image information dialog for the supplied image.
    /// </summary>
    /// <param name="image">The image entry to inspect.</param>
    /// <returns>A task that completes when the dialog has been prepared.</returns>
    Task ShowImageInformationAsync(ImageFileItem image);

    /// <summary>
    /// Opens a zoomable image inspection window.
    /// </summary>
    void ShowImagePreview(
        ImageSource image,
        string title,
        string resolution,
        BitmapScalingMode scalingMode);
}
