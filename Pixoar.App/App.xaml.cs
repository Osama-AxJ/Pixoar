using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Pixoar.App.Services;
using Pixoar.App.ViewModels;
using Pixoar.App.Views;
using Pixoar.Core.Configuration;
using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.App;

/// <summary>
/// Provides the WPF application entry point and composition root.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private bool _automaticUpdateCheckStarted;

    /// <summary>
    /// Builds the dependency graph, initializes core services, and shows the main window.
    /// </summary>
    /// <param name="e">Startup event arguments.</param>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (ExplorerBatchDispatcher.IsRequest(e.Args))
        {
            var exitCode = await ExplorerBatchDispatcher.DispatchAsync(e.Args);
            Shutdown(exitCode);
            return;
        }

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var logger = _serviceProvider.GetRequiredService<IApplicationLogger>();

        try
        {
            await _serviceProvider.GetRequiredService<ISettingsService>().LoadAsync();
            await logger.LogInformationAsync("Pixoar desktop application started.");

            if (await TryHandleLaunchArgumentsAsync(e.Args, logger))
            {
                return;
            }

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();
            StartAutomaticUpdateCheck(logger);
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync("Pixoar desktop application failed to start.", ex);
            MessageBox.Show(
                "Pixoar could not start. Check the log file for details.",
                "Pixoar",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(1);
        }
    }

    /// <summary>
    /// Releases services when the application exits.
    /// </summary>
    /// <param name="e">Exit event arguments.</param>
    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddPixoarCore();
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<IWindowService, WindowService>();
        services.AddSingleton<IUserPromptService, UserPromptService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SettingsWindow>();
        services.AddTransient<ImageInformationViewModel>();
        services.AddTransient<ImageInformationWindow>();
    }

    private async Task<bool> TryHandleLaunchArgumentsAsync(string[] args, IApplicationLogger logger)
    {
        if (_serviceProvider is null || args.Length == 0)
        {
            return false;
        }

        var command = args[0];
        var paths = args.Skip(1).Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
        await logger.LogInformationAsync($"Pixoar desktop launch command: {command}");

        if (string.Equals(command, "--info", StringComparison.OrdinalIgnoreCase))
        {
            await logger.LogInformationAsync("Opening image information from launch argument.");
            await ShowInformationWindowAsync(paths.FirstOrDefault(), logger);
            return true;
        }

        if (string.Equals(command, "--open", StringComparison.OrdinalIgnoreCase))
        {
            await logger.LogInformationAsync($"Opening {paths.Length} image path(s) from launch arguments.");
            var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();
            await mainViewModel.LoadPathsAsync(paths);
            StartAutomaticUpdateCheck(logger);
            return true;
        }

        return false;
    }

    private void StartAutomaticUpdateCheck(IApplicationLogger logger)
    {
        if (_serviceProvider is null || _automaticUpdateCheckStarted)
        {
            return;
        }

        if (!_serviceProvider.GetRequiredService<ISettingsService>().Current.General.CheckForUpdates)
        {
            return;
        }

        _automaticUpdateCheckStarted = true;
        _ = RunAutomaticUpdateCheckAsync(logger);
    }

    private async Task RunAutomaticUpdateCheckAsync(IApplicationLogger logger)
    {
        if (_serviceProvider is null)
        {
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            var result = await _serviceProvider.GetRequiredService<IUpdateService>().CheckForUpdatesAsync();

            if (result.Status == UpdateStatus.UpdateAvailable)
            {
                await Dispatcher.InvokeAsync(() =>
                    _serviceProvider.GetRequiredService<IUserPromptService>().ShowAutomaticUpdateAvailable(result));
                return;
            }

            if (result.Status == UpdateStatus.CheckFailed)
            {
                await logger.LogWarningAsync("Automatic update check failed.");
            }
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync("Automatic update check failed.", ex);
        }
    }

    private async Task ShowInformationWindowAsync(string? path, IApplicationLogger logger)
    {
        if (_serviceProvider is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            await logger.LogErrorAsync("Image information launch failed because no valid file path was provided.");
            MessageBox.Show(
                "Pixoar could not open image information because the selected file path was not valid.",
                "Pixoar",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Shutdown(3);
            return;
        }

        var viewModel = _serviceProvider.GetRequiredService<ImageInformationViewModel>();
        await viewModel.LoadAsync(path!);

        var window = _serviceProvider.GetRequiredService<ImageInformationWindow>();
        window.DataContext = viewModel;
        MainWindow = window;
        window.Show();
    }
}
