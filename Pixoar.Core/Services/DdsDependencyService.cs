using Pixoar.Core.Interfaces;

namespace Pixoar.Core.Services;

internal sealed class DdsDependencyService : IDdsDependencyService
{
    private const string TexconvFileName = "texconv.exe";

    public string MissingTexconvMessage =>
        "DDS support requires texconv.exe. Build Pixoar with the bundled tool or place texconv.exe beside the app.";

    public string? ResolveTexconvPath()
    {
        var candidate = Path.Combine(AppContext.BaseDirectory, TexconvFileName);
        return File.Exists(candidate) ? Path.GetFullPath(candidate) : null;
    }

    public bool IsTexconvAvailable()
    {
        return ResolveTexconvPath() is not null;
    }
}
