using Pixoar.App.ViewModels;
using System.Windows.Media;

namespace Pixoar.App.Models;

/// <summary>
/// Represents an image entry displayed by the desktop UI.
/// </summary>
public sealed class ImageFileItem : ViewModelBase
{
    private ImageSource? _thumbnail;
    private ImageSource? _previewImage;
    private string _resolution = "Loading...";
    private string _format = string.Empty;
    private string _fileSize = string.Empty;

    /// <summary>
    /// Gets or sets the full image path.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display file name.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the placeholder image resolution text.
    /// </summary>
    public string Resolution
    {
        get => _resolution;
        set => SetProperty(ref _resolution, value);
    }

    /// <summary>
    /// Gets or sets the file format text.
    /// </summary>
    public string Format
    {
        get => _format;
        set => SetProperty(ref _format, value);
    }

    /// <summary>
    /// Gets or sets the formatted file size.
    /// </summary>
    public string FileSize
    {
        get => _fileSize;
        set => SetProperty(ref _fileSize, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether this entry is a DDS file.
    /// </summary>
    public bool IsDds { get; set; }

    /// <summary>
    /// Gets or sets the glyph used for list thumbnails.
    /// </summary>
    public string ThumbnailGlyph { get; set; } = "\uE91B";

    /// <summary>
    /// Gets or sets the glyph used for the large preview placeholder.
    /// </summary>
    public string PreviewGlyph { get; set; } = "\uE91B";

    /// <summary>
    /// Gets or sets the loaded thumbnail image.
    /// </summary>
    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set
        {
            if (SetProperty(ref _thumbnail, value))
            {
                OnPropertyChanged(nameof(HasThumbnail));
            }
        }
    }

    /// <summary>
    /// Gets or sets the loaded preview image.
    /// </summary>
    public ImageSource? PreviewImage
    {
        get => _previewImage;
        set
        {
            if (SetProperty(ref _previewImage, value))
            {
                OnPropertyChanged(nameof(HasPreviewImage));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether a thumbnail image is loaded.
    /// </summary>
    public bool HasThumbnail => Thumbnail is not null;

    /// <summary>
    /// Gets a value indicating whether a preview image is loaded.
    /// </summary>
    public bool HasPreviewImage => PreviewImage is not null;
}
