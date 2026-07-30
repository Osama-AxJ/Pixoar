using System.Windows;
using Pixoar.App.ViewModels;

namespace Pixoar.App.Views;

/// <summary>
/// Hosts the primary Pixoar desktop shell.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <param name="viewModel">The view model backing the shell.</param>
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
