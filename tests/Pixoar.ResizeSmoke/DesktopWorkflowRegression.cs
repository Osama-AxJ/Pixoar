using System.Reflection;
using System.IO;
using System.Windows.Media;
using System.Windows.Threading;
using ImageMagick;
using Microsoft.Extensions.DependencyInjection;
using Pixoar.App.Commands;
using Pixoar.App.Models;
using Pixoar.App.Services;
using Pixoar.App.ViewModels;
using Pixoar.Core.Configuration;
using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.ResizeSmoke;

internal static class DesktopWorkflowRegression
{
    public static Task RunAsync(string root)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.UnhandledException += (_, e) =>
            {
                e.Handled = true;
                completion.TrySetException(e.Exception);
                dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
            };
            dispatcher.InvokeAsync(async () =>
            {
                try { await VerifyAsync(root); completion.TrySetResult(); }
                catch (Exception e) { completion.TrySetException(e); }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static async Task VerifyAsync(string root)
    {
        var folder = Path.Combine(root, "Desktop inputs with spaces");
        Directory.CreateDirectory(Path.Combine(folder, "nested"));
        var png = Path.Combine(folder, "z sample.png");
        var jpg = Path.Combine(folder, "nested", "a sample.jpg");
        using (var image = new MagickImage(new MagickColor(70, 110, 180), 32, 16))
        {
            image.Write(png);
            image.Write(jpg);
        }
        await File.WriteAllTextAsync(Path.Combine(folder, "unsupported.txt"), "not an image");
        var services = new ServiceCollection();
        services.AddPixoarCore();
        services.AddSingleton<IApplicationPathProvider>(new TestPaths(Path.Combine(root, "Desktop appdata")));
        using var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<ISettingsService>();
        var settingsPath = provider.GetRequiredService<IApplicationPathProvider>().SettingsFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        await File.WriteAllTextAsync(settingsPath, """{"general":{"defaultUpscaleScale":4}}""");
        await settings.LoadAsync();
        await settings.UpdateAsync(s => { s.Dds.GenerateMipmaps = true; s.Dds.MipmapMode = DdsMipmapMode.FullChain; });
        var dialogs = new TestDialogs { Files = [png], Folder = folder };
        var windows = new TestWindows();
        var vm = new MainViewModel(dialogs, windows,
            provider.GetRequiredService<IImageFormatDetector>(), provider.GetRequiredService<IImageInfoService>(),
            provider.GetRequiredService<IImagePreviewService>(), provider.GetRequiredService<IDdsService>(),
            provider.GetRequiredService<IImageConversionService>(), provider.GetRequiredService<IImageResizeService>(),
            provider.GetRequiredService<IImageUpscaleService>(), settings);
        try
        {
            Assert(vm.SelectedUpscaleScale == "2x", "Desktop must start at 2x even with a legacy saved 4x preference.");
            Assert(vm.VersionText == "Version 1.0.0", "Desktop version did not resolve from the assembly.");
            await ExecuteAsync(vm.AddImagesCommand);
            Assert(vm.Images.Count == 1, "Add Images did not load the selected file.");
            await WaitAsync(() => vm.SelectedImage?.HasPreviewImage == true);
            vm.ShowSelectedPreviewCommand.Execute(null);
            await ExecuteAsync(vm.ShowImageInformationCommand);
            Assert(windows.PreviewCount == 1 && windows.InfoCount == 1, "Preview/information commands did not open their views.");
            await ExecuteAsync(vm.AddFolderCommand);
            Assert(vm.Images.Count == 2, "Add Folder did not recurse, deduplicate, and reject unsupported files.");
            vm.SortImagesCommand.Execute("FileName");
            Assert(vm.Images[0].FileName == "a sample.jpg", "Filename sorting did not apply.");
            vm.SelectedImages.Add(vm.Images[0]);
            vm.RemoveSelectedCommand.Execute(null);
            Assert(vm.Images.Count == 1 && vm.SelectedImages.Count == 0, "Remove Selected did not update selection.");
            vm.ClearListCommand.Execute(null);
            Assert(vm.Images.Count == 0, "Clear List did not clear Originals.");
            await ExecuteAsync(vm.DropFilesCommand, new[] { folder });
            Assert(vm.Images.Count == 2, "Dropped folder did not load its images.");
            SelectAll(vm);
            vm.SelectedOutputFormat = "DDS";
            await ExecuteAsync(vm.ConvertCommand);
            Assert(vm.Images.Count == 0 && vm.ProcessedImages.Count == 2 && vm.IsProcessedListActive && vm.Errors.Count == 0,
                "Batch conversion did not move successful results to Processed.");
            await WaitAsync(() => vm.SelectedImage?.HasPreviewImage == true && vm.CanShowMipChain);
            await ExecuteAsync(vm.ToggleMipChainCommand);
            Assert(vm.PreviewMipmaps.Count == 6, "DDS mip-chain preview did not show the stored six levels.");
            vm.ShowSelectedPreviewCommand.Execute(null);
            vm.ShowMipPreviewCommand.Execute(vm.PreviewMipmaps[^1]);
            await ExecuteAsync(vm.ShowImageInformationCommand);
            Assert(windows.PreviewCount == 3 && windows.InfoCount == 2, "Expanded DDS/mip preview and DDS information commands failed.");
            dialogs.Folder = Path.Combine(root, "Desktop exports with spaces");
            await ExecuteAsync(vm.ExportCommand);
            Assert(vm.ProcessedImages.All(i => File.Exists(i.FilePath) && i.FilePath.StartsWith(dialogs.Folder, StringComparison.Ordinal)),
                "Export did not update Processed to persistent output files.");
            vm.ShowSourceImagesCommand.Execute(null);
            Assert(!vm.IsProcessedListActive && vm.DisplayedImages.Count == 0, "Originals tab did not show the source list.");
            vm.ClearListCommand.Execute(null);
            await ExecuteAsync(vm.DropFilesCommand, new[] { png, jpg });
            SelectAll(vm);
            vm.SelectedResizeMethod = "By Percentage";
            vm.SelectedResizePercentage = "50";
            await ExecuteAsync(vm.ResizeCommand);
            Assert(vm.ProcessedImages.Count == 2 && vm.ProcessedImages.All(i => i.PixelWidth == 16 && i.PixelHeight == 8),
                "Batch percentage resize did not integrate correctly with Processed.");
            vm.ClearListCommand.Execute(null);
            await ExecuteAsync(vm.DropFilesCommand, new[] { png });
            Assert(vm.IsProcessedListActive && vm.DisplayedImages.Count == 0 && !vm.ResizeCommand.CanExecute(null),
                "An empty active Processed view must not enable Resize for unselected Originals.");
            vm.ShowSourceImagesCommand.Execute(null);
            SelectAll(vm);
            vm.SelectedResizeMethod = "By Dimensions";
            vm.ResizeWidth = "8";
            vm.ResizeHeight = "4";
            Assert(vm.ResizeCommand.CanExecute(null), "Resize must be enabled after selecting Originals with valid dimensions.");
            await ExecuteAsync(vm.ResizeCommand);
            Assert(vm.ProcessedImages.Count == 1 && vm.ProcessedImages[0].PixelWidth == 8 && vm.ProcessedImages[0].PixelHeight == 4,
                "Dimension resize did not integrate correctly with Processed.");
            vm.ClearListCommand.Execute(null);
            await ExecuteAsync(vm.DropFilesCommand, new[] { png, jpg });
            vm.ShowSourceImagesCommand.Execute(null);
            SelectAll(vm);
            vm.SelectedUpscaleScale = "2x";
            var settingsBeforeUpscale = await File.ReadAllTextAsync(settingsPath);
            await ExecuteAsync(vm.UpscaleCommand);
            Assert(vm.ProcessedImages.Count == 2 && vm.Images.Count == 0 && vm.Errors.Count == 0 &&
                vm.ProcessedImages.All(i => i.PixelWidth == 64 && i.PixelHeight == 32), "Batch Upscale did not integrate with Processed.");
            vm.ClearListCommand.Execute(null);
            Assert(vm.Images.Count == 0 && vm.ProcessedImages.Count == 0, "Clear List did not clear both lists.");
            await ExecuteAsync(vm.DropFilesCommand, new[] { png, jpg });
            vm.ShowSourceImagesCommand.Execute(null);
            SelectAll(vm);
            vm.SelectedUpscaleScale = "4x";
            await ExecuteAsync(vm.UpscaleCommand);
            Assert(vm.ProcessedImages.Count == 2 && vm.Errors.Count == 0 &&
                vm.ProcessedImages.All(i => i.PixelWidth == 128 && i.PixelHeight == 64), "Desktop 4x selection did not upscale correctly.");
            Assert(vm.SelectedUpscaleScale == "4x", "The selected scale should remain available in the current session.");
            Assert(await File.ReadAllTextAsync(settingsPath) == settingsBeforeUpscale,
                "Desktop Upscale must not persist its session scale or change other settings.");
            using var restartedProvider = services.BuildServiceProvider();
            var restartedSettings = restartedProvider.GetRequiredService<ISettingsService>();
            await restartedSettings.LoadAsync();
            var restartedVm = new MainViewModel(dialogs, windows,
                restartedProvider.GetRequiredService<IImageFormatDetector>(), restartedProvider.GetRequiredService<IImageInfoService>(),
                restartedProvider.GetRequiredService<IImagePreviewService>(), restartedProvider.GetRequiredService<IDdsService>(),
                restartedProvider.GetRequiredService<IImageConversionService>(), restartedProvider.GetRequiredService<IImageResizeService>(),
                restartedProvider.GetRequiredService<IImageUpscaleService>(), restartedSettings);
            Assert(restartedVm.SelectedUpscaleScale == "2x", "A restarted desktop must reset the selection to 2x.");
            restartedVm.CleanupPendingExports();
            vm.ClearListCommand.Execute(null);
            Console.WriteLine("PASS: desktop 2x/4x batch selection, legacy default compatibility, session-only selection, and restart to 2x.");
            Console.WriteLine("PASS: Add Images/Folder, drop, selection/removal/clear, sorting, tabs, export, information, preview, expanded DDS/mips, convert/resize/upscale Processed workflows.");
        }
        finally { vm.CleanupPendingExports(); }
    }

    private static void SelectAll(MainViewModel vm)
    {
        vm.SelectedImages.Clear();
        foreach (var item in vm.DisplayedImages) vm.SelectedImages.Add(item);
    }

    private static async Task ExecuteAsync(AsyncRelayCommand command, object? parameter = null)
    {
        Assert(command.CanExecute(parameter), "A required desktop command was disabled.");
        command.Execute(parameter);
        var field = typeof(AsyncRelayCommand).GetField("_isExecuting", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await WaitAsync(() => !(bool)field.GetValue(command)!);
    }

    private static async Task WaitAsync(Func<bool> ready)
    {
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (!ready())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Desktop workflow did not complete.");
            await Task.Delay(10);
        }
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

    private sealed class TestDialogs : IFileDialogService
    {
        public IReadOnlyList<string> Files { get; set; } = [];
        public string? Folder { get; set; }
        public IReadOnlyList<string> SelectImageFiles() => Files;
        public string? SelectFolder(string title = "Select Folder") => Folder;
    }

    private sealed class TestWindows : IWindowService
    {
        public int PreviewCount { get; private set; }
        public int InfoCount { get; private set; }
        public void ShowSettingsWindow(IReadOnlyCollection<ImageFileItem> images) { }
        public Task ShowImageInformationAsync(ImageFileItem image) { InfoCount++; return Task.CompletedTask; }
        public void ShowImagePreview(ImageSource image, string title, string resolution, BitmapScalingMode scalingMode) => PreviewCount++;
    }
}
