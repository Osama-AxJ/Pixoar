namespace Pixoar.Core.Interfaces;

/// <summary>Resolves the native files required by local Real-ESRGAN upscaling.</summary>
public interface IRealEsrganDependencyService
{
    /// <summary>Gets the actionable message shown when the backend is absent.</summary>
    string MissingBackendMessage { get; }

    /// <summary>Resolves the bundled executable.</summary>
    string? ResolveExecutablePath();

    /// <summary>Resolves the bundled model directory when its required pair is present.</summary>
    string? ResolveModelsDirectory();
}
