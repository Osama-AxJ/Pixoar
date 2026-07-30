using System.Diagnostics;
using System.IO;
using System.Windows;
using Pixoar.App.Commands;
using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.App.ViewModels;

/// <summary>
/// Provides metadata for the image information dialog.
/// </summary>
public sealed class ImageInformationViewModel : ViewModelBase
{
    private readonly IImageInfoService _imageInfoService;
    private string _fileName = "No image selected";
    private string _filePath = string.Empty;
    private string _extension = string.Empty;
    private string _resolution = "Pending";
    private string _format = "Pending";
    private string _fileSize = "Pending";
    private string _aspectRatio = "Pending";
    private string _createdDate = "Pending";
    private string _lastModifiedDate = "Pending";
    private string _colorDepth = "Pending";
    private string _hasAlpha = "Pending";
    private string _hasTransparency = "Pending";
    private string _ddsCompression = "Pending";
    private string _ddsMipmaps = "Pending";
    private bool _isDds;
    private string _actionStatus = "Image information loaded.";

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageInformationViewModel"/> class.
    /// </summary>
    /// <param name="imageInfoService">The image information service.</param>
    public ImageInformationViewModel(IImageInfoService imageInfoService)
    {
        _imageInfoService = imageInfoService;
        OpenLocationCommand = new RelayCommand(_ => OpenLocation());
        CopyPathCommand = new RelayCommand(_ => CopyPath());
    }

    /// <summary>
    /// Gets the command that opens the selected image location.
    /// </summary>
    public RelayCommand OpenLocationCommand { get; }

    /// <summary>
    /// Gets the command that copies the selected image path.
    /// </summary>
    public RelayCommand CopyPathCommand { get; }

    /// <summary>
    /// Gets or sets the file name.
    /// </summary>
    public string FileName
    {
        get => _fileName;
        set => SetProperty(ref _fileName, value);
    }

    /// <summary>
    /// Gets or sets the file path.
    /// </summary>
    public string FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    /// <summary>
    /// Gets or sets the file extension.
    /// </summary>
    public string Extension
    {
        get => _extension;
        set => SetProperty(ref _extension, value);
    }

    /// <summary>
    /// Gets or sets the resolution text.
    /// </summary>
    public string Resolution
    {
        get => _resolution;
        set => SetProperty(ref _resolution, value);
    }

    /// <summary>
    /// Gets or sets the format text.
    /// </summary>
    public string Format
    {
        get => _format;
        set => SetProperty(ref _format, value);
    }

    /// <summary>
    /// Gets or sets the file size text.
    /// </summary>
    public string FileSize
    {
        get => _fileSize;
        set => SetProperty(ref _fileSize, value);
    }

    /// <summary>
    /// Gets or sets the aspect ratio text.
    /// </summary>
    public string AspectRatio
    {
        get => _aspectRatio;
        set => SetProperty(ref _aspectRatio, value);
    }

    /// <summary>
    /// Gets or sets the created date text.
    /// </summary>
    public string CreatedDate
    {
        get => _createdDate;
        set => SetProperty(ref _createdDate, value);
    }

    /// <summary>
    /// Gets or sets the last modified date text.
    /// </summary>
    public string LastModifiedDate
    {
        get => _lastModifiedDate;
        set => SetProperty(ref _lastModifiedDate, value);
    }

    /// <summary>
    /// Gets or sets the color depth text.
    /// </summary>
    public string ColorDepth
    {
        get => _colorDepth;
        set => SetProperty(ref _colorDepth, value);
    }

    /// <summary>
    /// Gets or sets alpha-channel text.
    /// </summary>
    public string HasAlpha
    {
        get => _hasAlpha;
        set => SetProperty(ref _hasAlpha, value);
    }

    /// <summary>
    /// Gets or sets transparency text.
    /// </summary>
    public string HasTransparency
    {
        get => _hasTransparency;
        set => SetProperty(ref _hasTransparency, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether DDS details are available.
    /// </summary>
    public bool IsDds
    {
        get => _isDds;
        set => SetProperty(ref _isDds, value);
    }

    /// <summary>
    /// Gets or sets DDS compression text.
    /// </summary>
    public string DdsCompression
    {
        get => _ddsCompression;
        set => SetProperty(ref _ddsCompression, value);
    }

    /// <summary>
    /// Gets or sets DDS mipmap text.
    /// </summary>
    public string DdsMipmaps
    {
        get => _ddsMipmaps;
        set => SetProperty(ref _ddsMipmaps, value);
    }

    /// <summary>
    /// Gets or sets the action status text.
    /// </summary>
    public string ActionStatus
    {
        get => _actionStatus;
        set => SetProperty(ref _actionStatus, value);
    }

    /// <summary>
    /// Loads image information into the dialog.
    /// </summary>
    /// <param name="path">The image path.</param>
    /// <returns>A task that completes when metadata is loaded.</returns>
    public async Task LoadAsync(string path)
    {
        var info = await _imageInfoService.GetInformationAsync(path);
        Apply(info);
    }

    private void Apply(ImageInformation info)
    {
        FileName = info.FileName;
        FilePath = info.FilePath;
        Extension = info.Extension;
        Resolution = info.Width > 0 && info.Height > 0 ? $"{info.Width}x{info.Height}" : "Unknown";
        Format = info.FormatDisplayName;
        FileSize = info.FileSize;
        AspectRatio = info.AspectRatio;
        CreatedDate = info.CreatedDate.ToString("g");
        LastModifiedDate = info.LastModifiedDate.ToString("g");
        ColorDepth = info.ColorDepth;
        HasAlpha = FormatYesNo(info.HasAlpha);
        HasTransparency = FormatYesNo(info.HasTransparency);
        IsDds = info.Dds is not null;

        DdsCompression = info.Dds?.CompressionType ?? "Not applicable";
        DdsMipmaps = info.Dds?.MipmapCount?.ToString() ?? "Not applicable";
        ActionStatus = "Metadata has already been loaded.";
    }

    private void OpenLocation()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            ActionStatus = "No image path is available.";
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = false
            };

            startInfo.ArgumentList.Add(File.Exists(FilePath)
                ? $"/select,{FilePath}"
                : Path.GetDirectoryName(FilePath) ?? FilePath);

            Process.Start(startInfo);
            ActionStatus = "Image location opened.";
        }
        catch
        {
            ActionStatus = "Pixoar couldn't open the image location.";
        }
    }

    private void CopyPath()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            ActionStatus = "No image path is available.";
            return;
        }

        try
        {
            Clipboard.SetText(FilePath);
            ActionStatus = "Image path copied.";
        }
        catch
        {
            ActionStatus = "Pixoar couldn't copy the image path.";
        }
    }

    private static string FormatYesNo(bool value)
    {
        return value ? "Yes" : "No";
    }
}
