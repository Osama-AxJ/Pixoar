using Microsoft.Win32;

namespace Pixoar.App.Services;

internal sealed class FileDialogService : IFileDialogService
{
    public IReadOnlyList<string> SelectImageFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add Images",
            Multiselect = true,
            Filter = "Supported images|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.tiff;*.tif;*.dds|All files|*.*"
        };

        return dialog.ShowDialog() == true ? dialog.FileNames : [];
    }

    public string? SelectFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Add Folder"
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
