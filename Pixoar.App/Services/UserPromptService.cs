using System.Windows;
using Pixoar.App.ViewModels;
using Pixoar.App.Views;
using Pixoar.Core.Models;

namespace Pixoar.App.Services;

internal sealed class UserPromptService : IUserPromptService
{
    public bool ConfirmOverwriteRisk()
    {
        var result = MessageBox.Show(
            "Existing output files may be overwritten. Continue?",
            "Pixoar",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return result == MessageBoxResult.Yes;
    }

    public bool ConfirmRemoveUserData()
    {
        var result = MessageBox.Show(
            "Remove Pixoar settings and logs from AppData? This cannot be undone.",
            "Pixoar",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return result == MessageBoxResult.Yes;
    }

    public void ShowAutomaticUpdateAvailable(UpdateCheckResult result)
    {
        var window = CreateUpdateWindow(result, "Remind Me Later");
        window.Show();
    }

    public void ShowManualUpdateCheckResult(UpdateCheckResult result)
    {
        if (result.Status == UpdateStatus.UpdateAvailable)
        {
            var window = CreateUpdateWindow(result, "Close");
            window.ShowDialog();
            return;
        }

        if (result.Status == UpdateStatus.UpToDate)
        {
            MessageBox.Show(
                "You're using the latest version of Pixoar.",
                "Pixoar",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        MessageBox.Show(
            "Pixoar couldn't check for updates. Please verify your internet connection and try again.",
            "Pixoar",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private static UpdateAvailableWindow CreateUpdateWindow(
        UpdateCheckResult result,
        string closeButtonText)
    {
        var window = new UpdateAvailableWindow();
        window.DataContext = new UpdateAvailableViewModel(
            result,
            closeButtonText,
            window.Close);

        if (Application.Current.MainWindow is { IsVisible: true } owner)
        {
            window.Owner = owner;
        }

        return window;
    }
}
