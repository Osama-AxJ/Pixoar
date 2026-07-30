using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Pixoar.App.Commands;
using Pixoar.App.Models;
using Pixoar.App.Services;
using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.App.ViewModels;

/// <summary>
/// Provides state and commands for the settings window.
/// </summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private const string WebsiteUrl = "https://oaj.sa";
    private const string GitHubUrl = "https://github.com/Osama-AxJ/Pixoar";
    private const string GitHubLicenseUrl = "https://github.com/Osama-AxJ/Pixoar/blob/main/LICENSE";
    private readonly ISettingsService _settingsService;
    private readonly IContextMenuService _contextMenuService;
    private readonly IPixoarCleanupService _cleanupService;
    private readonly IUpdateService _updateService;
    private readonly IApplicationPathProvider _pathProvider;
    private readonly IUserPromptService _userPromptService;
    private readonly IFileDialogService _fileDialogService;
    private PresetListItem? _selectedResizePreset;
    private bool _checkForUpdates = true;
    private bool _isCheckingForUpdates;
    private bool _saveBesideOriginal = true;
    private string _customOutputFolder = string.Empty;
    private bool _preventOverwrite = true;
    private bool _renameDuplicates = true;
    private string _selectedCompression = "DXT5";
    private bool _generateMipmaps = true;
    private bool _preserveAlpha = true;
    private bool _enableContextMenu = false;
    private bool _enableResizePresets = true;
    private bool _enableConvertPresets = true;
    private bool _enableImageInformation = true;
    private bool _enableOpenInPixoar = true;
    private string _statusText = "Settings ready.";
    private string _contextMenuInstallationStatusText = "Checking...";

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsViewModel"/> class.
    /// </summary>
    /// <param name="settingsService">The shared settings service.</param>
    /// <param name="contextMenuService">The Windows Explorer context menu service.</param>
    /// <param name="cleanupService">The cleanup service.</param>
    /// <param name="updateService">The update check service.</param>
    /// <param name="pathProvider">The app data path provider.</param>
    /// <param name="userPromptService">The user prompt service.</param>
    /// <param name="fileDialogService">The file dialog service.</param>
    /// <param name="formatDetector">The source of supported conversion formats.</param>
    public SettingsViewModel(
        ISettingsService settingsService,
        IContextMenuService contextMenuService,
        IPixoarCleanupService cleanupService,
        IUpdateService updateService,
        IApplicationPathProvider pathProvider,
        IUserPromptService userPromptService,
        IFileDialogService fileDialogService,
        IImageFormatDetector formatDetector)
    {
        _settingsService = settingsService;
        _contextMenuService = contextMenuService;
        _cleanupService = cleanupService;
        _updateService = updateService;
        _pathProvider = pathProvider;
        _userPromptService = userPromptService;
        _fileDialogService = fileDialogService;

        CompressionOptions = ["DXT1", "DXT3", "DXT5", "BC7", "Uncompressed"];

        var settings = settingsService.Current;
        _checkForUpdates = settings.General.CheckForUpdates;
        _saveBesideOriginal = settings.Output.SaveBesideOriginal;
        _customOutputFolder = settings.Output.CustomOutputFolder ?? string.Empty;
        _preventOverwrite = settings.Output.PreventOverwrite;
        _renameDuplicates = settings.Output.RenameDuplicatesAutomatically;
        _selectedCompression = FormatCompression(settings.Dds.Compression.ToString());
        _generateMipmaps = settings.Dds.GenerateMipmaps;
        _preserveAlpha = settings.Dds.PreserveAlpha;
        _enableContextMenu = settings.ContextMenu.EnableContextMenu;
        _enableResizePresets = settings.ContextMenu.EnableResizePresets;
        _enableConvertPresets = settings.ContextMenu.EnableConvertPresets;
        _enableImageInformation = settings.ContextMenu.EnableImageInformation;
        _enableOpenInPixoar = settings.ContextMenu.EnableOpenInPixoar;

        foreach (var preset in settings.ResizePresets)
        {
            ResizePresets.Add(new PresetListItem { Name = preset.Name, IsEnabled = preset.IsEnabled });
        }

        foreach (var preset in CreateConvertPresetItems(settings.ConvertPresets, formatDetector.SupportedFormats))
        {
            ConvertPresets.Add(preset);
        }

        AddResizePresetCommand = new RelayCommand(_ => AddResizePreset());
        RemoveResizePresetCommand = new RelayCommand(_ => RemoveResizePreset(), _ => SelectedResizePreset is not null);
        MoveResizePresetUpCommand = new RelayCommand(_ => MoveResizePreset(-1), _ => CanMoveResizePreset(-1));
        MoveResizePresetDownCommand = new RelayCommand(_ => MoveResizePreset(1), _ => CanMoveResizePreset(1));
        BrowseOutputFolderCommand = new RelayCommand(_ => BrowseOutputFolder());
        CheckForUpdatesNowCommand = new AsyncRelayCommand(_ => CheckForUpdatesNowAsync());
        CleanRegistryEntriesCommand = new AsyncRelayCommand(_ => CleanRegistryEntriesAsync());
        OpenSettingsFolderCommand = new RelayCommand(_ => OpenFolder(_pathProvider.AppDataDirectory));
        OpenLogsFolderCommand = new RelayCommand(_ => OpenFolder(_pathProvider.LogsDirectory));
        RemoveUserDataCommand = new AsyncRelayCommand(_ => RemoveUserDataAsync());
        OpenWebsiteCommand = new RelayCommand(_ => OpenPathOrUrl(WebsiteUrl));
        OpenGitHubCommand = new RelayCommand(_ => OpenPathOrUrl(GitHubUrl));
        ViewLicenseCommand = new RelayCommand(_ => ViewLicense());
        ApplyChangesCommand = new AsyncRelayCommand(_ => ApplyChangesAsync());
    }

    /// <summary>
    /// Gets DDS compression options.
    /// </summary>
    public IReadOnlyList<string> CompressionOptions { get; }

    /// <summary>
    /// Gets editable resize presets.
    /// </summary>
    public ObservableCollection<PresetListItem> ResizePresets { get; } = [];

    /// <summary>
    /// Gets supported conversion formats and their context-menu selection state.
    /// </summary>
    public ObservableCollection<PresetListItem> ConvertPresets { get; } = [];

    /// <summary>
    /// Gets the add resize preset command.
    /// </summary>
    public RelayCommand AddResizePresetCommand { get; }

    /// <summary>
    /// Gets the remove resize preset command.
    /// </summary>
    public RelayCommand RemoveResizePresetCommand { get; }

    /// <summary>
    /// Gets the move resize preset up command.
    /// </summary>
    public RelayCommand MoveResizePresetUpCommand { get; }

    /// <summary>
    /// Gets the move resize preset down command.
    /// </summary>
    public RelayCommand MoveResizePresetDownCommand { get; }

    /// <summary>
    /// Gets the browse output folder command.
    /// </summary>
    public RelayCommand BrowseOutputFolderCommand { get; }

    /// <summary>
    /// Gets the command that manually checks GitHub releases for updates.
    /// </summary>
    public AsyncRelayCommand CheckForUpdatesNowCommand { get; }

    /// <summary>
    /// Gets the command that removes Pixoar-owned registry entries.
    /// </summary>
    public AsyncRelayCommand CleanRegistryEntriesCommand { get; }

    /// <summary>
    /// Gets the command that opens the settings folder.
    /// </summary>
    public RelayCommand OpenSettingsFolderCommand { get; }

    /// <summary>
    /// Gets the command that opens the logs folder.
    /// </summary>
    public RelayCommand OpenLogsFolderCommand { get; }

    /// <summary>
    /// Gets the command that removes Pixoar user data.
    /// </summary>
    public AsyncRelayCommand RemoveUserDataCommand { get; }

    /// <summary>
    /// Gets the command that saves settings and applies Explorer context menu changes.
    /// </summary>
    public AsyncRelayCommand ApplyChangesCommand { get; }

    /// <summary>
    /// Gets the command that opens the Pixoar website.
    /// </summary>
    public RelayCommand OpenWebsiteCommand { get; }

    /// <summary>
    /// Gets the command that opens the Pixoar GitHub repository.
    /// </summary>
    public RelayCommand OpenGitHubCommand { get; }

    /// <summary>
    /// Gets the command that opens the Pixoar license.
    /// </summary>
    public RelayCommand ViewLicenseCommand { get; }

    /// <summary>
    /// Gets the application version text.
    /// </summary>
    public string VersionText => "0.1.0";

    /// <summary>
    /// Gets the license text.
    /// </summary>
    public string LicenseText => "GNU GPL v3.0";

    /// <summary>
    /// Gets the website text.
    /// </summary>
    public string WebsiteText => WebsiteUrl;

    /// <summary>
    /// Gets the GitHub repository text.
    /// </summary>
    public string GitHubText => GitHubUrl;

    /// <summary>
    /// Gets or sets the selected settings tab.
    /// </summary>
    public int SelectedPageIndex { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether update checks are enabled.
    /// </summary>
    public bool CheckForUpdates
    {
        get => _checkForUpdates;
        set => SetProperty(ref _checkForUpdates, value);
    }

    /// <summary>
    /// Gets a value indicating whether an update check is running.
    /// </summary>
    public bool IsCheckingForUpdates
    {
        get => _isCheckingForUpdates;
        private set
        {
            if (SetProperty(ref _isCheckingForUpdates, value))
            {
                OnPropertyChanged(nameof(CheckUpdatesButtonText));
            }
        }
    }

    /// <summary>
    /// Gets the manual update check button text.
    /// </summary>
    public string CheckUpdatesButtonText => IsCheckingForUpdates ? "Checking..." : "Check Now";

    /// <summary>
    /// Gets or sets a value indicating whether output should be saved beside original files.
    /// </summary>
    public bool SaveBesideOriginal
    {
        get => _saveBesideOriginal;
        set => SetProperty(ref _saveBesideOriginal, value);
    }

    /// <summary>
    /// Gets or sets the custom output folder text.
    /// </summary>
    public string CustomOutputFolder
    {
        get => _customOutputFolder;
        set => SetProperty(ref _customOutputFolder, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether overwrites should be prevented.
    /// </summary>
    public bool PreventOverwrite
    {
        get => _preventOverwrite;
        set => SetProperty(ref _preventOverwrite, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether duplicate names should be renamed.
    /// </summary>
    public bool RenameDuplicates
    {
        get => _renameDuplicates;
        set => SetProperty(ref _renameDuplicates, value);
    }

    /// <summary>
    /// Gets or sets the selected DDS compression mode.
    /// </summary>
    public string SelectedCompression
    {
        get => _selectedCompression;
        set => SetProperty(ref _selectedCompression, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether DDS mipmaps should be generated.
    /// </summary>
    public bool GenerateMipmaps
    {
        get => _generateMipmaps;
        set => SetProperty(ref _generateMipmaps, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether DDS alpha should be preserved.
    /// </summary>
    public bool PreserveAlpha
    {
        get => _preserveAlpha;
        set => SetProperty(ref _preserveAlpha, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether context menu entries are enabled.
    /// </summary>
    public bool EnableContextMenu
    {
        get => _enableContextMenu;
        set
        {
            if (SetProperty(ref _enableContextMenu, value))
            {
                StatusText = value
                    ? "Context menu will be enabled when changes are applied."
                    : "Context menu will be removed when changes are applied.";
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether resize presets appear in the context menu.
    /// </summary>
    public bool EnableResizePresets
    {
        get => _enableResizePresets;
        set => SetProperty(ref _enableResizePresets, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether convert presets appear in the context menu.
    /// </summary>
    public bool EnableConvertPresets
    {
        get => _enableConvertPresets;
        set => SetProperty(ref _enableConvertPresets, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether Image Information appears in the context menu.
    /// </summary>
    public bool EnableImageInformation
    {
        get => _enableImageInformation;
        set => SetProperty(ref _enableImageInformation, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether Open in Pixoar appears in the context menu.
    /// </summary>
    public bool EnableOpenInPixoar
    {
        get => _enableOpenInPixoar;
        set => SetProperty(ref _enableOpenInPixoar, value);
    }

    /// <summary>
    /// Gets or sets the selected resize preset.
    /// </summary>
    public PresetListItem? SelectedResizePreset
    {
        get => _selectedResizePreset;
        set
        {
            if (SetProperty(ref _selectedResizePreset, value))
            {
                RefreshResizeCommands();
            }
        }
    }

    /// <summary>
    /// Gets or sets settings status text.
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    /// <summary>
    /// Gets the current user-facing context menu installation status.
    /// </summary>
    public string ContextMenuInstallationStatusText
    {
        get => _contextMenuInstallationStatusText;
        private set => SetProperty(ref _contextMenuInstallationStatusText, value);
    }

    /// <summary>
    /// Loads the current context menu installation status for display.
    /// </summary>
    public async Task LoadContextMenuInstallationStatusAsync()
    {
        try
        {
            SetContextMenuInstallationStatus(await _contextMenuService.GetInstallationStatusAsync());
        }
        catch
        {
            ContextMenuInstallationStatusText = "Status Unavailable";
            StatusText = "Could not check the context menu status. Check logs for details.";
        }
    }

    private void AddResizePreset()
    {
        var preset = new PresetListItem { Name = GetNextResizePresetName() };
        ResizePresets.Add(preset);
        SelectedResizePreset = preset;
        StatusText = "Resize preset added.";
    }

    private void RemoveResizePreset()
    {
        if (SelectedResizePreset is null)
        {
            return;
        }

        ResizePresets.Remove(SelectedResizePreset);
        SelectedResizePreset = ResizePresets.FirstOrDefault();
        StatusText = "Resize preset removed.";
    }

    private void MoveResizePreset(int direction)
    {
        if (SelectedResizePreset is null)
        {
            return;
        }

        var index = ResizePresets.IndexOf(SelectedResizePreset);
        var targetIndex = index + direction;
        ResizePresets.Move(index, targetIndex);
        RefreshResizeCommands();
        StatusText = "Resize preset order updated.";
    }

    private bool CanMoveResizePreset(int direction)
    {
        if (SelectedResizePreset is null)
        {
            return false;
        }

        var targetIndex = ResizePresets.IndexOf(SelectedResizePreset) + direction;
        return targetIndex >= 0 && targetIndex < ResizePresets.Count;
    }

    private void BrowseOutputFolder()
    {
        var folder = _fileDialogService.SelectFolder();
        if (!string.IsNullOrWhiteSpace(folder))
        {
            CustomOutputFolder = folder;
            StatusText = "Output folder selected.";
        }
    }

    private async Task CheckForUpdatesNowAsync()
    {
        IsCheckingForUpdates = true;
        StatusText = "Checking for updates...";

        try
        {
            var result = await _updateService.CheckForUpdatesAsync();
            _userPromptService.ShowManualUpdateCheckResult(result);
            StatusText = result.Status switch
            {
                UpdateStatus.UpdateAvailable => "Update available.",
                UpdateStatus.UpToDate => "Pixoar is up to date.",
                _ => "Update check failed."
            };
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    private async Task CleanRegistryEntriesAsync()
    {
        var result = await _cleanupService.CleanupAsync(removeUserData: false);
        await LoadContextMenuInstallationStatusAsync();
        StatusText = result.Success
            ? "Pixoar registry entries cleaned."
            : "Cleanup completed with issues. Check logs.";
    }

    private async Task RemoveUserDataAsync()
    {
        if (!_userPromptService.ConfirmRemoveUserData())
        {
            StatusText = "User data removal canceled.";
            return;
        }

        var result = await _cleanupService.CleanupAsync(removeUserData: true);
        StatusText = result.Success
            ? "Pixoar user data removed."
            : "User data cleanup completed with issues. Check logs.";
    }

    private void OpenFolder(string folder)
    {
        Directory.CreateDirectory(folder);
        OpenPathOrUrl(folder);
    }

    private void OpenPathOrUrl(string pathOrUrl)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = pathOrUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusText = $"Could not open link: {ex.Message}";
        }
    }

    private void ViewLicense()
    {
        OpenPathOrUrl(FindLocalLicenseFile() ?? GitHubLicenseUrl);
    }

    private static string? FindLocalLicenseFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "LICENSE");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private async Task ApplyChangesAsync()
    {
        StatusText = "Applying changes...";

        try
        {
            await SaveCurrentSettingsAsync();
        }
        catch
        {
            StatusText = "Settings could not be saved.";
            return;
        }

        try
        {
            var installationStatus = await _contextMenuService.ApplyAsync();
            SetContextMenuInstallationStatus(installationStatus);

            StatusText = installationStatus switch
            {
                ContextMenuInstallationStatus.Installed => "Settings saved. Context menu installed.",
                ContextMenuInstallationStatus.NotInstalled when !EnableContextMenu =>
                    "Settings saved. Context menu not installed.",
                ContextMenuInstallationStatus.NeedsRepair =>
                    "Settings saved, but the context menu needs repair. Apply changes again.",
                _ => "Settings saved, but the context menu could not be installed."
            };
        }
        catch
        {
            await LoadContextMenuInstallationStatusAsync();
            StatusText = "Settings were saved, but context menu changes could not be applied. Check logs for details.";
        }
    }

    private void SetContextMenuInstallationStatus(ContextMenuInstallationStatus status)
    {
        ContextMenuInstallationStatusText = status switch
        {
            ContextMenuInstallationStatus.Installed => "Installed",
            ContextMenuInstallationStatus.NeedsRepair => "Needs Repair",
            _ => "Not Installed"
        };
    }

    private Task SaveCurrentSettingsAsync()
    {
        return _settingsService.UpdateAsync(settings =>
        {
            settings.General.CheckForUpdates = CheckForUpdates;

            settings.Output.SaveBesideOriginal = SaveBesideOriginal;
            settings.Output.CustomOutputFolder = string.IsNullOrWhiteSpace(CustomOutputFolder)
                ? null
                : CustomOutputFolder.Trim();
            settings.Output.PreventOverwrite = PreventOverwrite;
            settings.Output.RenameDuplicatesAutomatically = RenameDuplicates;

            settings.Dds.Compression = ParseCompression(SelectedCompression);
            settings.Dds.GenerateMipmaps = GenerateMipmaps;
            settings.Dds.PreserveAlpha = PreserveAlpha;

            settings.ContextMenu.EnableContextMenu = EnableContextMenu;
            settings.ContextMenu.EnableResizePresets = EnableResizePresets;
            settings.ContextMenu.EnableConvertPresets = EnableConvertPresets;
            settings.ContextMenu.EnableImageInformation = EnableImageInformation;
            settings.ContextMenu.EnableOpenInPixoar = EnableOpenInPixoar;

            settings.ResizePresets = ResizePresets.Select(CreateResizePreset).ToList();
            settings.ConvertPresets = ConvertPresets.Select(CreateConvertPreset).ToList();
        });
    }

    private void RefreshResizeCommands()
    {
        RemoveResizePresetCommand.NotifyCanExecuteChanged();
        MoveResizePresetUpCommand.NotifyCanExecuteChanged();
        MoveResizePresetDownCommand.NotifyCanExecuteChanged();
    }

    private static string FormatCompression(string value)
    {
        return value switch
        {
            "Dxt1" => "DXT1",
            "Dxt3" => "DXT3",
            "Dxt5" => "DXT5",
            "Bc7" => "BC7",
            _ => value
        };
    }

    private static DdsCompressionMode ParseCompression(string value)
    {
        return value switch
        {
            "DXT1" => DdsCompressionMode.Dxt1,
            "DXT3" => DdsCompressionMode.Dxt3,
            "DXT5" => DdsCompressionMode.Dxt5,
            "BC7" => DdsCompressionMode.Bc7,
            "Uncompressed" => DdsCompressionMode.Uncompressed,
            _ => DdsCompressionMode.Dxt5
        };
    }

    private static ResizePreset CreateResizePreset(PresetListItem item)
    {
        var name = string.IsNullOrWhiteSpace(item.Name) ? "50%" : item.Name.Trim();
        var preset = new ResizePreset
        {
            Name = name,
            IsEnabled = item.IsEnabled
        };

        if (TryParseResizePercentage(name, out var percentage))
        {
            preset.Name = $"{percentage}%";
            preset.Percentage = percentage;
        }

        return preset;
    }

    private static ConvertPreset CreateConvertPreset(PresetListItem item)
    {
        var name = string.IsNullOrWhiteSpace(item.Name) ? "PNG" : item.Name.Trim();
        return new ConvertPreset
        {
            Name = name.ToUpperInvariant(),
            Format = name.TrimStart('.').ToLowerInvariant(),
            IsEnabled = item.IsEnabled
        };
    }

    private static IReadOnlyList<PresetListItem> CreateConvertPresetItems(
        IEnumerable<ConvertPreset> presets,
        IReadOnlyList<ImageFormatDescriptor> supportedFormats)
    {
        var canonicalFormats = supportedFormats
            .Select(format => format.PrimaryExtension.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(GetConvertFormatOrder)
            .ThenBy(format => format, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var canonicalByAlias = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var descriptor in supportedFormats)
        {
            var canonical = descriptor.PrimaryExtension.ToUpperInvariant();
            canonicalByAlias[canonical] = canonical;
            canonicalByAlias[descriptor.DisplayName] = canonical;

            foreach (var extension in descriptor.Extensions)
            {
                canonicalByAlias[extension.TrimStart('.')] = canonical;
            }
        }

        var enabledByFormat = canonicalFormats.ToDictionary(
            format => format,
            _ => false,
            StringComparer.OrdinalIgnoreCase);

        foreach (var preset in presets)
        {
            var value = string.IsNullOrWhiteSpace(preset.Format) ? preset.Name : preset.Format;
            var alias = value.Trim().TrimStart('.');
            if (canonicalByAlias.TryGetValue(alias, out var canonical))
            {
                enabledByFormat[canonical] |= preset.IsEnabled;
            }
        }

        return canonicalFormats
            .Select(format => new PresetListItem
            {
                Name = format,
                IsEnabled = enabledByFormat[format]
            })
            .ToArray();
    }

    private static int GetConvertFormatOrder(string format)
    {
        return format.ToUpperInvariant() switch
        {
            "PNG" => 0,
            "JPG" => 1,
            "WEBP" => 2,
            "BMP" => 3,
            "TIFF" => 4,
            "DDS" => 5,
            _ => int.MaxValue
        };
    }

    private static bool TryParseResizePercentage(string value, out int percentage)
    {
        return int.TryParse(value.Trim().TrimEnd('%'), out percentage)
            && value.Trim().EndsWith('%')
            && percentage > 0;
    }

    private string GetNextResizePresetName()
    {
        var existing = ResizePresets
            .Select(preset => preset.Name.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in new[] { "50%", "75%", "25%" })
        {
            if (!existing.Contains(candidate))
            {
                return candidate;
            }
        }

        return "50%";
    }
}
