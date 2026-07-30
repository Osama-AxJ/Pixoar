using System.Runtime.Versioning;
using Microsoft.Win32;
using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.Core.Services;

internal sealed class PixoarCleanupService(
    IContextMenuService contextMenuService,
    IApplicationPathProvider pathProvider,
    IApplicationLogger logger) : IPixoarCleanupService
{
    public async Task<CleanupResult> CleanupAsync(
        bool removeUserData,
        CancellationToken cancellationToken = default)
    {
        var result = new CleanupResult();

        await TryRunAsync(
            "Removed Explorer context menu entries.",
            () => contextMenuService.UninstallAsync(cancellationToken),
            result,
            cancellationToken).ConfigureAwait(false);

        if (OperatingSystem.IsWindows())
        {
            await TryRunAsync(
                "Removed legacy Run entries.",
                () =>
                {
#pragma warning disable CA1416
                    RemoveLegacyRunEntries();
#pragma warning restore CA1416
                    return Task.CompletedTask;
                },
                result,
                cancellationToken).ConfigureAwait(false);

            await TryRunAsync(
                "Removed Pixoar registry keys.",
                () =>
                {
#pragma warning disable CA1416
                    RemovePixoarRegistryKeys();
#pragma warning restore CA1416
                    return Task.CompletedTask;
                },
                result,
                cancellationToken).ConfigureAwait(false);
        }

        if (removeUserData)
        {
            await RemoveUserDataAsync(result, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private async Task RemoveUserDataAsync(CleanupResult result, CancellationToken cancellationToken)
    {
        const string successMessage = "Removed Pixoar user settings and logs.";

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await logger.LogInformationAsync("Removing Pixoar user settings and logs.", cancellationToken).ConfigureAwait(false);
            RemoveUserData();
            result.Actions.Add(successMessage);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var message = $"{successMessage.TrimEnd('.')} failed: {ex.Message}";
            result.Errors.Add(message);
            await logger.LogErrorAsync(message, ex, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task TryRunAsync(
        string successMessage,
        Func<Task> action,
        CleanupResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await action().ConfigureAwait(false);
            result.Actions.Add(successMessage);
            await logger.LogInformationAsync(successMessage, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var message = $"{successMessage.TrimEnd('.')} failed: {ex.Message}";
            result.Errors.Add(message);
            await logger.LogErrorAsync(message, ex, cancellationToken).ConfigureAwait(false);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void RemoveLegacyRunEntries()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run",
            writable: true);

        runKey?.DeleteValue("Pixoar", throwOnMissingValue: false);
        runKey?.DeleteValue("Pixoar.App", throwOnMissingValue: false);
    }

    [SupportedOSPlatform("windows")]
    private static void RemovePixoarRegistryKeys()
    {
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Pixoar", throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Applications\Pixoar.exe", throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Applications\Pixoar.Cli.exe", throwOnMissingSubKey: false);
    }

    private void RemoveUserData()
    {
        var appDataDirectory = Path.GetFullPath(pathProvider.AppDataDirectory);
        var expectedDirectory = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Pixoar"));

        if (!string.Equals(appDataDirectory, expectedDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refusing to remove an unexpected user data path.");
        }

        if (Directory.Exists(appDataDirectory))
        {
            Directory.Delete(appDataDirectory, recursive: true);
        }
    }
}
