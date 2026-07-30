using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Pixoar.App.Models;
using Pixoar.App.ViewModels;
using Pixoar.App.Views;

namespace Pixoar.App.Services;

internal sealed class WindowService(IServiceProvider serviceProvider) : IWindowService
{
    public void ShowSettingsWindow()
    {
        var window = serviceProvider.GetRequiredService<SettingsWindow>();
        window.Owner = Application.Current.MainWindow;
        window.ShowDialog();
    }

    public async Task ShowImageInformationAsync(ImageFileItem image)
    {
        var viewModel = serviceProvider.GetRequiredService<ImageInformationViewModel>();
        await viewModel.LoadAsync(image.FilePath);

        var window = serviceProvider.GetRequiredService<ImageInformationWindow>();
        window.DataContext = viewModel;
        window.Owner = Application.Current.MainWindow;
        window.ShowDialog();
    }
}
