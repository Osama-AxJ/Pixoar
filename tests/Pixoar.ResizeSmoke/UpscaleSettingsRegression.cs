using System.IO;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Pixoar.App.Commands;
using Pixoar.App.Services;
using Pixoar.App.ViewModels;
using Pixoar.Core.Configuration;
using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.ResizeSmoke;

internal static class UpscaleSettingsRegression
{
    private const string AssociationsPath = @"Software\Classes\SystemFileAssociations";
    private static readonly string[] Extensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tif", ".tiff", ".dds"];
    private static readonly (bool Two, bool Four)[] Combinations = [(true, true), (true, false), (false, true), (false, false)];

    public static async Task VerifySettingsAsync(string root)
    {
        var cases = new[]
        {
            (Json: "{}", Two: true, Four: true),
            (Json: """{"contextMenu":{"enableUpscale":true}}""", Two: true, Four: true),
            (Json: """{"contextMenu":{"enableUpscale":false}}""", Two: false, Four: false),
            (Json: """{"contextMenu":{"EnableUpscale":false,"enableUpscale2x":true}}""", Two: true, Four: false),
            (Json: """{"contextMenu":{"enableUpscale":true,"enableUpscale4x":false}}""", Two: true, Four: false),
            (Json: """{"general":{"defaultUpscaleScale":4,"checkForUpdates":false},"output":{"conflictBehavior":"skipExistingFiles","saveUpscaledFilesInUpscaleFolder":true},"contextMenu":{"enableUpscale":false}}""", Two: false, Four: false)
        };
        for (var index = 0; index < cases.Length; index++)
        {
            var paths = new TestPaths(Path.Combine(root, "Upscale settings", index.ToString()));
            Directory.CreateDirectory(paths.AppDataDirectory);
            await File.WriteAllTextAsync(paths.SettingsFilePath, cases[index].Json);
            using (var provider = CreateProvider(paths))
            {
                var service = provider.GetRequiredService<ISettingsService>();
                var settings = await service.LoadAsync();
                Assert(settings.ContextMenu.EnableUpscale2x == cases[index].Two && settings.ContextMenu.EnableUpscale4x == cases[index].Four,
                    "Legacy/mixed/missing Upscale preferences did not migrate correctly.");
                if (index == cases.Length - 1)
                {
                    Assert(!settings.General.CheckForUpdates && settings.Output.ConflictBehavior == OutputConflictBehavior.SkipExistingFiles &&
                        settings.Output.SaveUpscaledFilesInUpscaleFolder, "Upscale migration changed unrelated settings.");
                    AssertCleanJson(await File.ReadAllTextAsync(paths.SettingsFilePath));
                    var migrated = await File.ReadAllTextAsync(paths.SettingsFilePath);
                    await service.LoadAsync();
                    Assert(migrated == await File.ReadAllTextAsync(paths.SettingsFilePath), "Settings migration was not idempotent.");
                }
                foreach (var combination in Combinations)
                {
                    await service.UpdateAsync(s => { s.ContextMenu.EnableUpscale2x = combination.Two; s.ContextMenu.EnableUpscale4x = combination.Four; });
                    using var restarted = CreateProvider(paths);
                    var loaded = await restarted.GetRequiredService<ISettingsService>().LoadAsync();
                    Assert(loaded.ContextMenu.EnableUpscale2x == combination.Two && loaded.ContextMenu.EnableUpscale4x == combination.Four,
                        "Independent Upscale preferences did not survive a settings-service restart.");
                    AssertCleanJson(await File.ReadAllTextAsync(paths.SettingsFilePath));
                }
            }
        }
        Console.WriteLine("PASS: legacy enabled/disabled and mixed-field migration, fresh defaults, four-way persistence, obsolete default removal, other settings, and migration idempotency.");
    }

    // Explicitly invoked only by --upscale-settings, from a temporary app folder.
    // The normal Apply path is exercised against HKCU and all original Pixoar roots restored.
    public static async Task VerifyRegistryAsync(string root)
    {
        var ownedPaths = Extensions.Append("image").SelectMany(extension =>
            new[] { "Pixoar", "PixoarInfo", "PixoarDds" }.Select(menu => $@"{AssociationsPath}\{extension}\shell\{menu}")).ToArray();
        var backup = ownedPaths.ToDictionary(path => path, path => ReadPath(path));
        await File.WriteAllTextAsync(Path.Combine(root, "original-explorer-registrations.json"), JsonSerializer.Serialize(backup));
        var before = SnapshotAssociations();
        var unrelatedBefore = SnapshotAssociations(skipOwned: true);
        var paths = new TestPaths(Path.Combine(root, "Registry test appdata"));
        using var provider = CreateProvider(paths);
        var settingsService = provider.GetRequiredService<ISettingsService>();
        var menuService = provider.GetRequiredService<IContextMenuService>();
        await settingsService.LoadAsync();
        await settingsService.UpdateAsync(s => s.ContextMenu.EnableContextMenu = true);
        try
        {
            string? otherActions = null;
            foreach (var combination in Combinations)
            {
                var vm = CreateViewModel(provider, paths);
                vm.EnableUpscale2x = combination.Two;
                vm.EnableUpscale4x = combination.Four;
                var countWithoutUpscale = vm.MainContextMenuEntryCount - ((combination.Two || combination.Four)
                    ? 1 + (combination.Two ? 1 : 0) + (combination.Four ? 1 : 0) : 0);
                Assert(countWithoutUpscale == 10, "Explorer menu budget changed unrelated default actions.");
                await ExecuteAsync(vm.ApplyChangesCommand);
                Assert(vm.ContextMenuInstallationStatusText == "Installed", $"Apply did not install the selected Upscale combination: {vm.StatusText}");
                var report = await menuService.DiagnoseAsync();
                Assert(report.IsValid, report.ToDisplayText());
                VerifyUpscaleKeys(combination.Two, combination.Four);
                Assert(SnapshotAssociations(skipOwned: true) == unrelatedBefore, "Apply changed an unrelated registry entry.");
                var currentOtherActions = SnapshotAssociations(skipUpscale: true);
                otherActions ??= currentOtherActions;
                Assert(otherActions == currentOtherActions, "Changing Upscale flags changed another Explorer action.");
                var applied = SnapshotAssociations();
                await ExecuteAsync(vm.ApplyChangesCommand);
                Assert(applied == SnapshotAssociations(), "Repeated Apply changed registry values or left stale entries.");
                using var restarted = CreateProvider(paths);
                await restarted.GetRequiredService<ISettingsService>().LoadAsync();
                var restoredVm = CreateViewModel(restarted, paths);
                Assert(restoredVm.EnableUpscale2x == combination.Two && restoredVm.EnableUpscale4x == combination.Four,
                    "Settings ViewModel did not restore both Explorer checkboxes.");
                Console.WriteLine($"PASS: actual Settings Apply 2x={combination.Two}, 4x={combination.Four}; exact registry commands, independent persistence, other actions, and repeated Apply.");
            }
            // The previous combined registration contains both actions. A legacy false
            // preference must remove those actions on the next normal Apply.
            foreach (var legacyEnabled in new[] { true, false })
            {
                await settingsService.UpdateAsync(s => { s.ContextMenu.EnableUpscale2x = true; s.ContextMenu.EnableUpscale4x = true; });
                Assert(await menuService.ApplyAsync() == ContextMenuInstallationStatus.Installed, "Could not seed the combined registration.");
                var migrationPaths = new TestPaths(Path.Combine(root, "Registry migration " + legacyEnabled));
                Directory.CreateDirectory(migrationPaths.AppDataDirectory);
                await File.WriteAllTextAsync(migrationPaths.SettingsFilePath,
                    JsonSerializer.Serialize(new { contextMenu = new { enableContextMenu = true, enableUpscale = legacyEnabled } }));
                using var migrationProvider = CreateProvider(migrationPaths);
                await migrationProvider.GetRequiredService<ISettingsService>().LoadAsync();
                var migratedVm = CreateViewModel(migrationProvider, migrationPaths);
                await ExecuteAsync(migratedVm.ApplyChangesCommand);
                Assert(migratedVm.ContextMenuInstallationStatusText == "Installed", "Migrated Apply failed.");
                VerifyUpscaleKeys(legacyEnabled, legacyEnabled);
                Assert(otherActions == SnapshotAssociations(skipUpscale: true), "Registration migration changed other Explorer actions.");
                var migratedRegistry = SnapshotAssociations();
                await ExecuteAsync(migratedVm.ApplyChangesCommand);
                Assert(migratedRegistry == SnapshotAssociations(), "Migrated registration Apply was not idempotent.");
            }
        }
        finally
        {
            foreach (var path in ownedPaths)
            {
                Registry.CurrentUser.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
                if (backup[path] is { } original)
                {
                    using var key = Registry.CurrentUser.CreateSubKey(path);
                    WriteNode(key, original);
                }
            }
            var serviceType = typeof(PixoarCoreServiceCollectionExtensions).Assembly.GetType("Pixoar.Core.Services.ContextMenuService", throwOnError: true)!;
            serviceType.GetMethod("NotifyShellAssociationChanged", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null);
        }
        Assert(before == SnapshotAssociations(), "Original Explorer registrations were not restored after testing.");
        Console.WriteLine("PASS: existing combined registrations migrate without stale/duplicate actions; original HKCU registrations restored exactly.");
    }

    private static SettingsViewModel CreateViewModel(IServiceProvider provider, TestPaths paths) => new(
        provider.GetRequiredService<ISettingsService>(), provider.GetRequiredService<IContextMenuService>(),
        provider.GetRequiredService<IPixoarCleanupService>(), provider.GetRequiredService<IUpdateService>(), paths,
        new TestPrompts(), new TestDialogs(), provider.GetRequiredService<IImageFormatDetector>());

    private static ServiceProvider CreateProvider(TestPaths paths)
    {
        var services = new ServiceCollection();
        services.AddPixoarCore();
        services.AddSingleton<IApplicationPathProvider>(paths);
        return services.BuildServiceProvider();
    }

    private static void VerifyUpscaleKeys(bool two, bool four)
    {
        foreach (var extension in Extensions)
        {
            using var upscale = Registry.CurrentUser.OpenSubKey($@"{AssociationsPath}\{extension}\shell\Pixoar\shell\50_Upscale");
            var expected = extension != ".dds" && (two || four);
            Assert((upscale is not null) == expected, "Empty or ineligible Upscale submenu remains.");
            if (upscale is null) continue;
            using var shell = upscale.OpenSubKey("shell");
            var children = shell!.GetSubKeyNames();
            Assert(children.Length == (two ? 1 : 0) + (four ? 1 : 0), "Stale or duplicate Upscale entries remain.");
            foreach (var scale in new[] { 2, 4 })
            {
                using var command = shell.OpenSubKey(scale == 2 ? @"01_2x\command" : @"02_4x\command");
                Assert((command is not null) == (scale == 2 ? two : four), "Incorrect independent Upscale action.");
                if (command is not null)
                    Assert((string?)command.GetValue("") == $"\"{Path.Combine(AppContext.BaseDirectory, "Pixoar.exe")}\" --explorer-batch upscale --scale {scale} \"%1\"", "Wrong scale or multi-selection command arguments.");
            }
        }
    }

    private static void AssertCleanJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert(!document.RootElement.GetProperty("general").TryGetProperty("defaultUpscaleScale", out _), "Obsolete desktop scale was serialized.");
        Assert(!document.RootElement.GetProperty("contextMenu").TryGetProperty("enableUpscale", out _), "Combined Explorer flag was serialized.");
    }

    private static async Task ExecuteAsync(AsyncRelayCommand command)
    {
        Assert(command.CanExecute(null), "Apply Changes was disabled.");
        command.Execute(null);
        var field = typeof(AsyncRelayCommand).GetField("_isExecuting", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var deadline = DateTime.UtcNow.AddMinutes(1);
        while ((bool)field.GetValue(command)!)
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Apply Changes did not finish.");
            await Task.Delay(10);
        }
    }

    private sealed record RegistryNode(Dictionary<string, RegistryValue> Values, SortedDictionary<string, RegistryNode> Children);
    private sealed record RegistryValue(RegistryValueKind Kind, object Value);

    private static RegistryNode? ReadPath(string path)
    {
        using var key = Registry.CurrentUser.OpenSubKey(path);
        return key is null ? null : ReadNode(key);
    }

    private static RegistryNode ReadNode(RegistryKey key)
    {
        var values = key.GetValueNames().Order(StringComparer.Ordinal).ToDictionary(name => name,
            name => new RegistryValue(key.GetValueKind(name), key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)!));
        var children = new SortedDictionary<string, RegistryNode>(StringComparer.Ordinal);
        foreach (var name in key.GetSubKeyNames())
        {
            using var child = key.OpenSubKey(name)!;
            children.Add(name, ReadNode(child));
        }
        return new RegistryNode(values, children);
    }

    private static void WriteNode(RegistryKey key, RegistryNode node)
    {
        foreach (var value in node.Values) key.SetValue(value.Key, value.Value.Value, value.Value.Kind);
        foreach (var child in node.Children)
        {
            using var subkey = key.CreateSubKey(child.Key);
            WriteNode(subkey, child.Value);
        }
    }

    private static string SnapshotAssociations(bool skipOwned = false, bool skipUpscale = false)
    {
        var tree = ReadPath(AssociationsPath)!;
        foreach (var extension in Extensions.Append("image"))
        {
            if (!tree.Children.TryGetValue(extension, out var association) || !association.Children.TryGetValue("shell", out var shell)) continue;
            foreach (var menuName in new[] { "Pixoar", "PixoarInfo", "PixoarDds" })
            {
                if (skipOwned) shell.Children.Remove(menuName);
                else if (skipUpscale && menuName == "Pixoar" && shell.Children.TryGetValue(menuName, out var menu) && menu.Children.TryGetValue("shell", out var actions))
                    actions.Children.Remove("50_Upscale");
            }
        }
        return JsonSerializer.Serialize(tree);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class TestPaths(string root) : IApplicationPathProvider
    {
        public string AppDataDirectory => root;
        public string SettingsFilePath => Path.Combine(root, "settings.json");
        public string LogsDirectory => Path.Combine(root, "Logs");
    }
    private sealed class TestPrompts : IUserPromptService
    {
        public bool ConfirmOverwriteRisk() => false;
        public bool ConfirmRemoveUserData() => false;
        public void ShowAutomaticUpdateAvailable(UpdateCheckResult result) { }
        public void ShowManualUpdateCheckResult(UpdateCheckResult result) { }
    }
    private sealed class TestDialogs : IFileDialogService
    {
        public IReadOnlyList<string> SelectImageFiles() => [];
        public string? SelectFolder(string title = "Select Folder") => null;
    }
}
