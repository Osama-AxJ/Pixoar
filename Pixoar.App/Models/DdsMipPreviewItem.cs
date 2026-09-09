using System.Windows.Media;
using Pixoar.App.ViewModels;

namespace Pixoar.App.Models;

/// <summary>
/// A visual entry in the DDS mip-chain preview.
/// </summary>
public sealed class DdsMipPreviewItem : ViewModelBase
{
    private ImageSource? _image;

    /// <summary>Gets the zero-based stored mip level.</summary>
    public int Level { get; init; }

    /// <summary>Gets the stored mip width.</summary>
    public int PixelWidth { get; init; }

    /// <summary>Gets the stored mip height.</summary>
    public int PixelHeight { get; init; }

    /// <summary>Gets the visual thumbnail width.</summary>
    public double DisplayWidth { get; init; }

    /// <summary>Gets the visual thumbnail height.</summary>
    public double DisplayHeight { get; init; }

    /// <summary>Gets the display scaling mode appropriate for this mip.</summary>
    public BitmapScalingMode ScalingMode { get; init; }

    /// <summary>Gets the mip label.</summary>
    public string Label => $"Mip {Level}";

    /// <summary>Gets the stored resolution label.</summary>
    public string Resolution => $"{PixelWidth} × {PixelHeight}";

    /// <summary>Gets or sets the decoded preview image.</summary>
    public ImageSource? Image
    {
        get => _image;
        set
        {
            if (SetProperty(ref _image, value))
            {
                OnPropertyChanged(nameof(HasImage));
            }
        }
    }

    /// <summary>Gets a value indicating whether the preview image is decoded.</summary>
    public bool HasImage => Image is not null;
}
