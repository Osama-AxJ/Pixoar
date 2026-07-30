using Pixoar.App.Models;

namespace Pixoar.App.Services;

/// <summary>
/// Opens application windows and dialogs for view models.
/// </summary>
public interface IWindowService
{
    /// <summary>
    /// Opens the settings window.
    /// </summary>
    void ShowSettingsWindow();

    /// <summary>
    /// Opens the image information dialog for the supplied image.
    /// </summary>
    /// <param name="image">The image entry to inspect.</param>
    /// <returns>A task that completes when the dialog has been prepared.</returns>
    Task ShowImageInformationAsync(ImageFileItem image);
}
