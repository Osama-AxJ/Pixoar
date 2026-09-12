using ImageMagick;
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
    var output = Path.Combine(root, "results folder");

    foreach (var scale in new[] { 2, 4 })
    {
        var result = await service.UpscaleAsync(new UpscaleRequest { InputPath = input, Scale = scale, OutputFolder = output });
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
    Console.WriteLine("PASS: 2x/4x dimensions, Unicode/spaced paths, PNG alpha, conflict naming, batch continuation, and DDS rejection.");
}
finally
{
    if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class SmokePaths(string root) : IApplicationPathProvider
{
    public string AppDataDirectory { get; } = root;
    public string SettingsFilePath => Path.Combine(AppDataDirectory, "settings.json");
    public string LogsDirectory => Path.Combine(AppDataDirectory, "Logs");
}
