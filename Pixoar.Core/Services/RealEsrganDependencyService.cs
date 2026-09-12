using Pixoar.Core.Interfaces;

namespace Pixoar.Core.Services;

internal sealed class RealEsrganDependencyService : IRealEsrganDependencyService
{
    private const string ToolDirectory = "tools\\realesrgan";
    private const string ExecutableName = "realesrgan-ncnn-vulkan.exe";

    public string MissingBackendMessage =>
        "The local AI upscaler is missing. Reinstall Pixoar to restore its bundled Real-ESRGAN files.";

    public string? ResolveExecutablePath()
    {
        var candidate = Path.Combine(AppContext.BaseDirectory, ToolDirectory, ExecutableName);
        return File.Exists(candidate) ? Path.GetFullPath(candidate) : null;
    }

    public string? ResolveModelsDirectory()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, ToolDirectory, "models");
        var param = Path.Combine(directory, "realesrgan-x4plus.param");
        var weights = Path.Combine(directory, "realesrgan-x4plus.bin");
        return File.Exists(param) && File.Exists(weights) ? Path.GetFullPath(directory) : null;
    }
}
