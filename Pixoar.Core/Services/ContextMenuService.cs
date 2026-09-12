using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.Core.Services;

internal sealed class ContextMenuService(
    ISettingsService settingsService,
    IApplicationLogger logger,
    IDdsDependencyService ddsDependencyService,
    IApplicationPathProvider pathProvider) : IContextMenuService
{
    private const string MenuKeyName = "Pixoar";
    private const string InfoMenuKeyName = "PixoarInfo";
    private const string InfoMenuDisplayName = "Pixoar Info";
    private const string LegacyDdsMenuKeyName = "PixoarDds";
    private const int StaticCascadeEntryLimit = 16;
    private const string SystemFileAssociationsPath = @"Software\Classes\SystemFileAssociations";
    private const string ImageFileAssociation = "image";
    private const string AppExecutableName = "Pixoar.exe";
    private const string CliExecutableName = "Pixoar.Cli.exe";
    private const string ContextMenuIconRelativePath = @"Resources\Assets\Branding\pixoar.ico";

    private static readonly string[] SupportedExtensions =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".bmp",
        ".tif",
        ".tiff",
        ".dds"
    ];

    private static readonly string[] MenuKeyNames =
    [
        MenuKeyName,
        InfoMenuKeyName,
        LegacyDdsMenuKeyName
    ];

    private static readonly (string DisplayName, string FormatArgument)[] DdsCompressionCommands =
    [
        ("DXT1", "dds-dxt1"),
        ("DXT3", "dds-dxt3"),
        ("DXT5", "dds-dxt5"),
        ("BC7", "dds-bc7"),
        ("Uncompressed", "dds-uncompressed")
    ];

    public async Task<ContextMenuInstallationStatus> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var status = await GetInstallationStatusAsync(cancellationToken).ConfigureAwait(false);

        if (settingsService.Current.ContextMenu.EnableContextMenu)
        {
            if (status is not ContextMenuInstallationStatus.Installed)
            {
                await RefreshAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        else if (status is not ContextMenuInstallationStatus.NotInstalled)
        {
            await UninstallAsync(cancellationToken).ConfigureAwait(false);
        }

        return await GetInstallationStatusAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task InstallAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows Explorer context menus are only supported on Windows.");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var settings = settingsService.Current;

            if (!settings.ContextMenu.EnableContextMenu)
            {
                await UninstallAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            var executablePaths = ResolveExecutablePaths();
            var texconvPath = ddsDependencyService.ResolveTexconvPath();
            var writtenCommands = new List<string>();

            await logger.LogInformationAsync($"Context menu resolved app path: {executablePaths.AppPath}", cancellationToken).ConfigureAwait(false);
            await logger.LogInformationAsync($"Context menu resolved CLI path: {executablePaths.CliPath}", cancellationToken).ConfigureAwait(false);
            await logger.LogInformationAsync($"Context menu resolved icon path: {executablePaths.ContextMenuIconPath}", cancellationToken).ConfigureAwait(false);
            await logger.LogInformationAsync($"Context menu resolved texconv path: {texconvPath ?? "<not found>"}", cancellationToken).ConfigureAwait(false);

            foreach (var removedMenuPath in RemoveForImageFiles())
            {
                await logger.LogInformationAsync(
                    $@"Registry key removed: HKEY_CURRENT_USER\{removedMenuPath}",
                    cancellationToken).ConfigureAwait(false);
            }

            foreach (var extension in SupportedExtensions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var removedMenuPath in RemoveForExtension(extension))
                {
                    await logger.LogInformationAsync(
                        $@"Registry key removed: HKEY_CURRENT_USER\{removedMenuPath}",
                        cancellationToken).ConfigureAwait(false);
                }

                foreach (var registration in CreateExpectedMenuKeys(settings, executablePaths, extension))
                {
                    writtenCommands.AddRange(InstallForExtension(extension, registration));
                    await logger.LogInformationAsync(
                        $@"Registry key created: HKEY_CURRENT_USER\{GetMenuKeyPath(extension, registration.MenuKeyName)}",
                        cancellationToken).ConfigureAwait(false);
                }
            }

            NotifyShellAssociationChanged();

            foreach (var command in writtenCommands)
            {
                await logger.LogInformationAsync($"Registry command written: {command}", cancellationToken).ConfigureAwait(false);
            }

            await logger.LogInformationAsync("Installed Windows Explorer context menu entries.", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync("Failed to install Windows Explorer context menu entries.", ex, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("Pixoar could not install the Windows Explorer context menu entries.", ex);
        }
    }

    public async Task UninstallAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows Explorer context menus are only supported on Windows.");
        }

        try
        {
            var removedKeys = new List<string>();
            removedKeys.AddRange(RemoveForImageFiles().Select(path => $@"HKEY_CURRENT_USER\{path}"));

            foreach (var extension in SupportedExtensions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                removedKeys.AddRange(RemoveForExtension(extension).Select(path => $@"HKEY_CURRENT_USER\{path}"));
            }

            foreach (var registryKey in removedKeys)
            {
                await logger.LogInformationAsync(
                    $"Registry key removed: {registryKey}",
                    cancellationToken).ConfigureAwait(false);
            }

            NotifyShellAssociationChanged();

            await logger.LogInformationAsync("Removed Windows Explorer context menu entries.", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync("Failed to remove Windows Explorer context menu entries.", ex, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("Pixoar could not remove the Windows Explorer context menu entries.", ex);
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await UninstallAsync(cancellationToken).ConfigureAwait(false);

        if (settingsService.Current.ContextMenu.EnableContextMenu)
        {
            await InstallAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<ContextMenuDiagnosticReport> RepairAsync(CancellationToken cancellationToken = default)
    {
        await RefreshAsync(cancellationToken).ConfigureAwait(false);
        var report = await DiagnoseAsync(cancellationToken).ConfigureAwait(false);

        if (report.IsValid)
        {
            await logger.LogInformationAsync("Context menu repair completed successfully.", cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await logger.LogWarningAsync("Context menu repair completed with diagnostic issues.", cancellationToken).ConfigureAwait(false);
        }

        return report;
    }

    public async Task<ContextMenuDiagnosticReport> DiagnoseAsync(CancellationToken cancellationToken = default)
    {
        var report = CreateBaseDiagnosticReport();

        if (!OperatingSystem.IsWindows())
        {
            report.Issues.Add("Windows Explorer context menus are only supported on Windows.");
            return report;
        }

        var installationStatus = await GetInstallationStatusAsync(cancellationToken).ConfigureAwait(false);
        if (installationStatus is not ContextMenuInstallationStatus.Installed)
        {
            report.Issues.Add(
                $"The context menu installation status is {installationStatus}; the complete registry tree is not valid.");
        }

        var settings = settingsService.Current;
        var executablePaths = CreateInstalledExecutablePaths();
        if (settings.ContextMenu.EnableContextMenu)
        {
            report.Issues.AddRange(GetStaticMenuBudgetIssues(settings, executablePaths));
        }

        foreach (var extension in SupportedExtensions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var registration in CreateExpectedMenuKeys(settings, executablePaths, extension))
            {
                var entryCount = CountStaticCascadeEntries(registration.ExpectedKey);
                if (entryCount > StaticCascadeEntryLimit)
                {
                    report.Issues.Add(
                        $"{registration.DisplayName} for {extension} has {entryCount} static menu entries; Windows Explorer displays at most {StaticCascadeEntryLimit}.");
                }

                report.Commands.AddRange(ReadCommandDiagnostics(
                    extension,
                    registration.MenuKeyName,
                    registration.DiagnosticActionPath,
                    report.AppExecutablePath,
                    report.ContextMenuIconPath));
            }
        }

        if (report.Commands.Count == 0)
        {
            report.Issues.Add("No Pixoar context menu registry commands were found.");
        }

        await LogDiagnosticReportAsync(report, cancellationToken).ConfigureAwait(false);
        return report;
    }

    public async Task<ContextMenuInstallationStatus> GetInstallationStatusAsync(
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return ContextMenuInstallationStatus.NotInstalled;
        }

        try
        {
            var settings = settingsService.Current;
            var executablePaths = CreateInstalledExecutablePaths();
            var installedExtensions = new List<string>();
            var issues = new List<string>();
            var hasSharedMenuRegistration = HasMenuRegistration(ImageFileAssociation);
            var hasAnyMenuRegistration = hasSharedMenuRegistration;

            foreach (var extension in SupportedExtensions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var expectedRegistrations = settings.ContextMenu.EnableContextMenu
                    ? CreateExpectedMenuKeys(settings, executablePaths, extension)
                    : [];
                var expectedByMenuKeyName = expectedRegistrations.ToDictionary(
                    registration => registration.MenuKeyName,
                    StringComparer.OrdinalIgnoreCase);
                var presentMenuKeyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var menuKeyName in MenuKeyNames)
                {
                    var menuKeyPath = GetMenuKeyPath(extension, menuKeyName);
                    using var menuKey = Registry.CurrentUser.OpenSubKey(menuKeyPath, writable: false);
                    if (menuKey is null)
                    {
                        continue;
                    }

                    hasAnyMenuRegistration = true;
                    presentMenuKeyNames.Add(menuKeyName);
                    if (menuKeyName.Equals(MenuKeyName, StringComparison.OrdinalIgnoreCase))
                    {
                        installedExtensions.Add(extension);
                    }

                    if (!settings.ContextMenu.EnableContextMenu)
                    {
                        continue;
                    }

                    if (!expectedByMenuKeyName.TryGetValue(menuKeyName, out var registration))
                    {
                        issues.Add($@"HKEY_CURRENT_USER\{menuKeyPath} is not expected for the current Quick Actions settings.");
                        continue;
                    }

                    ValidateRegistryKey(
                        menuKey,
                        registration.ExpectedKey,
                        $@"HKEY_CURRENT_USER\{menuKeyPath}",
                        issues);
                }

                if (settings.ContextMenu.EnableContextMenu)
                {
                    foreach (var registration in expectedRegistrations.Where(registration =>
                                 !presentMenuKeyNames.Contains(registration.MenuKeyName)))
                    {
                        issues.Add(
                            $"The context menu registry tree is missing for {extension}: {registration.MenuKeyName}.");
                    }
                }
            }

            ContextMenuInstallationStatus status;
            if (!hasAnyMenuRegistration)
            {
                status = ContextMenuInstallationStatus.NotInstalled;
            }
            else if (!settings.ContextMenu.EnableContextMenu)
            {
                issues.Add(
                    "Context menu registration remains while the context menu setting is disabled.");
                status = ContextMenuInstallationStatus.NeedsRepair;
            }
            else
            {
                issues.AddRange(GetExecutablePathIssues(executablePaths));
                issues.AddRange(GetStaticMenuBudgetIssues(settings, executablePaths));

                if (hasSharedMenuRegistration)
                {
                    issues.Add("The legacy shared image context menu registry tree remains.");
                }

                foreach (var extension in SupportedExtensions.Except(
                    installedExtensions,
                    StringComparer.OrdinalIgnoreCase))
                {
                    issues.Add($"The context menu registry tree is missing for {extension}.");
                }

                status = issues.Count == 0
                    ? ContextMenuInstallationStatus.Installed
                    : ContextMenuInstallationStatus.NeedsRepair;
            }

            await LogInstallationStatusAsync(status, issues, cancellationToken).ConfigureAwait(false);
            return status;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await logger.LogErrorAsync(
                "Failed to verify Windows Explorer context menu entries.",
                ex,
                cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                "Pixoar could not verify the Windows Explorer context menu entries.",
                ex);
        }
    }

    private ContextMenuDiagnosticReport CreateBaseDiagnosticReport()
    {
        var executablePaths = CreateInstalledExecutablePaths();
        var validationIssues = GetExecutablePathIssues(executablePaths);
        var texconvPath = ddsDependencyService.ResolveTexconvPath();

        var report = new ContextMenuDiagnosticReport
        {
            AppExecutablePath = executablePaths.AppPath,
            AppExecutableExists = File.Exists(executablePaths.AppPath),
            CliExecutablePath = executablePaths.CliPath,
            CliExecutableExists = File.Exists(executablePaths.CliPath),
            ContextMenuIconPath = executablePaths.ContextMenuIconPath,
            ContextMenuIconExists = File.Exists(executablePaths.ContextMenuIconPath),
            TexconvPath = texconvPath ?? string.Empty,
            TexconvExists = texconvPath is not null && File.Exists(texconvPath),
            SettingsFilePath = pathProvider.SettingsFilePath,
            CurrentDirectory = Environment.CurrentDirectory,
            AppContextBaseDirectory = AppContext.BaseDirectory
        };

        report.Issues.AddRange(validationIssues);

        if (!report.TexconvExists)
        {
            report.Issues.Add("texconv.exe could not be resolved. DDS context menu conversion will fail.");
        }

        return report;
    }

    private async Task LogDiagnosticReportAsync(ContextMenuDiagnosticReport report, CancellationToken cancellationToken)
    {
        await logger.LogInformationAsync(report.ToDisplayText(), cancellationToken).ConfigureAwait(false);

        foreach (var command in report.Commands)
        {
            var message =
                $"Context menu command diagnostic: {command.Extension} {command.ActionPath} | " +
                $"Key: {command.RegistryKeyPath} | Command: {command.Command} | " +
                $"Executable: {command.ExecutablePath} | Exists: {command.ExecutableExists} | " +
                $"Icon: {command.IconPath} | Icon exists: {command.IconExists} | " +
                $"Args: {command.Arguments} | Placeholder: {command.SelectedFilePlaceholder} | " +
                $"Valid: {command.IsValid}";

            if (!string.IsNullOrWhiteSpace(command.Issue))
            {
                message += $" | Issue: {command.Issue}";
            }

            await logger.LogInformationAsync(message, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task LogInstallationStatusAsync(
        ContextMenuInstallationStatus status,
        IReadOnlyCollection<string> issues,
        CancellationToken cancellationToken)
    {
        await logger.LogInformationAsync(
            $"Context menu installation status: {status}.",
            cancellationToken).ConfigureAwait(false);

        foreach (var issue in issues)
        {
            await logger.LogWarningAsync(
                $"Context menu registry verification: {issue}",
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static IReadOnlyList<ContextMenuRegistration> CreateExpectedMenuKeys(
        PixoarSettings settings,
        ContextMenuExecutablePaths executablePaths,
        string extension)
    {
        var enabledDdsCommands = GetEnabledDdsCompressionCommands(settings);
        var ddsCapacity = GetDdsCompressionShortcutCapacity(settings, executablePaths);
        var visibleDdsCommands = enabledDdsCommands.Take(ddsCapacity).ToArray();
        var registrations = new List<ContextMenuRegistration>
        {
            new(MenuKeyName, "Pixoar", [], CreateExpectedMenuKey(settings, executablePaths, extension, visibleDdsCommands))
        };

        var infoMenuKey = CreateExpectedInfoMenuKey(settings, executablePaths);
        if (infoMenuKey is not null)
        {
            registrations.Add(new(InfoMenuKeyName, InfoMenuDisplayName, ["Info"], infoMenuKey));
        }

        return registrations;
    }

    private static ExpectedRegistryKey CreateExpectedMenuKey(
        PixoarSettings settings,
        ContextMenuExecutablePaths executablePaths,
        string extension,
        IReadOnlyList<(string DisplayName, string FormatArgument)> ddsCompressionCommands)
    {
        // Windows Explorer renders at most 16 descendants in a registry-backed
        // static cascade. Keep this menu below that limit so every normal image
        // action, including Upscale, remains reachable.
        var menuKey = CreateExpectedSubmenu("Pixoar", executablePaths.ContextMenuIconPath);
        var menuShellKey = menuKey.AddSubKey("shell");

        var enabledPercentages = settings.ContextMenu.EnableResizePresets
            ? settings.ResizePresets
                .Where(preset => preset.IsEnabled)
                .Select(GetResizePresetPercentage)
                .Where(percentage => percentage is > 0)
                .Select(percentage => percentage.GetValueOrDefault())
                .ToArray()
            : [];
        if (enabledPercentages.Length > 0)
        {
            var resizeKey = CreateExpectedSubmenu("Resize", executablePaths.ContextMenuIconPath);
            menuShellKey.SubKeys.Add("10_Resize", resizeKey);
            var resizeShellKey = resizeKey.AddSubKey("shell");

            for (var index = 0; index < enabledPercentages.Length; index++)
            {
                var percentage = enabledPercentages[index];
                var displayName = $"{percentage}%";
                var keyName = $"{index + 1:00}_{CreateRegistryKeyName(displayName)}";
                AddExpectedCommand(
                    resizeShellKey,
                    keyName,
                    displayName,
                    executablePaths.AppPath,
                    $"--explorer-batch resize --percentage {percentage}",
                    executablePaths.ContextMenuIconPath);
            }
        }

        var enabledConvertPresets = settings.ContextMenu.EnableConvertPresets
            ? GetEnabledConvertPresets(settings)
                .Where(preset => !GetConvertPresetArgument(preset).Equals("dds", StringComparison.OrdinalIgnoreCase))
                .ToArray()
            : [];
        if (enabledConvertPresets.Length > 0 || ddsCompressionCommands.Count > 0)
        {
            var convertKey = CreateExpectedSubmenu("Convert", executablePaths.ContextMenuIconPath);
            menuShellKey.SubKeys.Add("20_Convert", convertKey);
            var convertShellKey = convertKey.AddSubKey("shell");

            for (var index = 0; index < enabledConvertPresets.Length; index++)
            {
                var preset = enabledConvertPresets[index];
                var formatArgument = GetConvertPresetArgument(preset);
                var displayName = string.IsNullOrWhiteSpace(preset.Name)
                    ? formatArgument.ToUpperInvariant()
                    : preset.Name;
                var keyName = $"{index + 1:00}_{CreateRegistryKeyName(displayName)}";
                AddExpectedCommand(
                    convertShellKey,
                    keyName,
                    displayName,
                    executablePaths.AppPath,
                    $"--explorer-batch convert --format {formatArgument}",
                    executablePaths.ContextMenuIconPath);
            }

            if (ddsCompressionCommands.Count > 0)
            {
                var ddsKey = CreateExpectedSubmenu("DDS", executablePaths.ContextMenuIconPath);
                convertShellKey.SubKeys.Add("10_DDS", ddsKey);
                var ddsShellKey = ddsKey.AddSubKey("shell");

                for (var compressionIndex = 0; compressionIndex < ddsCompressionCommands.Count; compressionIndex++)
                {
                    var command = ddsCompressionCommands[compressionIndex];
                    AddExpectedCommand(
                        ddsShellKey,
                        $"{compressionIndex + 1:00}_{CreateRegistryKeyName(command.DisplayName)}",
                        command.DisplayName,
                        executablePaths.AppPath,
                        $"--explorer-batch convert --format {command.FormatArgument}",
                        executablePaths.ContextMenuIconPath);
                }
            }
        }

        if (settings.ContextMenu.EnableUpscale && !extension.Equals(".dds", StringComparison.OrdinalIgnoreCase))
        {
            var upscaleKey = CreateExpectedSubmenu("Upscale", executablePaths.ContextMenuIconPath);
            menuShellKey.SubKeys.Add("50_Upscale", upscaleKey);
            var upscaleShellKey = upscaleKey.AddSubKey("shell");
            AddExpectedCommand(upscaleShellKey, "01_2x", "2x", executablePaths.AppPath, "--explorer-batch upscale --scale 2", executablePaths.ContextMenuIconPath);
            AddExpectedCommand(upscaleShellKey, "02_4x", "4x", executablePaths.AppPath, "--explorer-batch upscale --scale 4", executablePaths.ContextMenuIconPath);
        }

        return menuKey;
    }

    private static ExpectedRegistryKey? CreateExpectedInfoMenuKey(
        PixoarSettings settings,
        ContextMenuExecutablePaths executablePaths)
    {
        if (!settings.ContextMenu.EnableImageInformation && !settings.ContextMenu.EnableOpenInPixoar)
        {
            return null;
        }

        var infoMenuKey = CreateExpectedSubmenu(InfoMenuDisplayName, executablePaths.ContextMenuIconPath);
        var infoShellKey = infoMenuKey.AddSubKey("shell");
        if (settings.ContextMenu.EnableImageInformation)
        {
            AddExpectedCommand(
                infoShellKey,
                "01_ImageInformation",
                "Image Information",
                executablePaths.AppPath,
                "--info",
                executablePaths.ContextMenuIconPath);
        }

        if (settings.ContextMenu.EnableOpenInPixoar)
        {
            AddExpectedCommand(
                infoShellKey,
                "02_OpenInPixoar",
                "Open in Pixoar",
                executablePaths.AppPath,
                "--open",
                executablePaths.ContextMenuIconPath);
        }

        return infoMenuKey;
    }

    private static ExpectedRegistryKey CreateExpectedSubmenu(string displayName, string iconPath)
    {
        var key = new ExpectedRegistryKey();
        key.Values.Add("MUIVerb", displayName);
        key.Values.Add("Icon", iconPath);
        key.Values.Add("SubCommands", string.Empty);
        key.Values.Add("MultiSelectModel", "Player");
        return key;
    }

    private static void AddExpectedCommand(
        ExpectedRegistryKey parentKey,
        string keyName,
        string displayName,
        string executablePath,
        string argumentsBeforeFile,
        string iconPath)
    {
        var commandKey = new ExpectedRegistryKey();
        commandKey.Values.Add("MUIVerb", displayName);
        commandKey.Values.Add("Icon", iconPath);
        commandKey.Values.Add("MultiSelectModel", "Player");

        var commandValueKey = commandKey.AddSubKey("command");
        commandValueKey.Values.Add(
            string.Empty,
            $"\"{executablePath}\" {argumentsBeforeFile} \"%1\"");
        parentKey.SubKeys.Add(keyName, commandKey);
    }

    [SupportedOSPlatform("windows")]
    private static void ValidateRegistryKey(
        RegistryKey registryKey,
        ExpectedRegistryKey expectedKey,
        string registryPath,
        List<string> issues)
    {
        var actualValueNames = registryKey
            .GetValueNames()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (valueName, expectedValue) in expectedKey.Values)
        {
            if (!actualValueNames.Contains(valueName))
            {
                issues.Add(
                    $"{registryPath} is missing the {FormatRegistryValueName(valueName)} value.");
                continue;
            }

            var actualValue = registryKey.GetValue(
                valueName,
                defaultValue: null,
                RegistryValueOptions.DoNotExpandEnvironmentNames);

            if (actualValue is not string actualText ||
                !string.Equals(actualText, expectedValue, StringComparison.Ordinal))
            {
                issues.Add(
                    $"{registryPath} has an unexpected {FormatRegistryValueName(valueName)} value.");
            }
        }

        foreach (var valueName in actualValueNames.Except(
            expectedKey.Values.Keys,
            StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(
                $"{registryPath} contains the unexpected {FormatRegistryValueName(valueName)} value.");
        }

        var actualSubKeyNames = registryKey
            .GetSubKeyNames()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (subKeyName, expectedSubKey) in expectedKey.SubKeys)
        {
            if (!actualSubKeyNames.Contains(subKeyName))
            {
                issues.Add($"{registryPath} is missing the {subKeyName} key.");
                continue;
            }

            using var subKey = registryKey.OpenSubKey(subKeyName, writable: false);
            if (subKey is null)
            {
                issues.Add($"{registryPath} could not read the {subKeyName} key.");
                continue;
            }

            ValidateRegistryKey(
                subKey,
                expectedSubKey,
                $@"{registryPath}\{subKeyName}",
                issues);
        }

        foreach (var subKeyName in actualSubKeyNames.Except(
            expectedKey.SubKeys.Keys,
            StringComparer.OrdinalIgnoreCase))
        {
            issues.Add($"{registryPath} contains the unexpected {subKeyName} key.");
        }
    }

    private static string FormatRegistryValueName(string valueName)
    {
        return string.IsNullOrEmpty(valueName) ? "(Default)" : valueName;
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<string> InstallForExtension(
        string extension,
        ContextMenuRegistration registration)
    {
        var writtenCommands = new List<string>();

        using var shellKey = Registry.CurrentUser.CreateSubKey(GetShellKeyPath(extension), writable: true)
            ?? throw new InvalidOperationException($"Could not create Explorer shell key for {extension}.");

        shellKey.DeleteSubKeyTree(registration.MenuKeyName, throwOnMissingSubKey: false);

        using var menuKey = shellKey.CreateSubKey(registration.MenuKeyName, writable: true)
            ?? throw new InvalidOperationException($"Could not create Pixoar menu key for {extension}.");

        WriteRegistryKey(menuKey, registration.ExpectedKey, writtenCommands);

        return writtenCommands;
    }

    [SupportedOSPlatform("windows")]
    private static void WriteRegistryKey(
        RegistryKey registryKey,
        ExpectedRegistryKey expectedKey,
        List<string> writtenCommands)
    {
        foreach (var (valueName, value) in expectedKey.Values)
        {
            registryKey.SetValue(valueName, value);
        }

        if (expectedKey.Values.TryGetValue(string.Empty, out var command))
        {
            writtenCommands.Add(command);
        }

        foreach (var (subKeyName, expectedSubKey) in expectedKey.SubKeys)
        {
            using var subKey = registryKey.CreateSubKey(subKeyName, writable: true)
                ?? throw new InvalidOperationException($"Could not create Pixoar context menu key {subKeyName}.");
            WriteRegistryKey(subKey, expectedSubKey, writtenCommands);
        }
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<ContextMenuCommandDiagnostic> ReadCommandDiagnostics(
        string extension,
        string menuKeyName,
        IReadOnlyList<string> diagnosticActionPath,
        string expectedAppPath,
        string expectedIconPath)
    {
        var diagnostics = new List<ContextMenuCommandDiagnostic>();
        var menuShellPath = $@"{GetMenuKeyPath(extension, menuKeyName)}\shell";

        using var menuShellKey = Registry.CurrentUser.OpenSubKey(menuShellPath, writable: false);
        if (menuShellKey is null)
        {
            return diagnostics;
        }

        ReadCommandDiagnosticsRecursive(
            extension,
            menuShellPath,
            diagnosticActionPath,
            expectedAppPath,
            expectedIconPath,
            diagnostics);

        return diagnostics;
    }

    [SupportedOSPlatform("windows")]
    private static void ReadCommandDiagnosticsRecursive(
        string extension,
        string shellPath,
        IReadOnlyList<string> actionPath,
        string expectedAppPath,
        string expectedIconPath,
        List<ContextMenuCommandDiagnostic> diagnostics)
    {
        using var shellKey = Registry.CurrentUser.OpenSubKey(shellPath, writable: false);
        if (shellKey is null)
        {
            return;
        }

        foreach (var commandKeyName in shellKey.GetSubKeyNames())
        {
            using var commandKey = shellKey.OpenSubKey(commandKeyName, writable: false);
            if (commandKey is null)
            {
                continue;
            }

            var displayName = commandKey.GetValue("MUIVerb") as string ?? commandKeyName;
            var iconPath = commandKey.GetValue("Icon") as string ?? string.Empty;
            var childPath = $@"{shellPath}\{commandKeyName}";
            var childActionPath = actionPath.Concat([displayName]).ToArray();

            var commandValuePath = $@"{childPath}\command";
            using var commandValueKey = Registry.CurrentUser.OpenSubKey(commandValuePath, writable: false);
            if (commandValueKey is not null)
            {
                var command = commandValueKey.GetValue(string.Empty) as string ?? string.Empty;
                var parsedCommand = ParseRegistryCommand(command);
                var issue = GetCommandIssue(
                    childActionPath,
                    command,
                    parsedCommand,
                    iconPath,
                    expectedAppPath,
                    expectedIconPath);

                diagnostics.Add(new ContextMenuCommandDiagnostic
                {
                    Extension = extension,
                    ActionPath = string.Join(" > ", childActionPath),
                    RegistryKeyPath = $@"HKEY_CURRENT_USER\{commandValuePath}",
                    DisplayName = displayName,
                    Command = command,
                    ExecutablePath = parsedCommand.ExecutablePath,
                    Arguments = parsedCommand.ArgumentsBeforeFile,
                    SelectedFilePlaceholder = parsedCommand.SelectedFilePlaceholder,
                    ExecutableExists = File.Exists(parsedCommand.ExecutablePath),
                    ExecutableWasQuoted = parsedCommand.ExecutableWasQuoted,
                    IconPath = iconPath,
                    IconExists = File.Exists(iconPath),
                    IsValid = string.IsNullOrWhiteSpace(issue),
                    Issue = issue
                });
            }

            ReadCommandDiagnosticsRecursive(
                extension,
                $@"{childPath}\shell",
                childActionPath,
                expectedAppPath,
                expectedIconPath,
                diagnostics);

        }
    }

    private static ParsedRegistryCommand ParseRegistryCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return new ParsedRegistryCommand(string.Empty, string.Empty, string.Empty, false);
        }

        var trimmedCommand = command.Trim();
        var executablePath = string.Empty;
        var arguments = string.Empty;
        var executableWasQuoted = false;

        if (trimmedCommand.StartsWith('"'))
        {
            executableWasQuoted = true;
            var closingQuoteIndex = trimmedCommand.IndexOf('"', 1);
            if (closingQuoteIndex > 1)
            {
                executablePath = trimmedCommand[1..closingQuoteIndex];
                arguments = trimmedCommand[(closingQuoteIndex + 1)..].Trim();
            }
        }
        else
        {
            var firstSpaceIndex = trimmedCommand.IndexOf(' ');
            if (firstSpaceIndex < 0)
            {
                executablePath = trimmedCommand;
            }
            else
            {
                executablePath = trimmedCommand[..firstSpaceIndex];
                arguments = trimmedCommand[(firstSpaceIndex + 1)..].Trim();
            }
        }

        var selectedFilePlaceholder = string.Empty;
        if (arguments.EndsWith("\"%1\"", StringComparison.Ordinal))
        {
            selectedFilePlaceholder = "\"%1\"";
            arguments = arguments[..^4].Trim();
        }
        else if (arguments.EndsWith("%1", StringComparison.Ordinal))
        {
            selectedFilePlaceholder = "%1";
            arguments = arguments[..^2].Trim();
        }

        return new ParsedRegistryCommand(executablePath, arguments, selectedFilePlaceholder, executableWasQuoted);
    }

    private static string GetCommandIssue(
        IReadOnlyList<string> actionPath,
        string command,
        ParsedRegistryCommand parsedCommand,
        string iconPath,
        string expectedAppPath,
        string expectedIconPath)
    {
        var issues = new List<string>();
        var actionName = actionPath.LastOrDefault() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(command))
        {
            issues.Add("The command value is empty.");
        }

        if (!parsedCommand.ExecutableWasQuoted)
        {
            issues.Add("The executable path is not quoted.");
        }

        if (!File.Exists(parsedCommand.ExecutablePath))
        {
            issues.Add("The executable path does not exist.");
        }

        if (parsedCommand.SelectedFilePlaceholder != "\"%1\"")
        {
            issues.Add("The selected file placeholder is not quoted.");
        }

        ValidateIconPath(iconPath, expectedIconPath, issues);

        if (IsResizeAction(actionPath))
        {
            ValidateAppPath(parsedCommand, expectedAppPath, issues);
            ValidateResizeCommand(actionPath.LastOrDefault() ?? string.Empty, parsedCommand, command, issues);
        }
        else if (IsConvertAction(actionPath))
        {
            ValidateAppPath(parsedCommand, expectedAppPath, issues);
            ValidateConvertCommand(actionPath, parsedCommand, issues);
        }
        else if (actionName.Equals("Image Information", StringComparison.OrdinalIgnoreCase))
        {
            ValidateAppPath(parsedCommand, expectedAppPath, issues);
            ValidateArguments(parsedCommand, "--info", issues);
        }
        else if (actionName.Equals("Open in Pixoar", StringComparison.OrdinalIgnoreCase))
        {
            ValidateAppPath(parsedCommand, expectedAppPath, issues);
            ValidateArguments(parsedCommand, "--open", issues);
        }
        else if (IsUpscaleAction(actionPath))
        {
            ValidateAppPath(parsedCommand, expectedAppPath, issues);
            var scale = actionPath.LastOrDefault()?.Contains('4') == true ? "4" : "2";
            ValidateArguments(parsedCommand, $"--explorer-batch upscale --scale {scale}", issues);
        }
        else
        {
            issues.Add("The action type is not recognized.");
        }

        return string.Join(" ", issues);
    }

    private static void ValidateAppPath(ParsedRegistryCommand parsedCommand, string expectedAppPath, List<string> issues)
    {
        if (!string.Equals(Path.GetFileName(parsedCommand.ExecutablePath), AppExecutableName, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add("The command does not point to Pixoar.exe.");
        }

        if (!string.IsNullOrWhiteSpace(expectedAppPath) && !PathEquals(parsedCommand.ExecutablePath, expectedAppPath))
        {
            issues.Add("The command points to a stale app path.");
        }
    }

    private static void ValidateIconPath(string iconPath, string expectedIconPath, List<string> issues)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            issues.Add("The context menu icon is missing.");
            return;
        }

        if (!File.Exists(iconPath))
        {
            issues.Add("The context menu icon file does not exist.");
        }

        if (!string.IsNullOrWhiteSpace(expectedIconPath) && !PathEquals(iconPath, expectedIconPath))
        {
            issues.Add("The context menu icon points to a stale path.");
        }
    }

    private static void ValidateResizeCommand(
        string displayName,
        ParsedRegistryCommand parsedCommand,
        string command,
        List<string> issues)
    {
        if (!TryParseResizePercentage(displayName, out var percentage))
        {
            issues.Add("The resize display label is not a percentage preset.");
            return;
        }

        if (command.Contains("--preset", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add("The command still uses --preset.");
        }

        if (parsedCommand.ArgumentsBeforeFile.Contains('%', StringComparison.Ordinal))
        {
            issues.Add("The command arguments contain a raw percent symbol.");
        }

        ValidateArguments(parsedCommand, $"--explorer-batch resize --percentage {percentage}", issues);
    }

    private static bool IsUpscaleAction(IReadOnlyList<string> actionPath) =>
        actionPath.Count == 2 &&
        actionPath[0].Equals("Upscale", StringComparison.OrdinalIgnoreCase) &&
        (actionPath[1].Equals("2x", StringComparison.OrdinalIgnoreCase) ||
         actionPath[1].Equals("4x", StringComparison.OrdinalIgnoreCase));

    private static void ValidateConvertCommand(
        IReadOnlyList<string> actionPath,
        ParsedRegistryCommand parsedCommand,
        List<string> issues)
    {
        var arguments = parsedCommand.ArgumentsBeforeFile.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (arguments.Length != 4 ||
            !arguments[0].Equals("--explorer-batch", StringComparison.OrdinalIgnoreCase) ||
            !arguments[1].Equals("convert", StringComparison.OrdinalIgnoreCase) ||
            !arguments[2].Equals("--format", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add("Expected arguments: --explorer-batch convert --format <format>.");
            return;
        }

        var format = arguments[3];
        if (TryGetDdsCompressionCommand(actionPath, out var ddsCommand))
        {
            if (!format.Equals(ddsCommand.FormatArgument, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add($"Expected DDS compression format argument: {ddsCommand.FormatArgument}.");
            }

            return;
        }

        if (!SupportedExtensions.Contains($".{format}", StringComparer.OrdinalIgnoreCase))
        {
            issues.Add($"The convert command uses an unsupported format: {format}.");
        }
    }

    private static void ValidateArguments(
        ParsedRegistryCommand parsedCommand,
        string expectedArguments,
        List<string> issues)
    {
        if (!string.Equals(parsedCommand.ArgumentsBeforeFile, expectedArguments, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add($"Expected arguments: {expectedArguments}.");
        }
    }

    private static bool IsResizeAction(IReadOnlyList<string> actionPath)
    {
        return actionPath.Count >= 2 &&
            actionPath[0].Equals("Resize", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConvertAction(IReadOnlyList<string> actionPath)
    {
        return (actionPath.Count >= 2 &&
                (actionPath[0].Equals("Convert", StringComparison.OrdinalIgnoreCase) ||
                 actionPath[0].Equals("DDS", StringComparison.OrdinalIgnoreCase))) ||
            TryGetDdsCompressionCommand(actionPath, out _);
    }

    private static bool TryGetDdsCompressionCommand(
        IReadOnlyList<string> actionPath,
        out (string DisplayName, string FormatArgument) command)
    {
        command = default;
        var displayName = actionPath.LastOrDefault();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return false;
        }

        foreach (var candidate in DdsCompressionCommands)
        {
            if (displayName.Equals(candidate.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                displayName.Equals($"DDS ({candidate.DisplayName})", StringComparison.OrdinalIgnoreCase))
            {
                command = candidate;
                return true;
            }
        }

        return false;
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<string> RemoveForExtension(string extension) =>
        RemoveForAssociation(extension);

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<string> RemoveForImageFiles() =>
        RemoveForAssociation(ImageFileAssociation);

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<string> RemoveForAssociation(string association)
    {
        var removedMenuPaths = new List<string>();
        using var shellKey = Registry.CurrentUser.OpenSubKey(GetShellKeyPath(association), writable: true);
        if (shellKey is null)
        {
            return removedMenuPaths;
        }

        var existingMenuKeyNames = shellKey
            .GetSubKeyNames()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var menuKeyName in MenuKeyNames)
        {
            if (!existingMenuKeyNames.Contains(menuKeyName))
            {
                continue;
            }

            shellKey.DeleteSubKeyTree(menuKeyName, throwOnMissingSubKey: false);
            removedMenuPaths.Add(GetMenuKeyPath(association, menuKeyName));
        }

        return removedMenuPaths;
    }

    [SupportedOSPlatform("windows")]
    private static bool HasMenuRegistration(string association)
    {
        foreach (var menuKeyName in MenuKeyNames)
        {
            using var menuKey = Registry.CurrentUser.OpenSubKey(
                GetMenuKeyPath(association, menuKeyName),
                writable: false);
            if (menuKey is not null)
            {
                return true;
            }
        }

        return false;
    }

    [SupportedOSPlatform("windows")]
    private static void NotifyShellAssociationChanged()
    {
        SHChangeNotify(0x08000000, 0, nint.Zero, nint.Zero);
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint eventId, uint flags, nint item1, nint item2);

    private static ContextMenuExecutablePaths ResolveExecutablePaths()
    {
        var executablePaths = CreateInstalledExecutablePaths();
        var issues = GetExecutablePathIssues(executablePaths);
        if (issues.Count > 0)
        {
            throw new InvalidOperationException(string.Join(' ', issues));
        }

        return executablePaths;
    }

    private static ContextMenuExecutablePaths CreateInstalledExecutablePaths()
    {
        var installDirectory = GetInstallDirectory();
        return new ContextMenuExecutablePaths(
            Path.Combine(installDirectory, CliExecutableName),
            Path.Combine(installDirectory, AppExecutableName),
            Path.Combine(installDirectory, ContextMenuIconRelativePath));
    }

    private static IReadOnlyList<string> GetExecutablePathIssues(ContextMenuExecutablePaths executablePaths)
    {
        var issues = new List<string>();
        var installDirectory = GetInstallDirectory();

        if (IsProjectBuildOutputDirectory(installDirectory))
        {
            issues.Add("Context menu registration must be run from an installed or published Pixoar folder, not from bin\\Debug or bin\\Release.");
        }

        if (!File.Exists(executablePaths.AppPath))
        {
            issues.Add($"Pixoar.exe was not found in the installation folder: {installDirectory}");
        }

        if (!File.Exists(executablePaths.CliPath))
        {
            issues.Add($"Pixoar.Cli.exe was not found in the installation folder: {installDirectory}");
        }

        if (!File.Exists(executablePaths.ContextMenuIconPath))
        {
            issues.Add($"The Pixoar context menu icon was not found in the installation folder: {ContextMenuIconRelativePath}");
        }

        return issues;
    }

    private static string GetInstallDirectory()
    {
        return Path.GetFullPath(AppContext.BaseDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsProjectBuildOutputDirectory(string directory)
    {
        var parts = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

        for (var index = 0; index < parts.Length - 1; index++)
        {
            if (parts[index].Equals("bin", StringComparison.OrdinalIgnoreCase) &&
                (parts[index + 1].Equals("Debug", StringComparison.OrdinalIgnoreCase) ||
                 parts[index + 1].Equals("Release", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static int? GetResizePresetPercentage(ResizePreset preset)
    {
        if (preset.Percentage is > 0)
        {
            return preset.Percentage.Value;
        }

        var normalizedName = NormalizeResizePresetText(preset.Name);
        return TryParseResizePercentage(normalizedName, out var percentage) ? percentage : null;
    }

    private static string NormalizeResizePresetText(string value)
    {
        return value.Trim().Replace('\u00d7', 'x');
    }

    private static bool TryParseResizePercentage(string value, out int percentage)
    {
        percentage = 0;
        var trimmed = value.Trim();

        return trimmed.EndsWith('%')
            && int.TryParse(trimmed.TrimEnd('%'), out percentage)
            && percentage > 0;
    }

    private static string GetConvertPresetArgument(ConvertPreset preset)
    {
        return (string.IsNullOrWhiteSpace(preset.Format) ? preset.Name : preset.Format)
            .Trim()
            .TrimStart('.')
            .ToLowerInvariant();
    }

    private static ConvertPreset[] GetEnabledConvertPresets(PixoarSettings settings)
    {
        return settings.ConvertPresets
            .Where(preset => preset.IsEnabled)
            .Where(preset => !string.IsNullOrWhiteSpace(GetConvertPresetArgument(preset)))
            .ToArray();
    }

    private static IReadOnlyList<(string DisplayName, string FormatArgument)> GetEnabledDdsCompressionCommands(
        PixoarSettings settings)
    {
        if (!settings.ContextMenu.EnableConvertPresets ||
            !GetEnabledConvertPresets(settings).Any(preset =>
                GetConvertPresetArgument(preset).Equals("dds", StringComparison.OrdinalIgnoreCase)))
        {
            return [];
        }

        return DdsCompressionCommands
            .Where(command => IsDdsCompressionShortcutEnabled(settings.ContextMenu, command.FormatArgument))
            .ToArray();
    }

    private static int GetDdsCompressionShortcutCapacity(
        PixoarSettings settings,
        ContextMenuExecutablePaths executablePaths)
    {
        for (var count = DdsCompressionCommands.Length; count >= 0; count--)
        {
            var candidateMenu = CreateExpectedMenuKey(
                settings,
                executablePaths,
                ".png",
                DdsCompressionCommands.Take(count).ToArray());
            if (CountStaticCascadeEntries(candidateMenu) <= StaticCascadeEntryLimit)
            {
                return count;
            }
        }

        return 0;
    }

    private static IReadOnlyList<string> GetStaticMenuBudgetIssues(
        PixoarSettings settings,
        ContextMenuExecutablePaths executablePaths)
    {
        var requestedMenu = CreateExpectedMenuKey(
            settings,
            executablePaths,
            ".png",
            GetEnabledDdsCompressionCommands(settings));
        var totalEntryCount = CountStaticCascadeEntries(requestedMenu);

        if (totalEntryCount <= StaticCascadeEntryLimit)
        {
            return [];
        }

        return
        [
            $"Pixoar is configured with {totalEntryCount} static menu entries for normal images, but Windows Explorer displays at most {StaticCascadeEntryLimit}. Disable a resize/convert preset or a DDS compression shortcut before applying changes."
        ];
    }

    private static bool IsDdsCompressionShortcutEnabled(ContextMenuSettings settings, string formatArgument)
    {
        return formatArgument switch
        {
            "dds-dxt1" => settings.EnableDdsDxt1Shortcut,
            "dds-dxt3" => settings.EnableDdsDxt3Shortcut,
            "dds-dxt5" => settings.EnableDdsDxt5Shortcut,
            "dds-bc7" => settings.EnableDdsBc7Shortcut,
            "dds-uncompressed" => settings.EnableDdsUncompressedShortcut,
            _ => false
        };
    }

    private static string CreateRegistryKeyName(string value)
    {
        var sanitized = new string(value
            .Select(character => char.IsLetterOrDigit(character) ? character : '_')
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "Command" : sanitized;
    }

    private static bool PathEquals(string first, string second)
    {
        return string.Equals(
            Path.GetFullPath(first),
            Path.GetFullPath(second),
            StringComparison.OrdinalIgnoreCase);
    }

    private static int CountStaticCascadeEntries(ExpectedRegistryKey menuKey)
    {
        return menuKey.SubKeys.TryGetValue("shell", out var shellKey)
            ? CountStaticCascadeEntriesInShell(shellKey)
            : 0;
    }

    private static int CountStaticCascadeEntriesInShell(ExpectedRegistryKey shellKey)
    {
        var entryCount = 0;
        foreach (var commandKey in shellKey.SubKeys.Values)
        {
            entryCount++;
            if (commandKey.SubKeys.TryGetValue("shell", out var childShellKey))
            {
                entryCount += CountStaticCascadeEntriesInShell(childShellKey);
            }
        }

        return entryCount;
    }

    private static string GetShellKeyPath(string extension)
    {
        return $@"{SystemFileAssociationsPath}\{extension}\shell";
    }

    private static string GetMenuKeyPath(string extension, string menuKeyName = MenuKeyName)
    {
        return $@"{GetShellKeyPath(extension)}\{menuKeyName}";
    }

    private sealed class ExpectedRegistryKey
    {
        public Dictionary<string, string> Values { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, ExpectedRegistryKey> SubKeys { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public ExpectedRegistryKey AddSubKey(string name)
        {
            var subKey = new ExpectedRegistryKey();
            SubKeys.Add(name, subKey);
            return subKey;
        }
    }

    private sealed record ContextMenuRegistration(
        string MenuKeyName,
        string DisplayName,
        IReadOnlyList<string> DiagnosticActionPath,
        ExpectedRegistryKey ExpectedKey);

    private sealed record ContextMenuExecutablePaths(string CliPath, string AppPath, string ContextMenuIconPath);

    private sealed record ParsedRegistryCommand(
        string ExecutablePath,
        string ArgumentsBeforeFile,
        string SelectedFilePlaceholder,
        bool ExecutableWasQuoted);
}
