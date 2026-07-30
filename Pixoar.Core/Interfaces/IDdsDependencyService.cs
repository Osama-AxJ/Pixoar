namespace Pixoar.Core.Interfaces;

/// <summary>
/// Resolves external dependencies required for DDS processing.
/// </summary>
public interface IDdsDependencyService
{
    /// <summary>
    /// Gets the user-facing message shown when texconv.exe is unavailable.
    /// </summary>
    string MissingTexconvMessage { get; }

    /// <summary>
    /// Resolves texconv.exe from the app folder, development tools folder, or PATH.
    /// </summary>
    /// <returns>The full path to texconv.exe, or null when it cannot be found.</returns>
    string? ResolveTexconvPath();

    /// <summary>
    /// Tests whether texconv.exe can be resolved.
    /// </summary>
    /// <returns><see langword="true" /> when texconv.exe is available; otherwise <see langword="false" />.</returns>
    bool IsTexconvAvailable();
}
