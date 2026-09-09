using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Pixoar.App.ViewModels;

namespace Pixoar.App.Views;

/// <summary>
/// Displays a zoomable image in a separate window.
/// </summary>
public partial class MipPreviewWindow : Window
{
    private Point? _panStart;
    private double _panHorizontalOffset;
    private double _panVerticalOffset;

    /// <summary>
    /// Initializes the inspection window.
    /// </summary>
    public MipPreviewWindow(
        ImageSource image,
        string title,
        string resolution,
        BitmapScalingMode scalingMode)
    {
        InitializeComponent();
        DataContext = new ImagePreviewDialogViewModel(image, title, resolution, scalingMode);
        Title = title;
    }

    private void PreviewScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (DataContext is not ImagePreviewDialogViewModel viewModel)
        {
            return;
        }

        var multiplier = e.Delta > 0 ? 1.1 : 1 / 1.1;
        viewModel.ZoomFactor = Math.Clamp(viewModel.ZoomFactor * multiplier, 0.25, 8);
        e.Handled = true;
    }

    private void PreviewScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _panStart = e.GetPosition(PreviewScrollViewer);
        _panHorizontalOffset = PreviewScrollViewer.HorizontalOffset;
        _panVerticalOffset = PreviewScrollViewer.VerticalOffset;
        PreviewScrollViewer.CaptureMouse();
        PreviewScrollViewer.Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void PreviewScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_panStart is not { } start || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(PreviewScrollViewer);
        PreviewScrollViewer.ScrollToHorizontalOffset(_panHorizontalOffset - (current.X - start.X));
        PreviewScrollViewer.ScrollToVerticalOffset(_panVerticalOffset - (current.Y - start.Y));
    }

    private void PreviewScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _panStart = null;
        PreviewScrollViewer.ReleaseMouseCapture();
        PreviewScrollViewer.Cursor = Cursors.Hand;
        e.Handled = true;
    }
}
