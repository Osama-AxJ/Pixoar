using System.Windows;
using Pixoar.App.ViewModels;

namespace Pixoar.App.Views;

/// <summary>
/// Hosts Pixoar settings pages.
/// </summary>
public partial class SettingsWindow : Window
{
    private bool _contextMenuStatusLoaded;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsWindow"/> class.
    /// </summary>
    /// <param name="viewModel">The settings view model.</param>
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <inheritdoc />
    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        if (_contextMenuStatusLoaded)
        {
            return;
        }

        _contextMenuStatusLoaded = true;
        await ((SettingsViewModel)DataContext).LoadContextMenuInstallationStatusAsync();
    }
}
