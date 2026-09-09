using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pixoar.App.Commands;

namespace Pixoar.App.ViewModels;

/// <summary>
/// Provides zoomable image state for the standalone preview window.
/// </summary>
public sealed class ImagePreviewDialogViewModel : ViewModelBase
{
    private double _zoomFactor = 1;

    /// <summary>
    /// Initializes a zoomable preview.
    /// </summary>
    public ImagePreviewDialogViewModel(
        ImageSource image,
        string title,
        string resolution,
        BitmapScalingMode scalingMode)
    {
        Image = image;
        Title = title;
        Resolution = resolution;
        ScalingMode = scalingMode;
        PixelWidth = image is BitmapSource bitmap ? bitmap.PixelWidth : Math.Max(1, image.Width);
        PixelHeight = image is BitmapSource bitmapHeight ? bitmapHeight.PixelHeight : Math.Max(1, image.Height);
        ZoomInCommand = new RelayCommand(_ => ZoomFactor = Math.Min(8, ZoomFactor + 0.25));
        ZoomOutCommand = new RelayCommand(_ => ZoomFactor = Math.Max(0.25, ZoomFactor - 0.25));
        ResetZoomCommand = new RelayCommand(_ => ZoomFactor = 1);
    }

    /// <summary>Gets the image displayed in the dialog.</summary>
    public ImageSource Image { get; }

    /// <summary>Gets the dialog title.</summary>
    public string Title { get; }

    /// <summary>Gets the image resolution label.</summary>
    public string Resolution { get; }

    /// <summary>Gets the scaling mode used by the image.</summary>
    public BitmapScalingMode ScalingMode { get; }

    /// <summary>Gets the decoded image width.</summary>
    public double PixelWidth { get; }

    /// <summary>Gets the decoded image height.</summary>
    public double PixelHeight { get; }

    /// <summary>Gets or sets the zoom multiplier.</summary>
    public double ZoomFactor
    {
        get => _zoomFactor;
        set
        {
            if (SetProperty(ref _zoomFactor, value))
            {
                OnPropertyChanged(nameof(ZoomText));
                OnPropertyChanged(nameof(ZoomedWidth));
                OnPropertyChanged(nameof(ZoomedHeight));
            }
        }
    }

    /// <summary>Gets the human-readable zoom percentage.</summary>
    public string ZoomText => $"{ZoomFactor:P0}";

    /// <summary>Gets the displayed image width at the current zoom.</summary>
    public double ZoomedWidth => PixelWidth * ZoomFactor;

    /// <summary>Gets the displayed image height at the current zoom.</summary>
    public double ZoomedHeight => PixelHeight * ZoomFactor;

    /// <summary>Gets the zoom-in command.</summary>
    public RelayCommand ZoomInCommand { get; }

    /// <summary>Gets the zoom-out command.</summary>
    public RelayCommand ZoomOutCommand { get; }

    /// <summary>Gets the reset-zoom command.</summary>
    public RelayCommand ResetZoomCommand { get; }
}
