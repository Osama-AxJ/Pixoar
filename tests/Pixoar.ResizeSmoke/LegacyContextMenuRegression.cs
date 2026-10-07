using System.Reflection;
using System.Text.Json;
using Microsoft.Win32;
using Pixoar.Core.Configuration;

namespace Pixoar.ResizeSmoke;

internal static class LegacyContextMenuRegression
{
    public static void Run()
    {
        var path = $@"Software\Pixoar.Tests\LegacyMenu\{Guid.NewGuid():N}";
        try
        {
            using var associations = Registry.CurrentUser.CreateSubKey(path);
            string[] extensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tif", ".tiff"];
            foreach (var extension in extensions)
            {
                WriteLegacy(associations, extension);
                foreach (var action in new[] { "Convert", "Resize", "Info", "Open", "Dds", "Upscale" })
                {
                    using var key = associations.CreateSubKey($@"{extension}\shell\Pixoar\shell\Current{action}\command");
                    key.SetValue("", $"current {action} command");
                }
            }

            using (var other = associations.CreateSubKey(@".png\shell\OtherApp\shell\40_RemoveBackground"))
                other.SetValue("MUIVerb", "Remove Background");
            WriteLegacy(associations, ".dds");
            WriteLegacy(associations, ".other");
            var before = Snapshot(associations, excludeLegacy: true);
            var removed = Cleanup(associations);
            Assert(removed.Count == 7, "Migration did not remove exactly the seven known legacy branches.");
            foreach (var extension in extensions)
            {
                using var legacy = associations.OpenSubKey($@"{extension}\shell\Pixoar\shell\40_RemoveBackground");
                Assert(legacy is null, $"Legacy branch remains for {extension}.");
            }
            Assert(before == Snapshot(associations, excludeLegacy: true), "Migration changed current actions or unrelated entries.");
            Assert(Cleanup(associations).Count == 0, "Migration was not idempotent.");

            WriteLegacy(associations, ".png", "OtherApp.exe");
            var guarded = Snapshot(associations, excludeLegacy: false);
            Assert(Cleanup(associations).Count == 0 && guarded == Snapshot(associations, false), "An unrelated executable was removed.");
            WriteLegacy(associations, ".png");
            using (associations.CreateSubKey(@".png\shell\Pixoar\shell\40_RemoveBackground\Unrelated")) { }
            guarded = Snapshot(associations, false);
            Assert(Cleanup(associations).Count == 0 && guarded == Snapshot(associations, false), "An unrecognized subtree was removed.");
            Console.WriteLine("PASS: isolated legacy registry migration, seven exact branches, current/unrelated preservation, command guards, missing keys, and idempotency.");
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
        }
    }

    private static IReadOnlyList<string> Cleanup(RegistryKey associations)
    {
        var service = typeof(PixoarCoreServiceCollectionExtensions).Assembly.GetType("Pixoar.Core.Services.ContextMenuService", throwOnError: true)!;
        var method = service.GetMethod("RemoveLegacyBackgroundRemovalEntries", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (IReadOnlyList<string>)method.Invoke(null, [associations, CancellationToken.None])!;
    }

    private static void WriteLegacy(RegistryKey associations, string extension, string executable = "Pixoar.exe")
    {
        using var key = associations.CreateSubKey($@"{extension}\shell\Pixoar\shell\40_RemoveBackground");
        key.SetValue("MUIVerb", "Remove Background");
        using var command = key.CreateSubKey("command");
        command.SetValue("", $"\"C:\\Old Pixoar\\{executable}\" --explorer-batch remove-bg --local true \"%1\"");
    }

    private static string Snapshot(RegistryKey root, bool excludeLegacy)
    {
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
        void Read(RegistryKey key, string relative)
        {
            if (excludeLegacy && relative.Contains(@"\Pixoar\shell\40_RemoveBackground", StringComparison.Ordinal) &&
                !relative.StartsWith(".dds", StringComparison.Ordinal) && !relative.StartsWith(".other", StringComparison.Ordinal)) return;
            values[relative] = "<key>";
            foreach (var name in key.GetValueNames())
                values[$"{relative}|{name}"] = JsonSerializer.Serialize(new { Kind = key.GetValueKind(name), Value = key.GetValue(name) });
            foreach (var child in key.GetSubKeyNames())
            {
                using var next = key.OpenSubKey(child)!;
                Read(next, relative.Length == 0 ? child : $@"{relative}\{child}");
            }
        }
        Read(root, "");
        return JsonSerializer.Serialize(values);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
