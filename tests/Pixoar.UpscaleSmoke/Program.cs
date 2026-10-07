using ImageMagick;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Pixoar.Core.Configuration;
using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

var root = Path.Combine(Path.GetTempPath(), "Pixoar Upscale Smoke ü", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var input = Path.Combine(root, "transparent input.png");
    var pixels = new byte[40 * 30 * 4];
    for (var index = 0; index < 40 * 30; index++)
    {
        pixels[index * 4] = 40;
        pixels[(index * 4) + 1] = 120;
        pixels[(index * 4) + 2] = 220;
        pixels[(index * 4) + 3] = (index % 40) < 20 ? byte.MinValue : (byte)128;
    }
    using (var source = new MagickImage(pixels, new PixelReadSettings(40, 30, StorageType.Char, "RGBA")))
    {
        source.Write(input);
    }

    var services = new ServiceCollection();
    services.AddPixoarCore();
    services.AddSingleton<IApplicationPathProvider>(new SmokePaths(Path.Combine(root, "appdata")));
    using var provider = services.BuildServiceProvider();
    await provider.GetRequiredService<ISettingsService>().LoadAsync();
    var service = provider.GetRequiredService<IImageUpscaleService>();
    var dependency = provider.GetRequiredService<IRealEsrganDependencyService>();
    VerifyBundledDiscovery(root, dependency);
    var output = Path.Combine(root, "results folder");

    foreach (var scale in new[] { 2, 4 })
    {
        var operation = service.UpscaleAsync(new UpscaleRequest { InputPath = input, Scale = scale, OutputFolder = output });
        await VerifyLoadedRuntimeAsync(operation, dependency.ResolveExecutablePath()!);
        var result = await operation;
        Assert(result.Success && result.OutputPath is not null, $"{scale}x upscale failed: {result.Error?.Message}");
        using var image = new MagickImage(result.OutputPath!);
        Assert(image.Width == 40u * (uint)scale && image.Height == 30u * (uint)scale, $"{scale}x output dimensions were incorrect.");
        var alpha = image.GetPixels().ToByteArray(PixelMapping.RGBA)!;
        var opaqueSideIndex = ((10 * (int)image.Width + ((int)image.Width * 3 / 4)) * 4) + 3;
        Assert(alpha[3] == 0 && alpha[opaqueSideIndex] > 0, "Transparent PNG alpha was not preserved.");
    }

    var dds = Path.Combine(root, "unsupported.dds");
    await File.WriteAllBytesAsync(dds, [0]);
    var batch = await service.UpscaleBatchAsync([
        new UpscaleRequest { InputPath = input, Scale = 2, OutputFolder = output },
        new UpscaleRequest { InputPath = dds, Scale = 2, OutputFolder = output }]);
    Assert(batch.SuccessCount == 1 && batch.ErrorCount == 1, "Batch processing did not continue after an ineligible DDS file.");
    var failureServices = new ServiceCollection();
    failureServices.AddPixoarCore();
    failureServices.AddSingleton<IApplicationPathProvider>(new SmokePaths(Path.Combine(root, "failure appdata")));
    failureServices.AddSingleton<IRealEsrganDependencyService>(new FailingBackend(Environment.ProcessPath!, dependency.ResolveModelsDirectory()!));
    using var failureProvider = failureServices.BuildServiceProvider();
    await failureProvider.GetRequiredService<ISettingsService>().LoadAsync();
    var failure = await failureProvider.GetRequiredService<IImageUpscaleService>().UpscaleAsync(
        new UpscaleRequest { InputPath = input, Scale = 2, OutputFolder = Path.Combine(root, "failed output") });
    Assert(!failure.Success && failure.Error?.Message.Contains("Vulkan-compatible", StringComparison.Ordinal) == true,
        "A nonzero backend exit did not produce the Vulkan guidance.");
    Assert(failure.OutputPath is null || !File.Exists(failure.OutputPath), "Failed inference left a completed output.");
    Console.WriteLine("PASS: 2x/4x dimensions, Unicode/spaced paths, PNG alpha, conflict naming, batch continuation, and DDS rejection.");
    Console.WriteLine("PASS: bundled backend/model discovery, application-local Microsoft OpenMP loading, and backend/Vulkan failure handling.");
}
finally
{
    if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void VerifyBundledDiscovery(string root, IRealEsrganDependencyService dependency)
{
    var executable = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "tools", "realesrgan", "realesrgan-ncnn-vulkan.exe"));
    var model = Path.Combine(Path.GetDirectoryName(executable)!, "models", "realesrgan-x4plus.bin");
    Assert(string.Equals(executable, dependency.ResolveExecutablePath(), StringComparison.OrdinalIgnoreCase), "Backend discovery was not application-relative.");
    var decoys = Path.Combine(root, "Decoy PATH and working directory");
    Directory.CreateDirectory(Path.Combine(decoys, "models"));
    File.WriteAllText(Path.Combine(decoys, "realesrgan-ncnn-vulkan.exe"), "decoy");
    File.WriteAllText(Path.Combine(decoys, "models", "realesrgan-x4plus.bin"), "decoy");
    File.WriteAllText(Path.Combine(decoys, "models", "realesrgan-x4plus.param"), "decoy");
    var oldPath = Environment.GetEnvironmentVariable("PATH");
    var oldDirectory = Environment.CurrentDirectory;
    try
    {
        Environment.SetEnvironmentVariable("PATH", decoys + Path.PathSeparator + oldPath);
        Environment.CurrentDirectory = decoys;
        File.Move(executable, executable + ".smoke-hidden");
        Assert(dependency.ResolveExecutablePath() is null, "Missing backend fell back to PATH or the working directory.");
        File.Move(model, model + ".smoke-hidden");
        Assert(dependency.ResolveModelsDirectory() is null, "Missing model fell back to developer files.");
    }
    finally
    {
        if (File.Exists(executable + ".smoke-hidden")) File.Move(executable + ".smoke-hidden", executable);
        if (File.Exists(model + ".smoke-hidden")) File.Move(model + ".smoke-hidden", model);
        Environment.SetEnvironmentVariable("PATH", oldPath);
        Environment.CurrentDirectory = oldDirectory;
    }
}

static async Task VerifyLoadedRuntimeAsync(Task operation, string executable)
{
    var runtime = Path.Combine(Path.GetDirectoryName(executable)!, "vcomp140.dll");
    var observed = false;
    while (!operation.IsCompleted)
    {
        foreach (var process in Process.GetProcessesByName("realesrgan-ncnn-vulkan"))
        {
            using (process)
            {
                try
                {
                    if (!string.Equals(process.MainModule?.FileName, executable, StringComparison.OrdinalIgnoreCase)) continue;
                    foreach (ProcessModule module in process.Modules)
                    {
                        if (!module.ModuleName.Equals("vcomp140.dll", StringComparison.OrdinalIgnoreCase)) continue;
                        Assert(string.Equals(module.FileName, runtime, StringComparison.OrdinalIgnoreCase), $"Backend loaded an unexpected OpenMP runtime: {module.FileName}");
                        observed = true;
                    }
                }
                catch (System.ComponentModel.Win32Exception) { /* Process may exit between enumeration and inspection. */ }
                catch (InvalidOperationException) when (process.HasExited) { }
            }
        }
        await Task.Delay(10);
    }
    Assert(observed, "The bundled OpenMP module was not observed during inference.");
    Console.WriteLine($"PASS runtime module: {runtime}");
}

sealed class SmokePaths(string root) : IApplicationPathProvider
{
    public string AppDataDirectory { get; } = root;
    public string SettingsFilePath => Path.Combine(AppDataDirectory, "settings.json");
    public string LogsDirectory => Path.Combine(AppDataDirectory, "Logs");
}

sealed class FailingBackend(string executable, string models) : IRealEsrganDependencyService
{
    public string MissingBackendMessage => "Missing test AI upscaler.";
    public string? ResolveExecutablePath() => executable;
    public string? ResolveModelsDirectory() => models;
}
