namespace Pixoar.App.Services;

/// <summary>
/// Provides file and folder selection dialogs for view models.
/// </summary>
public interface IFileDialogService
{
    /// <summary>
    /// Shows an image file picker.
    /// </summary>
    /// <returns>The selected file paths, or an empty sequence when canceled.</returns>
    IReadOnlyList<string> SelectImageFiles();

    /// <summary>
    /// Shows a folder picker.
    /// </summary>
    /// <returns>The selected folder path, or null when canceled.</returns>
    string? SelectFolder();
}
