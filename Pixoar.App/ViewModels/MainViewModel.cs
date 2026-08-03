using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pixoar.App.Commands;
using Pixoar.App.Models;
using Pixoar.App.Services;
using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.App.ViewModels;

/// <summary>
/// Provides state and commands for the main Pixoar workspace.
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly IFileDialogService _fileDialogService;
    private readonly IWindowService _windowService;
    private readonly IImageFormatDetector _formatDetector;
    private readonly IImageInfoService _imageInfoService;
    private readonly IImagePreviewService _imagePreviewService;
    private readonly IImageConversionService _imageConversionService;
    private readonly IImageResizeService _imageResizeService;
    private readonly ISettingsService _settingsService;
    private readonly IUserPromptService _userPromptService;
    private ImageFileItem? _selectedImage;
    private string _statusText = "Ready";
    private string _selectedOutputFormat = "PNG";
    private string _resizeWidth = string.Empty;
    private string _resizeHeight = string.Empty;
    private bool _keepAspectRatio = true;
    private string _selectedResizeMethod = "By Dimensions";
    private string _selectedResizePercentage = "50%";
    private string _selectedResizeMode = "Fit";
    private double _progressPercent;
    private bool _isBusy;
    private int _previewLoadVersion;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// </summary>
    public MainViewModel(
        IFileDialogService fileDialogService,
        IWindowService windowService,
        IImageFormatDetector formatDetector,
        IImageInfoService imageInfoService,
        IImagePreviewService imagePreviewService,
        IImageConversionService imageConversionService,
        IImageResizeService imageResizeService,
        ISettingsService settingsService,
        IUserPromptService userPromptService)
    {
        _fileDialogService = fileDialogService;
        _windowService = windowService;
        _formatDetector = formatDetector;
        _imageInfoService = imageInfoService;
        _imagePreviewService = imagePreviewService;
        _imageConversionService = imageConversionService;
        _imageResizeService = imageResizeService;
        _settingsService = settingsService;
        _userPromptService = userPromptService;

        ApplicationTitle = "Pixoar";
        VersionText = $"Version {GetVersionText()}";
        OutputFormats = ["PNG", "JPG", "JPEG", "WEBP", "BMP", "TIFF", "DDS"];
        ResizeMethods = ["By Dimensions", "By Percentage"];
        ResizePercentages = ["50%", "75%"];
        ResizeModes = ["Stretch", "Crop", "Fit"];

        AddImagesCommand = new AsyncRelayCommand(_ => AddImagesAsync(), _ => !IsBusy);
        AddFolderCommand = new AsyncRelayCommand(_ => AddFolderAsync(), _ => !IsBusy);
        RemoveSelectedCommand = new RelayCommand(_ => RemoveSelected(), _ => HasSelection && !IsBusy);
        ClearListCommand = new RelayCommand(_ => ClearList(), _ => Images.Count > 0 && !IsBusy);
        OpenSettingsCommand = new RelayCommand(_ => _windowService.ShowSettingsWindow(), _ => !IsBusy);
        ShowImageInformationCommand = new AsyncRelayCommand(_ => ShowImageInformationAsync(), _ => SelectedImage is not null && !IsBusy);
        DropFilesCommand = new AsyncRelayCommand(AddDroppedPathsAsync, _ => !IsBusy);
        ConvertCommand = new AsyncRelayCommand(_ => ConvertSelectedAsync(), _ => HasSelection && !IsBusy);
        ResizeCommand = new AsyncRelayCommand(_ => ResizeSelectedAsync(), _ => CanResize);

        SelectedImages.CollectionChanged += OnSelectedImagesChanged;
        Images.CollectionChanged += OnImagesChanged;
    }

    /// <summary>
    /// Gets the application title.
    /// </summary>
    public string ApplicationTitle { get; }

    /// <summary>
    /// Gets the version text displayed by the UI.
    /// </summary>
    public string VersionText { get; }

    /// <summary>
    /// Gets the loaded image entries.
    /// </summary>
    public ObservableCollection<ImageFileItem> Images { get; } = [];

    /// <summary>
    /// Gets the selected image entries.
    /// </summary>
    public ObservableCollection<ImageFileItem> SelectedImages { get; } = [];

    /// <summary>
    /// Gets user-visible processing errors.
    /// </summary>
    public ObservableCollection<string> Errors { get; } = [];

    /// <summary>
    /// Gets the supported output formats shown by the convert panel.
    /// </summary>
    public IReadOnlyList<string> OutputFormats { get; }

    /// <summary>
    /// Gets the resize methods shown by the resize panel.
    /// </summary>
    public IReadOnlyList<string> ResizeMethods { get; }

    /// <summary>
    /// Gets the percentage resize choices shown by the resize panel.
    /// </summary>
    public IReadOnlyList<string> ResizePercentages { get; }

    /// <summary>
    /// Gets the resize modes shown by the resize panel.
    /// </summary>
    public IReadOnlyList<string> ResizeModes { get; }

    /// <summary>
    /// Gets the command that opens the add-images dialog.
    /// </summary>
    public AsyncRelayCommand AddImagesCommand { get; }

    /// <summary>
    /// Gets the command that opens the add-folder dialog.
    /// </summary>
    public AsyncRelayCommand AddFolderCommand { get; }

    /// <summary>
    /// Gets the command that removes selected images.
    /// </summary>
    public RelayCommand RemoveSelectedCommand { get; }

    /// <summary>
    /// Gets the command that clears the image list.
    /// </summary>
    public RelayCommand ClearListCommand { get; }

    /// <summary>
    /// Gets the command that opens settings.
    /// </summary>
    public RelayCommand OpenSettingsCommand { get; }

    /// <summary>
    /// Gets the command that opens image information.
    /// </summary>
    public AsyncRelayCommand ShowImageInformationCommand { get; }

    /// <summary>
    /// Gets the command that handles dropped files and folders.
    /// </summary>
    public AsyncRelayCommand DropFilesCommand { get; }

    /// <summary>
    /// Gets the convert command.
    /// </summary>
    public AsyncRelayCommand ConvertCommand { get; }

    /// <summary>
    /// Gets the resize command.
    /// </summary>
    public AsyncRelayCommand ResizeCommand { get; }

    /// <summary>
    /// Gets or sets the selected image displayed in the preview panel.
    /// </summary>
    public ImageFileItem? SelectedImage
    {
        get => _selectedImage;
        set
        {
            if (SetProperty(ref _selectedImage, value))
            {
                OnPropertyChanged(nameof(HasSelectedImage));
                ShowImageInformationCommand.NotifyCanExecuteChanged();
                RefreshResizeState();
                _ = LoadSelectedPreviewAsync(value, ++_previewLoadVersion);
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether at least one image is selected.
    /// </summary>
    public bool HasSelection => SelectedImages.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the preview panel has a selected image.
    /// </summary>
    public bool HasSelectedImage => SelectedImage is not null;

    /// <summary>
    /// Gets a value indicating whether the current resize settings can be submitted.
    /// </summary>
    public bool CanResize => HasSelection && !IsBusy && HasValidResizeInput();

    /// <summary>
    /// Gets a value indicating whether dimension-based resize is selected.
    /// </summary>
    public bool IsDimensionResize => string.Equals(SelectedResizeMethod, "By Dimensions", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a value indicating whether percentage-based resize is selected.
    /// </summary>
    public bool IsPercentageResize => string.Equals(SelectedResizeMethod, "By Percentage", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a value indicating whether errors are available.
    /// </summary>
    public bool HasErrors => Errors.Count > 0;

    /// <summary>
    /// Gets the selected image count.
    /// </summary>
    public int SelectedCount => SelectedImages.Count;

    /// <summary>
    /// Gets the loaded image count.
    /// </summary>
    public int LoadedCount => Images.Count;

    /// <summary>
    /// Gets or sets the current status bar text.
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    /// <summary>
    /// Gets or sets the current progress percentage.
    /// </summary>
    public double ProgressPercent
    {
        get => _progressPercent;
        set => SetProperty(ref _progressPercent, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether a background operation is active.
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommandStates();
            }
        }
    }

    /// <summary>
    /// Gets or sets the selected output format.
    /// </summary>
    public string SelectedOutputFormat
    {
        get => _selectedOutputFormat;
        set => SetProperty(ref _selectedOutputFormat, value);
    }

    /// <summary>
    /// Gets or sets the resize width value.
    /// </summary>
    public string ResizeWidth
    {
        get => _resizeWidth;
        set
        {
            if (SetProperty(ref _resizeWidth, value))
            {
                RefreshResizeState();
            }
        }
    }

    /// <summary>
    /// Gets or sets the resize height value.
    /// </summary>
    public string ResizeHeight
    {
        get => _resizeHeight;
        set
        {
            if (SetProperty(ref _resizeHeight, value))
            {
                RefreshResizeState();
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether aspect ratio should be preserved.
    /// </summary>
    public bool KeepAspectRatio
    {
        get => _keepAspectRatio;
        set => SetProperty(ref _keepAspectRatio, value);
    }

    /// <summary>
    /// Gets or sets the selected resize method.
    /// </summary>
    public string SelectedResizeMethod
    {
        get => _selectedResizeMethod;
        set
        {
            if (SetProperty(ref _selectedResizeMethod, value))
            {
                RefreshResizeState();
                OnPropertyChanged(nameof(IsDimensionResize));
                OnPropertyChanged(nameof(IsPercentageResize));
            }
        }
    }

    /// <summary>
    /// Gets or sets the selected percentage resize value.
    /// </summary>
    public string SelectedResizePercentage
    {
        get => _selectedResizePercentage;
        set
        {
            if (SetProperty(ref _selectedResizePercentage, value))
            {
                RefreshResizeState();
            }
        }
    }

    /// <summary>
    /// Gets or sets the selected resize mode.
    /// </summary>
    public string SelectedResizeMode
    {
        get => _selectedResizeMode;
        set => SetProperty(ref _selectedResizeMode, value);
    }

    /// <summary>
    /// Gets a short estimate of the resize output for the selected image.
    /// </summary>
    public string ResizeEstimateText
    {
        get
        {
            if (!IsPercentageResize)
            {
                return "Manual dimensions use the selected resize mode.";
            }

            if (!TryParsePercentage(SelectedResizePercentage, out var percentage))
            {
                return "Choose a valid percentage.";
            }

            if (SelectedImages.Count > 1)
            {
                return $"Each image will be resized to {percentage}% of its own dimensions.";
            }

            var image = SelectedImage ?? SelectedImages.FirstOrDefault();
            if (image is not null && TryParseResolution(image.Resolution, out var width, out var height))
            {
                var scale = percentage / 100d;
                var outputWidth = Math.Max(1, (int)Math.Round(width * scale));
                var outputHeight = Math.Max(1, (int)Math.Round(height * scale));
                return $"Output: {outputWidth}x{outputHeight}";
            }

            return $"Output: {percentage}% of original dimensions.";
        }
    }

    /// <summary>
    /// Loads image paths into the workspace from startup arguments or shell integration.
    /// </summary>
    /// <param name="paths">The image paths to load.</param>
    /// <returns>A task that completes when paths have been processed.</returns>
    public Task LoadPathsAsync(IEnumerable<string> paths)
    {
        return AddPathsAsync(paths);
    }

    private async Task AddImagesAsync()
    {
        await AddPathsAsync(_fileDialogService.SelectImageFiles());
    }

    private async Task AddFolderAsync()
    {
        var folder = _fileDialogService.SelectFolder();
        if (string.IsNullOrWhiteSpace(folder))
        {
            StatusText = "Ready";
            return;
        }

        StatusText = "Loading...";
        ClearErrors();
        var files = EnumerateSupportedFiles(folder, recursive: true).ToArray();

        await AddPathsAsync(files);
    }

    private async Task AddDroppedPathsAsync(object? parameter)
    {
        if (parameter is not string[] paths)
        {
            return;
        }

        var files = paths.SelectMany(ExpandPath).Where(_formatDetector.IsSupported).ToArray();
        await AddPathsAsync(files);
    }

    private async Task AddPathsAsync(IEnumerable<string> paths)
    {
        var pathList = paths.Where(_formatDetector.IsSupported).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (pathList.Length == 0)
        {
            StatusText = "Ready";
            return;
        }

        IsBusy = true;
        ProgressPercent = 0;
        ClearErrors();

        try
        {
            var knownPaths = Images.Select(image => image.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var added = 0;

            for (var index = 0; index < pathList.Length; index++)
            {
                var path = pathList[index];
                if (!knownPaths.Add(path))
                {
                    continue;
                }

                StatusText = $"Loading {Path.GetFileName(path)}...";
                var item = CreateImageItem(path);
                Images.Add(item);
                added++;

                await PopulateImageItemAsync(item);
                ProgressPercent = (index + 1) * 100d / pathList.Length;
            }

            if (Images.Count > 0 && SelectedImage is null)
            {
                SelectedImage = Images[0];
            }

            StatusText = added == 0 ? "Ready" : $"Loaded {added} image{(added == 1 ? string.Empty : "s")}.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PopulateImageItemAsync(ImageFileItem item)
    {
        try
        {
            var info = await _imageInfoService.GetInformationAsync(item.FilePath);
            item.Format = info.FormatDisplayName;
            item.Resolution = info.Width > 0 && info.Height > 0 ? $"{info.Width}x{info.Height}" : "Unknown";
            item.FileSize = info.FileSize;
            item.IsDds = info.Format == ImageFormat.Dds;
            item.ThumbnailGlyph = item.IsDds ? "\uE8A5" : "\uE91B";
            item.PreviewGlyph = item.IsDds ? "\uE8A5" : "\uE91B";

            var thumbnail = await _imagePreviewService.LoadPreviewAsync(item.FilePath, 96);
            item.Thumbnail = CreateBitmapImage(thumbnail.PngBytes);

            if (thumbnail.IsPlaceholder && !string.IsNullOrWhiteSpace(thumbnail.Message))
            {
                AddError($"{item.FileName}: {thumbnail.Message}");
            }
        }
        catch (Exception)
        {
            item.Resolution = "Unavailable";
            AddError($"{item.FileName}: The selected image could not be loaded.");
        }
    }

    private void RemoveSelected()
    {
        var selected = SelectedImages.ToArray();
        foreach (var image in selected)
        {
            Images.Remove(image);
        }

        SelectedImages.Clear();
        SelectedImage = Images.FirstOrDefault();
        StatusText = selected.Length == 0 ? "Ready" : $"Removed {selected.Length} image{(selected.Length == 1 ? string.Empty : "s")}.";
    }

    private void ClearList()
    {
        Images.Clear();
        SelectedImages.Clear();
        SelectedImage = null;
        ClearErrors();
        ProgressPercent = 0;
        StatusText = "Ready";
    }

    private async Task ShowImageInformationAsync()
    {
        if (SelectedImage is not null)
        {
            await _windowService.ShowImageInformationAsync(SelectedImage);
        }
    }

    private async Task ConvertSelectedAsync()
    {
        if (RequiresOverwriteConfirmation() && !_userPromptService.ConfirmOverwriteRisk())
        {
            StatusText = "Conversion canceled.";
            return;
        }

        if (!TryParseOutputFormat(SelectedOutputFormat, out var outputFormat))
        {
            AddError($"Unsupported output format: {SelectedOutputFormat}");
            return;
        }

        var requests = SelectedImages
            .Select(image => new ImageConversionRequest
            {
                InputPath = image.FilePath,
                OutputFormat = outputFormat
            })
            .ToArray();

        await RunBatchAsync(
            "Convert",
            progress => _imageConversionService.ConvertBatchAsync(requests, progress),
            "conversion");
    }

    private async Task ResizeSelectedAsync()
    {
        if (RequiresOverwriteConfirmation() && !_userPromptService.ConfirmOverwriteRisk())
        {
            StatusText = "Resize canceled.";
            return;
        }

        var width = ParsePositiveInt(ResizeWidth);
        var height = ParsePositiveInt(ResizeHeight);
        var isPercentageResize = IsPercentageResize;
        var percentage = 0;

        if (isPercentageResize && !TryParsePercentage(SelectedResizePercentage, out percentage))
        {
            AddError("Choose a valid resize percentage.");
            StatusText = "Resize needs a percentage.";
            return;
        }

        if (!isPercentageResize && width is null && height is null)
        {
            AddError("Enter a width, height, or both before resizing.");
            StatusText = "Resize needs dimensions.";
            return;
        }

        var mode = ParseResizeMode(SelectedResizeMode);
        var requests = SelectedImages
            .Select(image => new ImageResizeRequest
            {
                InputPath = image.FilePath,
                ResizeMethod = isPercentageResize ? ResizeMethod.Percentage : ResizeMethod.Dimensions,
                Width = isPercentageResize ? null : width,
                Height = isPercentageResize ? null : height,
                Percentage = isPercentageResize ? percentage : null,
                KeepAspectRatio = isPercentageResize || KeepAspectRatio,
                Mode = isPercentageResize ? ResizeMode.Fit : mode
            })
            .ToArray();

        await RunBatchAsync(
            "Resize",
            progress => _imageResizeService.ResizeBatchAsync(requests, progress),
            "resize",
            addOutputFiles: true);
    }

    private async Task RunBatchAsync(
        string operationName,
        Func<IProgress<ImageOperationProgress>, Task<BatchImageOperationResult>> operation,
        string completionNoun,
        bool addOutputFiles = false)
    {
        IsBusy = true;
        ProgressPercent = 0;
        ClearErrors();

        try
        {
            var progress = new Progress<ImageOperationProgress>(value =>
            {
                ProgressPercent = value.Percent;
                StatusText = $"{value.Status} {Path.GetFileName(value.CurrentFile)}";
            });

            var result = await operation(progress);
            foreach (var error in result.Errors)
            {
                AddError($"{Path.GetFileName(error.InputPath)}: {error.Message}");
            }

            if (addOutputFiles)
            {
                await AddOutputFilesAsync(result.SuccessfulResults.Select(success => success.OutputPath));
            }

            StatusText = result.SkippedCount == 0 && result.ErrorCount == 0
                ? $"Completed {result.SuccessCount} {completionNoun}{(result.SuccessCount == 1 ? string.Empty : "s")}."
                : $"Completed with {result.SuccessCount} success{(result.SuccessCount == 1 ? string.Empty : "es")}, {result.SkippedCount} skipped, and {result.ErrorCount} error{(result.ErrorCount == 1 ? string.Empty : "s")}.";
        }
        catch (Exception)
        {
            AddError($"{operationName}: The operation could not be completed.");
            StatusText = $"{operationName} failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnSelectedImagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedCount));
        RefreshResizeState();
        RefreshCommandStates();
    }

    private void OnImagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(LoadedCount));
        RefreshCommandStates();
    }

    private void ClearErrors()
    {
        Errors.Clear();
        OnPropertyChanged(nameof(HasErrors));
    }

    private void AddError(string message)
    {
        Errors.Add(message);
        OnPropertyChanged(nameof(HasErrors));
    }

    private void RefreshCommandStates()
    {
        AddImagesCommand.NotifyCanExecuteChanged();
        AddFolderCommand.NotifyCanExecuteChanged();
        RemoveSelectedCommand.NotifyCanExecuteChanged();
        ClearListCommand.NotifyCanExecuteChanged();
        OpenSettingsCommand.NotifyCanExecuteChanged();
        ShowImageInformationCommand.NotifyCanExecuteChanged();
        DropFilesCommand.NotifyCanExecuteChanged();
        ConvertCommand.NotifyCanExecuteChanged();
        ResizeCommand.NotifyCanExecuteChanged();
    }

    private void RefreshResizeState()
    {
        OnPropertyChanged(nameof(CanResize));
        OnPropertyChanged(nameof(ResizeEstimateText));
        ResizeCommand.NotifyCanExecuteChanged();
    }

    private async Task AddOutputFilesAsync(IEnumerable<string?> paths)
    {
        var outputPaths = paths
            .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            .Cast<string>()
            .Where(_formatDetector.IsSupported)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (outputPaths.Length == 0)
        {
            return;
        }

        var knownPaths = Images.Select(image => image.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in outputPaths)
        {
            if (!knownPaths.Add(path))
            {
                continue;
            }

            var item = CreateImageItem(path);
            Images.Add(item);
            await PopulateImageItemAsync(item);
        }
    }

    private async Task LoadSelectedPreviewAsync(ImageFileItem? item, int version)
    {
        if (item is null || item.HasPreviewImage)
        {
            return;
        }

        try
        {
            var preview = await _imagePreviewService.LoadPreviewAsync(item.FilePath, 900);
            if (version != _previewLoadVersion || SelectedImage != item)
            {
                return;
            }

            item.PreviewImage = CreateBitmapImage(preview.PngBytes);
            if (preview.IsPlaceholder && !string.IsNullOrWhiteSpace(preview.Message))
            {
                AddError($"{item.FileName}: {preview.Message}");
            }
        }
        catch (Exception)
        {
            if (version == _previewLoadVersion && SelectedImage == item)
            {
                AddError($"{item.FileName}: The preview could not be loaded.");
            }
        }
    }

    private IEnumerable<string> ExpandPath(string path)
    {
        if (File.Exists(path))
        {
            yield return path;
            yield break;
        }

        if (!Directory.Exists(path))
        {
            yield break;
        }

        foreach (var file in EnumerateSupportedFiles(path, recursive: true))
        {
            yield return file;
        }
    }

    private IEnumerable<string> EnumerateSupportedFiles(string folder, bool recursive)
    {
        var directories = new Queue<string>();
        directories.Enqueue(folder);

        while (directories.Count > 0)
        {
            var current = directories.Dequeue();

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current, "*.*", SearchOption.TopDirectoryOnly).ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                AddError($"Skipped folder: {current}");
                continue;
            }

            foreach (var file in files.Where(_formatDetector.IsSupported))
            {
                yield return file;
            }

            if (!recursive)
            {
                continue;
            }

            IEnumerable<string> childDirectories;
            try
            {
                childDirectories = Directory.EnumerateDirectories(current).ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                AddError($"Skipped folder: {current}");
                continue;
            }

            foreach (var childDirectory in childDirectories)
            {
                directories.Enqueue(childDirectory);
            }
        }
    }

    private bool RequiresOverwriteConfirmation()
    {
        return _settingsService.Current.Output.ConflictBehavior ==
            OutputConflictBehavior.OverwriteExistingFiles;
    }

    private static ImageFileItem CreateImageItem(string path)
    {
        var file = new FileInfo(path);
        var format = file.Extension.TrimStart('.').ToUpperInvariant();
        var isDds = string.Equals(format, "DDS", StringComparison.OrdinalIgnoreCase);

        return new ImageFileItem
        {
            FilePath = path,
            FileName = file.Name,
            Format = format,
            FileSize = FormatFileSize(file.Exists ? file.Length : 0),
            Resolution = "Loading...",
            IsDds = isDds,
            ThumbnailGlyph = isDds ? "\uE8A5" : "\uE91B",
            PreviewGlyph = isDds ? "\uE8A5" : "\uE91B"
        };
    }

    private static ImageSource? CreateBitmapImage(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        using var stream = new MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static bool TryParseOutputFormat(string value, out ImageFormat format)
    {
        switch (value.ToUpperInvariant())
        {
            case "PNG":
                format = ImageFormat.Png;
                return true;
            case "JPG":
            case "JPEG":
                format = ImageFormat.Jpeg;
                return true;
            case "WEBP":
                format = ImageFormat.Webp;
                return true;
            case "BMP":
                format = ImageFormat.Bmp;
                return true;
            case "TIFF":
            case "TIF":
                format = ImageFormat.Tiff;
                return true;
            case "DDS":
                format = ImageFormat.Dds;
                return true;
            default:
                format = default;
                return false;
        }
    }

    private static ResizeMode ParseResizeMode(string value)
    {
        return value switch
        {
            "Stretch" => ResizeMode.Stretch,
            "Crop" => ResizeMode.Crop,
            _ => ResizeMode.Fit
        };
    }

    private static int? ParsePositiveInt(string value)
    {
        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : null;
    }

    private bool HasValidResizeInput()
    {
        return IsPercentageResize
            ? TryParsePercentage(SelectedResizePercentage, out _)
            : ParsePositiveInt(ResizeWidth) is not null || ParsePositiveInt(ResizeHeight) is not null;
    }

    private static bool TryParsePercentage(string value, out int percentage)
    {
        var trimmed = value.Trim();
        return int.TryParse(trimmed.TrimEnd('%'), out percentage)
            && trimmed.EndsWith('%')
            && percentage > 0;
    }

    private static bool TryParseResolution(string value, out int width, out int height)
    {
        width = 0;
        height = 0;

        var parts = value.Split('x', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2
            && int.TryParse(parts[0], out width)
            && int.TryParse(parts[1], out height)
            && width > 0
            && height > 0;
    }

    private static string FormatFileSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB"];
        var size = (double)bytes;
        var suffixIndex = 0;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return $"{size:0.#} {suffixes[suffixIndex]}";
    }

    private static string GetVersionText()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";
    }
}
