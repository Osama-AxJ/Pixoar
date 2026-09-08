using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
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
    private readonly string _pendingExportRoot = Path.Combine(
        Path.GetTempPath(),
        "Pixoar",
        "PendingExports",
        Guid.NewGuid().ToString("N"));
    private readonly List<string> _pendingExportPaths = [];
    private readonly Dictionary<string, ImageFileItem> _visibleStagedResults =
        new(StringComparer.OrdinalIgnoreCase);
    private ImageFileItem? _selectedImage;
    private string _statusText = "Ready";
    private string _selectedOutputFormat = "PNG";
    private string _selectedDdsCompression = "DXT5";
    private string _resizeWidth = string.Empty;
    private string _resizeHeight = string.Empty;
    private bool _keepAspectRatio = true;
    private string _selectedResizeMethod = "By Dimensions";
    private string _selectedResizePercentage = "50%";
    private string _selectedResizeMode = "Fit";
    private double _progressPercent;
    private bool _isBusy;
    private bool _isUpdatingLinkedDimension;
    private bool _isProcessedListActive;
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
        ISettingsService settingsService)
    {
        _fileDialogService = fileDialogService;
        _windowService = windowService;
        _formatDetector = formatDetector;
        _imageInfoService = imageInfoService;
        _imagePreviewService = imagePreviewService;
        _imageConversionService = imageConversionService;
        _imageResizeService = imageResizeService;
        _settingsService = settingsService;

        ApplicationTitle = "Pixoar";
        VersionText = $"Version {GetVersionText()}";
        OutputFormats = ["PNG", "JPG", "WEBP", "BMP", "TIFF", "DDS"];
        DdsCompressionOptions = ["DXT1", "DXT3", "DXT5", "BC7", "Uncompressed"];
        _selectedDdsCompression = FormatDdsCompression(settingsService.Current.Dds.Compression);
        ResizeMethods = ["By Dimensions", "By Percentage"];
        ResizePercentages = ["50%", "75%"];
        ResizeModes = ["Stretch", "Crop", "Fit"];

        AddImagesCommand = new AsyncRelayCommand(_ => AddImagesAsync(), _ => !IsBusy);
        AddFolderCommand = new AsyncRelayCommand(_ => AddFolderAsync(), _ => !IsBusy);
        RemoveSelectedCommand = new RelayCommand(_ => RemoveSelected(), _ => HasSelection && !IsBusy);
        ClearListCommand = new RelayCommand(
            _ => ClearList(),
            _ => (Images.Count > 0 || ProcessedImages.Count > 0) && !IsBusy);
        OpenSettingsCommand = new RelayCommand(_ => OpenSettings(), _ => !IsBusy);
        ShowImageInformationCommand = new AsyncRelayCommand(_ => ShowImageInformationAsync(), _ => SelectedImage is not null && !IsBusy);
        DropFilesCommand = new AsyncRelayCommand(AddDroppedPathsAsync, _ => !IsBusy);
        ConvertCommand = new AsyncRelayCommand(_ => ConvertSelectedAsync(), _ => HasSelection && !IsBusy);
        ResizeCommand = new AsyncRelayCommand(_ => ResizeSelectedAsync(), _ => CanResize);
        ExportCommand = new AsyncRelayCommand(_ => ExportPendingAsync(), _ => CanExport);
        ShowSourceImagesCommand = new RelayCommand(_ => ShowSourceImages(), _ => !IsBusy);
        ShowProcessedImagesCommand = new RelayCommand(_ => ShowProcessedImages(), _ => !IsBusy);

        SelectedImages.CollectionChanged += OnSelectedImagesChanged;
        Images.CollectionChanged += OnImagesChanged;
        ProcessedImages.CollectionChanged += OnProcessedImagesChanged;
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
    /// Gets completed images that are ready for export or further editing.
    /// </summary>
    public ObservableCollection<ImageFileItem> ProcessedImages { get; } = [];

    /// <summary>
    /// Gets user-visible processing errors.
    /// </summary>
    public ObservableCollection<string> Errors { get; } = [];

    /// <summary>
    /// Gets the supported output formats shown by the convert panel.
    /// </summary>
    public IReadOnlyList<string> OutputFormats { get; }

    /// <summary>
    /// Gets the DDS compression modes shown by the convert panel.
    /// </summary>
    public IReadOnlyList<string> DdsCompressionOptions { get; }

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
    /// Gets the command that exports completed desktop results to a selected folder.
    /// </summary>
    public AsyncRelayCommand ExportCommand { get; }

    /// <summary>
    /// Gets the command that displays source images.
    /// </summary>
    public RelayCommand ShowSourceImagesCommand { get; }

    /// <summary>
    /// Gets the command that displays processed images.
    /// </summary>
    public RelayCommand ShowProcessedImagesCommand { get; }

    /// <summary>
    /// Gets a value indicating whether the processed-images view is active.
    /// </summary>
    public bool IsProcessedListActive => _isProcessedListActive;

    /// <summary>
    /// Gets the image collection shown in the primary list panel.
    /// </summary>
    public ObservableCollection<ImageFileItem> DisplayedImages =>
        IsProcessedListActive ? ProcessedImages : Images;

    /// <summary>
    /// Gets the title for the primary list panel.
    /// </summary>
    public string ActiveListTitle => IsProcessedListActive ? "Processed Images" : "Image List";

    /// <summary>
    /// Gets the number of images in the active list.
    /// </summary>
    public int ActiveListCount => DisplayedImages.Count;

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
                UpdateLinkedDimensionFromCurrentInput();
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
    /// Gets a value indicating whether processed images are ready to export.
    /// </summary>
    public bool CanExport => !IsBusy && _pendingExportPaths.Any(File.Exists);

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
    /// Gets the number of processed images currently available for export or further editing.
    /// </summary>
    public int ProcessedCount => ProcessedImages.Count;

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
        set
        {
            if (SetProperty(ref _selectedOutputFormat, value))
            {
                OnPropertyChanged(nameof(IsDdsOutputFormat));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether DDS is the selected output format.
    /// </summary>
    public bool IsDdsOutputFormat =>
        string.Equals(SelectedOutputFormat, "DDS", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the DDS compression used for the current desktop conversion.
    /// </summary>
    public string SelectedDdsCompression
    {
        get => _selectedDdsCompression;
        set => SetProperty(ref _selectedDdsCompression, value);
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
                UpdateLinkedDimension(isWidthSource: true);
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
                UpdateLinkedDimension(isWidthSource: false);
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
        set => ApplyResizeMode(value ? "Fit" : "Stretch");
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
        set => ApplyResizeMode(value);
    }

    /// <summary>
    /// Gets or sets whether the linked-dimensions resize mode is selected.
    /// </summary>
    public bool IsAspectRatioMode
    {
        get => string.Equals(SelectedResizeMode, "Fit", StringComparison.OrdinalIgnoreCase);
        set
        {
            if (value)
            {
                ApplyResizeMode("Fit");
            }
        }
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
                var outputWidth = ParsePositiveInt(ResizeWidth);
                var outputHeight = ParsePositiveInt(ResizeHeight);
                var width = outputWidth;
                var height = outputHeight;
                if (outputWidth is not null && outputHeight is not null)
                {
                    return $"Output: {outputWidth} x {outputHeight}";
                }

                return outputWidth is not null && outputHeight is not null
                    ? $"Output: {width} × {height}"
                    : "Enter a width or height to calculate the output.";
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
            if (image is not null && TryParseResolution(image.Resolution, out var sourceWidth, out var sourceHeight))
            {
                var scale = percentage / 100d;
                var outputWidth = Math.Max(1, (int)Math.Round(sourceWidth * scale));
                var outputHeight = Math.Max(1, (int)Math.Round(sourceHeight * scale));
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
        var folder = _fileDialogService.SelectFolder("Add Folder");
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
            if (ProcessedImages.Remove(image))
            {
                RemovePendingExport(image.FilePath);
            }
        }

        SelectedImages.Clear();
        SelectedImage = DisplayedImages.FirstOrDefault();
        StatusText = selected.Length == 0 ? "Ready" : $"Removed {selected.Length} image{(selected.Length == 1 ? string.Empty : "s")}.";
    }

    private void ClearList()
    {
        Images.Clear();
        ProcessedImages.Clear();
        SelectedImages.Clear();
        SelectedImage = null;
        ClearErrors();
        ClearPendingExports();
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

    private void OpenSettings()
    {
        var previousDefault = FormatDdsCompression(_settingsService.Current.Dds.Compression);
        _windowService.ShowSettingsWindow();

        if (string.Equals(
            SelectedDdsCompression,
            previousDefault,
            StringComparison.OrdinalIgnoreCase))
        {
            SelectedDdsCompression = FormatDdsCompression(_settingsService.Current.Dds.Compression);
        }
    }

    private async Task ConvertSelectedAsync()
    {
        if (!TryParseOutputFormat(SelectedOutputFormat, out var outputFormat))
        {
            AddError($"Unsupported output format: {SelectedOutputFormat}");
            return;
        }

        DdsCompressionMode? ddsCompression = null;
        if (outputFormat == ImageFormat.Dds)
        {
            if (!TryParseDdsCompression(SelectedDdsCompression, out var parsedCompression))
            {
                AddError($"Unsupported DDS compression: {SelectedDdsCompression}");
                return;
            }

            ddsCompression = parsedCompression;
        }

        var selectedImages = SelectedImages.ToArray();
        var requests = selectedImages
            .Select(image => new ImageConversionRequest
            {
                InputPath = image.FilePath,
                OutputFormat = outputFormat,
                DdsCompression = ddsCompression,
                OutputFolder = CreateBatchStagingDirectory()
            })
            .ToArray();

        var result = await RunBatchAsync(
            "Convert",
            progress => _imageConversionService.ConvertBatchAsync(requests, progress),
            "conversion");
        await MoveSuccessfulResultsToProcessedAsync(result, selectedImages);
    }

    private async Task ResizeSelectedAsync()
    {
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
        var selectedImages = SelectedImages.ToArray();
        var requests = selectedImages
            .Select(image => new ImageResizeRequest
            {
                InputPath = image.FilePath,
                ResizeMethod = isPercentageResize ? ResizeMethod.Percentage : ResizeMethod.Dimensions,
                Width = isPercentageResize ? null : width,
                Height = isPercentageResize ? null : height,
                Percentage = isPercentageResize ? percentage : null,
                KeepAspectRatio = isPercentageResize || KeepAspectRatio,
                Mode = isPercentageResize ? ResizeMode.Fit : mode,
                OutputFolder = CreateBatchStagingDirectory()
            })
            .ToArray();

        var result = await RunBatchAsync(
            "Resize",
            progress => _imageResizeService.ResizeBatchAsync(requests, progress),
            "resize");
        await MoveSuccessfulResultsToProcessedAsync(result, selectedImages);
    }

    private async Task<BatchImageOperationResult?> RunBatchAsync(
        string operationName,
        Func<IProgress<ImageOperationProgress>, Task<BatchImageOperationResult>> operation,
        string completionNoun)
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

            StatusText = result.SkippedCount == 0 && result.ErrorCount == 0
                ? $"Completed {result.SuccessCount} {completionNoun}{(result.SuccessCount == 1 ? string.Empty : "s")}."
                : $"Completed with {result.SuccessCount} success{(result.SuccessCount == 1 ? string.Empty : "es")}, {result.SkippedCount} skipped, and {result.ErrorCount} error{(result.ErrorCount == 1 ? string.Empty : "s")}.";
            return result;
        }
        catch (Exception)
        {
            AddError($"{operationName}: The operation could not be completed.");
            StatusText = $"{operationName} failed.";
            return null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ShowSourceImages()
    {
        SetActiveImageList(isProcessedListActive: false);
    }

    private void ShowProcessedImages()
    {
        SetActiveImageList(isProcessedListActive: true);
    }

    private void SetActiveImageList(bool isProcessedListActive)
    {
        if (_isProcessedListActive == isProcessedListActive)
        {
            return;
        }

        _isProcessedListActive = isProcessedListActive;
        SelectedImages.Clear();
        SelectedImage = null;
        OnPropertyChanged(nameof(IsProcessedListActive));
        OnPropertyChanged(nameof(DisplayedImages));
        OnPropertyChanged(nameof(ActiveListCount));
        OnPropertyChanged(nameof(ActiveListTitle));
        RefreshCommandStates();
    }

    private async Task MoveSuccessfulResultsToProcessedAsync(
        BatchImageOperationResult? result,
        IReadOnlyList<ImageFileItem> sourceImages)
    {
        if (result is null)
        {
            return;
        }

        var sourceByPath = sourceImages
            .GroupBy(image => image.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var newItems = new List<(string Path, ImageFileItem Item)>();

        foreach (var success in result.SuccessfulResults)
        {
            if (string.IsNullOrWhiteSpace(success.OutputPath) || !File.Exists(success.OutputPath))
            {
                continue;
            }

            if (sourceByPath.TryGetValue(success.InputPath, out var sourceItem))
            {
                Images.Remove(sourceItem);
                if (ProcessedImages.Remove(sourceItem))
                {
                    RemovePendingExport(sourceItem.FilePath);
                }

                SelectedImages.Remove(sourceItem);
            }

            var processedItem = CreateImageItem(success.OutputPath);
            ProcessedImages.Add(processedItem);
            _pendingExportPaths.Add(success.OutputPath);
            _visibleStagedResults[success.OutputPath] = processedItem;
            newItems.Add((success.OutputPath, processedItem));
        }

        if (newItems.Count == 0)
        {
            return;
        }

        ShowProcessedImages();
        foreach (var (_, item) in newItems)
        {
            SelectedImages.Add(item);
            await PopulateImageItemAsync(item);
        }

        SelectedImage = newItems[^1].Item;
        StatusText = $"Ready to export {_pendingExportPaths.Count} image{(_pendingExportPaths.Count == 1 ? string.Empty : "s")}.";
        RefreshCommandStates();
    }

    private void RemovePendingExport(string path)
    {
        _pendingExportPaths.RemoveAll(candidate =>
            string.Equals(candidate, path, StringComparison.OrdinalIgnoreCase));
        _visibleStagedResults.Remove(path);
    }

    private async Task ExportPendingAsync()
    {
        var destinationFolder = _fileDialogService.SelectFolder("Export Images");
        if (string.IsNullOrWhiteSpace(destinationFolder))
        {
            StatusText = "Export canceled.";
            return;
        }

        var pendingPaths = _pendingExportPaths.Where(File.Exists).ToArray();
        if (pendingPaths.Length == 0)
        {
            ClearPendingExports();
            StatusText = "There are no completed images to export.";
            return;
        }

        IsBusy = true;
        ProgressPercent = 0;
        ClearErrors();
        var exportedPaths = new List<string>();
        var exportedFilePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            Directory.CreateDirectory(destinationFolder);
            for (var index = 0; index < pendingPaths.Length; index++)
            {
                var stagedPath = pendingPaths[index];
                StatusText = $"Exporting {Path.GetFileName(stagedPath)}";
                var exportPath = GetAvailableExportPath(destinationFolder, Path.GetFileName(stagedPath));
                await Task.Run(() => File.Copy(stagedPath, exportPath));
                exportedPaths.Add(stagedPath);
                exportedFilePaths.Add(stagedPath, exportPath);
                ProgressPercent = (index + 1) * 100d / pendingPaths.Length;
            }

            await ReplaceVisibleStagedResultsAsync(exportedFilePaths);
            _pendingExportPaths.RemoveAll(path => exportedPaths.Contains(path, StringComparer.OrdinalIgnoreCase));
            StatusText = $"Exported {exportedPaths.Count} image{(exportedPaths.Count == 1 ? string.Empty : "s")}.";

            if (_pendingExportPaths.Count == 0)
            {
                ClearPendingExports();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AddError($"Export: {ex.Message}");
            StatusText = "Export failed. Choose another folder and try again.";
        }
        finally
        {
            IsBusy = false;
            RefreshCommandStates();
        }
    }

    /// <summary>
    /// Removes temporary desktop results when they are no longer needed.
    /// </summary>
    public void CleanupPendingExports()
    {
        ClearPendingExports();
    }

    private string CreateBatchStagingDirectory()
    {
        var directory = Path.Combine(_pendingExportRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private void ClearPendingExports()
    {
        _pendingExportPaths.Clear();
        _visibleStagedResults.Clear();

        try
        {
            if (Directory.Exists(_pendingExportRoot))
            {
                Directory.Delete(_pendingExportRoot, recursive: true);
            }
        }
        catch (IOException)
        {
            // Temporary files can be released by the operating system after the window closes.
        }
        catch (UnauthorizedAccessException)
        {
            // Temporary files can be released by the operating system after the window closes.
        }

        RefreshCommandStates();
    }

    private async Task ReplaceVisibleStagedResultsAsync(
        IReadOnlyDictionary<string, string> exportedFilePaths)
    {
        foreach (var (stagedPath, exportPath) in exportedFilePaths)
        {
            if (!_visibleStagedResults.Remove(stagedPath, out var stagedItem) ||
                !ProcessedImages.Contains(stagedItem))
            {
                continue;
            }

            var index = ProcessedImages.IndexOf(stagedItem);
            var exportedItem = CreateImageItem(exportPath);
            ProcessedImages[index] = exportedItem;

            var selectedIndex = SelectedImages.IndexOf(stagedItem);
            if (selectedIndex >= 0)
            {
                SelectedImages[selectedIndex] = exportedItem;
            }

            if (ReferenceEquals(SelectedImage, stagedItem))
            {
                SelectedImage = exportedItem;
            }

            await PopulateImageItemAsync(exportedItem);
        }
    }

    private static string GetAvailableExportPath(string destinationFolder, string fileName)
    {
        var candidate = Path.Combine(destinationFolder, fileName);
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var suffix = 1;
        do
        {
            candidate = Path.Combine(destinationFolder, $"{baseName}_{suffix}{extension}");
            suffix++;
        }
        while (File.Exists(candidate));

        return candidate;
    }

    private void OnSelectedImagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedCount));
        UpdateLinkedDimensionFromCurrentInput();
        RefreshResizeState();
        RefreshCommandStates();
    }

    private void OnImagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(LoadedCount));
        OnPropertyChanged(nameof(DisplayedImages));
        OnPropertyChanged(nameof(ActiveListCount));
        RefreshCommandStates();
    }

    private void OnProcessedImagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ProcessedCount));
        OnPropertyChanged(nameof(DisplayedImages));
        OnPropertyChanged(nameof(ActiveListCount));
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
        ExportCommand.NotifyCanExecuteChanged();
        ShowSourceImagesCommand.NotifyCanExecuteChanged();
        ShowProcessedImagesCommand.NotifyCanExecuteChanged();
    }

    private void RefreshResizeState()
    {
        OnPropertyChanged(nameof(CanResize));
        OnPropertyChanged(nameof(ResizeEstimateText));
        ResizeCommand.NotifyCanExecuteChanged();
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

    private static string FormatDdsCompression(DdsCompressionMode compression)
    {
        return compression switch
        {
            DdsCompressionMode.Dxt1 => "DXT1",
            DdsCompressionMode.Dxt3 => "DXT3",
            DdsCompressionMode.Bc7 => "BC7",
            DdsCompressionMode.Uncompressed => "Uncompressed",
            _ => "DXT5"
        };
    }

    private static bool TryParseDdsCompression(string value, out DdsCompressionMode compression)
    {
        switch (value.Trim().ToUpperInvariant())
        {
            case "DXT1":
                compression = DdsCompressionMode.Dxt1;
                return true;
            case "DXT3":
                compression = DdsCompressionMode.Dxt3;
                return true;
            case "DXT5":
                compression = DdsCompressionMode.Dxt5;
                return true;
            case "BC7":
                compression = DdsCompressionMode.Bc7;
                return true;
            case "UNCOMPRESSED":
                compression = DdsCompressionMode.Uncompressed;
                return true;
            default:
                compression = default;
                return false;
        }
    }

    private static int? ParsePositiveInt(string value)
    {
        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : null;
    }

    private void UpdateLinkedDimensionFromCurrentInput()
    {
        if (ParsePositiveInt(ResizeWidth) is not null)
        {
            UpdateLinkedDimension(isWidthSource: true);
            return;
        }

        UpdateLinkedDimension(isWidthSource: false);
    }

    private void UpdateLinkedDimension(bool isWidthSource)
    {
        if (_isUpdatingLinkedDimension || !KeepAspectRatio || !IsDimensionResize)
        {
            return;
        }

        var image = SelectedImages.FirstOrDefault() ?? SelectedImage;
        if (image is null || !TryParseResolution(image.Resolution, out var sourceWidth, out var sourceHeight))
        {
            return;
        }

        var sourceValue = ParsePositiveInt(isWidthSource ? ResizeWidth : ResizeHeight);
        if (sourceValue is null)
        {
            return;
        }

        var calculatedValue = isWidthSource
            ? Math.Max(1, (int)Math.Round(sourceValue.Value * (double)sourceHeight / sourceWidth))
            : Math.Max(1, (int)Math.Round(sourceValue.Value * (double)sourceWidth / sourceHeight));

        _isUpdatingLinkedDimension = true;
        try
        {
            if (isWidthSource)
            {
                ResizeHeight = calculatedValue.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                ResizeWidth = calculatedValue.ToString(CultureInfo.InvariantCulture);
            }
        }
        finally
        {
            _isUpdatingLinkedDimension = false;
        }
    }

    private void ApplyResizeMode(string mode)
    {
        var normalizedMode = mode is "Stretch" or "Crop" ? mode : "Fit";
        var modeChanged = SetProperty(ref _selectedResizeMode, normalizedMode, nameof(SelectedResizeMode));
        var keepAspectRatio = string.Equals(normalizedMode, "Fit", StringComparison.OrdinalIgnoreCase);
        var aspectChanged = _keepAspectRatio != keepAspectRatio;

        if (aspectChanged)
        {
            _keepAspectRatio = keepAspectRatio;
            OnPropertyChanged(nameof(KeepAspectRatio));
        }

        if (!modeChanged && !aspectChanged)
        {
            return;
        }

        OnPropertyChanged(nameof(IsAspectRatioMode));
        if (keepAspectRatio)
        {
            UpdateLinkedDimensionFromCurrentInput();
        }

        RefreshResizeState();
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
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.2.0";
    }
}
